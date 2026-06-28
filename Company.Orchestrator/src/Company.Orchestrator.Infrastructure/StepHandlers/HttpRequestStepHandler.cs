using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Calls a REST API and stores the raw response in workflow variables.
/// </summary>
public sealed class HttpRequestStepHandler : IStepHandler
{
    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "ApiKey",
        "X-Api-Key",
        "Bearer",
    };

    private static readonly HashSet<string> SensitiveQueryParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "token",
        "apikey",
        "api_key",
        "password",
        "secret",
        "authorization",
        "access_token",
        "refresh_token",
    };

    private static readonly HashSet<string> SupportedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialResolver _credentialResolver;
    private readonly ILogger<HttpRequestStepHandler> _logger;

    public string HandlerType => "http.request";

    public HttpRequestStepHandler(
        IHttpClientFactory httpClientFactory,
        ICredentialResolver credentialResolver,
        ILogger<HttpRequestStepHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _credentialResolver = credentialResolver;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var urlRaw = GetString(config, "url");
        if (string.IsNullOrWhiteSpace(urlRaw))
            return StepResult.Fail("http.request: 'url' is required.");

        var methodRaw = GetString(config, "method", "GET").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(methodRaw))
            return StepResult.Fail("http.request: 'method' is required.");
        if (!SupportedMethods.Contains(methodRaw))
        {
            return StepResult.Fail(
                "http.request: 'method' must be GET, POST, PUT, PATCH, or DELETE.");
        }

        var outputVariable = GetString(config, "outputVariable");
        if (string.IsNullOrWhiteSpace(outputVariable))
            return StepResult.Fail("http.request: 'outputVariable' is required.");

        var timeoutSeconds = GetInt(config, "timeoutSeconds", defaultValue: 60);
        if (timeoutSeconds <= 0)
            return StepResult.Fail("http.request: 'timeoutSeconds' must be greater than 0.");

        var failOnNonSuccess = GetBool(config, "failOnNonSuccessStatus", defaultValue: true);
        var contentType      = GetString(config, "contentType", "application/json");

        var url             = context.Interpolate(urlRaw.Trim());
        var headers         = ParseStringDictionary(config, "headers", context);
        var queryParameters = ParseStringDictionary(config, "queryParameters", context);
        var body            = config.ContainsKey("body")
            ? context.Interpolate(GetString(config, "body"))
            : string.Empty;

        url = AppendQueryParameters(url, queryParameters);

        try
        {
            await ApplyCredentialHeadersAsync(config, headers, cancellationToken);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"http.request: {ex.Message}");
        }

        var safeUrl = RedactUrlForLogging(url);
        var safeHeaders = RedactHeadersForLogging(headers);

        _logger.LogInformation(
            "http.request: {Method} {Url} headers=[{Headers}] timeout={TimeoutSeconds}s",
            methodRaw,
            safeUrl,
            string.Join(", ", safeHeaders.Select(kv => $"{kv.Key}={kv.Value}")),
            timeoutSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage response;
        string responseBody;
        string responseContentType;
        int statusCode;
        bool isSuccess;
        Dictionary<string, string> responseHeaders;

        try
        {
            var httpClient = _httpClientFactory.CreateClient("WorkflowHttpRequest");
            using var request = new HttpRequestMessage(new HttpMethod(methodRaw), url);

            foreach (var (key, value) in headers)
                request.Headers.TryAddWithoutValidation(key, value);

            if (!string.IsNullOrEmpty(body) &&
                methodRaw is "POST" or "PUT" or "PATCH" or "DELETE")
            {
                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }

            response = await httpClient.SendAsync(request, timeoutCts.Token);
            using (response)
            {
                responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                responseContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                statusCode = (int)response.StatusCode;
                isSuccess = response.IsSuccessStatusCode;
                responseHeaders = response.Headers
                    .Concat(response.Content.Headers)
                    .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => string.Join(", ", g.SelectMany(v => v.Value)),
                        StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "http.request: timeout after {DurationMs}ms for {Method} {Url}",
                stopwatch.ElapsedMilliseconds,
                methodRaw,
                safeUrl);
            return StepResult.Fail($"http.request: request timed out after {timeoutSeconds} second(s).");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var reason = ClassifyHttpRequestException(ex);
            _logger.LogWarning(
                ex,
                "http.request: {Reason} after {DurationMs}ms for {Method} {Url}",
                reason,
                stopwatch.ElapsedMilliseconds,
                methodRaw,
                safeUrl);
            return StepResult.Fail($"http.request: {reason}. {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "http.request: request failed after {DurationMs}ms for {Method} {Url}",
                stopwatch.ElapsedMilliseconds,
                methodRaw,
                safeUrl);
            return StepResult.Fail($"http.request: {ex.Message}");
        }

        stopwatch.Stop();

        var headersJson = JsonSerializer.Serialize(responseHeaders);
        var output = BuildOutput(outputVariable, statusCode, isSuccess, headersJson, responseBody, responseContentType);

        _logger.LogInformation(
            "http.request: completed {Method} {Url} status={StatusCode} duration={DurationMs}ms success={Success}",
            methodRaw,
            safeUrl,
            statusCode,
            stopwatch.ElapsedMilliseconds,
            isSuccess);

        var summary =
            $"HTTP {methodRaw} {safeUrl} — status {statusCode}, {responseBody.Length} byte(s), " +
            $"{stopwatch.ElapsedMilliseconds}ms.";

        if (!isSuccess && failOnNonSuccess)
        {
            return StepResult.Fail(
                $"http.request: HTTP {statusCode}.",
                output);
        }

        return StepResult.Ok(output: output, outputData: summary);
    }

    private static Dictionary<string, object> BuildOutput(
        string outputVar,
        int statusCode,
        bool isSuccess,
        string headersJson,
        string body,
        string contentType)
    {
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]                     = body,
            [$"{outputVar}_statusCode"]     = statusCode,
            [$"{outputVar}_isSuccess"]      = isSuccess,
            [$"{outputVar}_headers"]        = headersJson,
            [$"{outputVar}_body"]           = body,
            [$"{outputVar}_contentType"]    = contentType,
        };
    }

    private static Dictionary<string, string> ParseStringDictionary(
        Dictionary<string, object> config,
        string key,
        WorkflowContext context)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in je.EnumerateObject())
            {
                var value = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.GetRawText();
                result[prop.Name] = context.Interpolate(value);
            }

            return result;
        }

        if (raw is Dictionary<string, object> dict)
        {
            foreach (var (k, v) in dict)
                result[k] = context.Interpolate(v?.ToString() ?? string.Empty);
            return result;
        }

        if (raw is IEnumerable<KeyValuePair<string, object>> pairs)
        {
            foreach (var (k, v) in pairs)
                result[k] = context.Interpolate(v?.ToString() ?? string.Empty);
            return result;
        }

        return result;
    }

    private async Task ApplyCredentialHeadersAsync(
        Dictionary<string, object> config,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var bearerName = GetString(config, "bearerTokenCredentialName").Trim();
        if (!string.IsNullOrWhiteSpace(bearerName))
        {
            var token = await _credentialResolver.GetSecretByNameAsync(bearerName, cancellationToken);
            headers["Authorization"] = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? token
                : $"Bearer {token}";
        }

        var apiKeyName = GetString(config, "apiKeyCredentialName").Trim();
        if (!string.IsNullOrWhiteSpace(apiKeyName))
        {
            var apiKey = await _credentialResolver.GetSecretByNameAsync(apiKeyName, cancellationToken);
            var headerName = GetString(config, "apiKeyHeaderName", "X-Api-Key").Trim();
            if (string.IsNullOrWhiteSpace(headerName))
                headerName = "X-Api-Key";
            headers[headerName] = apiKey;
        }

        var authName = GetString(config, "authCredentialName").Trim();
        if (!string.IsNullOrWhiteSpace(authName) &&
            string.IsNullOrWhiteSpace(bearerName) &&
            string.IsNullOrWhiteSpace(apiKeyName))
        {
            var secret = await _credentialResolver.GetSecretByNameAsync(authName, cancellationToken);
            headers["Authorization"] = secret.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? secret
                : $"Bearer {secret}";
        }
    }

    private static string AppendQueryParameters(string url, Dictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var parts = parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Key))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");

        return url + separator + string.Join('&', parts);
    }

    private static string RedactUrlForLogging(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var builder = new UriBuilder(uri);
        if (string.IsNullOrEmpty(builder.Query) || builder.Query.Length <= 1)
            return url;

        var query = builder.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < query.Length; i++)
        {
            var pair = query[i].Split('=', 2);
            if (pair.Length == 0)
                continue;

            var name = Uri.UnescapeDataString(pair[0]);
            if (SensitiveQueryParams.Contains(name))
                query[i] = $"{pair[0]}=***";
        }

        builder.Query = string.Join('&', query);
        return builder.Uri.ToString();
    }

    private static Dictionary<string, string> RedactHeadersForLogging(Dictionary<string, string> headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers)
        {
            if (SensitiveHeaderNames.Contains(key) ||
                value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                result[key] = "***";
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string ClassifyHttpRequestException(HttpRequestException ex)
    {
        if (ex.StatusCode == HttpStatusCode.RequestTimeout)
            return "Request timed out";

        var message = ex.Message;
        if (message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
            return "SSL/TLS failure";

        if (message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase))
            return "DNS failure";

        return "HTTP request failed";
    }

    private static string GetString(Dictionary<string, object> config, string key, string fallback = "")
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return fallback;

        if (raw is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? fallback,
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                _                    => el.GetRawText(),
            };
        }

        return raw.ToString() ?? fallback;
    }

    private static int GetInt(Dictionary<string, object> config, string key, int defaultValue)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var num))
                return num;
            if (je.ValueKind == JsonValueKind.String &&
                int.TryParse(je.GetString(), out var parsed))
                return parsed;
            return defaultValue;
        }

        return int.TryParse(raw.ToString(), out var value) ? value : defaultValue;
    }

    private static bool GetBool(Dictionary<string, object> config, string key, bool defaultValue)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.String => bool.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
                _                    => defaultValue,
            };
        }

        if (raw is bool flag)
            return flag;

        return bool.TryParse(raw.ToString(), out var boolVal) ? boolVal : defaultValue;
    }
}
