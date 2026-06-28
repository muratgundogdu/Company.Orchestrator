using System.Text;
using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public class ApiCallStepHandler : IStepHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiCallStepHandler> _logger;

    public string HandlerType => "ApiCall";

    public ApiCallStepHandler(IHttpClientFactory httpClientFactory, ILogger<ApiCallStepHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("url", out var urlRaw) || urlRaw is null)
            return StepResult.Fail("ApiCall 'url' is required.");

        var url = context.Interpolate(urlRaw.ToString()!);
        var method = config.GetValueOrDefault("method")?.ToString()?.ToUpperInvariant() ?? "GET";
        var bodyJson = config.TryGetValue("body", out var bodyRaw)
            ? context.Interpolate(bodyRaw?.ToString() ?? "")
            : null;

        var headers = new Dictionary<string, string>();
        if (config.TryGetValue("headers", out var headersRaw) && headersRaw is JsonElement headersEl)
        {
            foreach (var prop in headersEl.EnumerateObject())
                headers[prop.Name] = prop.Value.GetString() ?? "";
        }

        _logger.LogInformation("Calling API {Method} {Url}", method, url);

        var httpClient = _httpClientFactory.CreateClient("WorkflowApiCall");
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        foreach (var (key, value) in headers)
            request.Headers.TryAddWithoutValidation(key, value);

        if (bodyJson is not null && method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("API call failed with status {StatusCode}", response.StatusCode);
            return StepResult.Fail($"API call returned {(int)response.StatusCode}: {responseBody}");
        }

        _logger.LogInformation("API call succeeded with status {StatusCode}", response.StatusCode);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["statusCode"] = (int)response.StatusCode,
                ["responseBody"] = responseBody
            },
            outputData: responseBody);
    }
}
