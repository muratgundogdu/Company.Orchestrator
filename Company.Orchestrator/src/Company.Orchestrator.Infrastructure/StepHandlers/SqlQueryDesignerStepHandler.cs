using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Executes a parameterized SELECT query against SQL Server and stores rows as a DataTable JSON variable.
/// </summary>
public sealed class SqlQueryDesignerStepHandler : IStepHandler
{
    private static readonly Regex ParameterNameRegex = new(
        @"@([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "TRUNCATE",
        "EXEC", "EXECUTE", "CREATE", "GRANT", "REVOKE", "DENY", "BACKUP", "RESTORE",
    ];

    private readonly IConfiguration _configuration;
    private readonly ICredentialResolver _credentialResolver;
    private readonly ILogger<SqlQueryDesignerStepHandler> _logger;

    public string HandlerType => "sql.query";

    public SqlQueryDesignerStepHandler(
        IConfiguration configuration,
        ICredentialResolver credentialResolver,
        ILogger<SqlQueryDesignerStepHandler> logger)
    {
        _configuration = configuration;
        _credentialResolver = credentialResolver;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var query          = GetString(config, "query");
        var outputVariable = GetString(config, "outputVariable");
        var timeoutSeconds = GetInt(config, "timeoutSeconds", defaultValue: 60);

        if (string.IsNullOrWhiteSpace(query))
            return StepResult.Fail("sql.query: 'query' is required.");
        if (string.IsNullOrWhiteSpace(outputVariable))
            return StepResult.Fail("sql.query: 'outputVariable' is required.");
        if (timeoutSeconds <= 0)
            return StepResult.Fail("sql.query: 'timeoutSeconds' must be greater than 0.");

        if (!IsSelectOnlyQuery(query, out var queryError))
            return StepResult.Fail($"sql.query: {queryError}");

        var directConnection = context.Interpolate(GetString(config, "connectionString")).Trim();
        var connectionName   = GetString(config, "connectionName").Trim();
        string connectionString;
        string connectionLabel;

        if (!string.IsNullOrWhiteSpace(directConnection))
        {
            connectionString = directConnection;
            connectionLabel = MaskConnectionString(directConnection);
        }
        else if (!string.IsNullOrWhiteSpace(connectionName))
        {
            var vaultSecret = await _credentialResolver.TryGetSecretByNameAsync(
                connectionName, cancellationToken);

            if (!string.IsNullOrWhiteSpace(vaultSecret))
            {
                connectionString = vaultSecret;
                connectionLabel = $"vault:{connectionName} ({MaskConnectionString(vaultSecret)})";
            }
            else
            {
                var resolved = _configuration.GetConnectionString(connectionName);
                if (string.IsNullOrWhiteSpace(resolved))
                {
                    return StepResult.Fail(
                        $"sql.query: connection name '{connectionName}' not found in credential vault or configuration.");
                }

                connectionString = resolved;
                connectionLabel = $"config:{connectionName} ({MaskConnectionString(resolved)})";
            }
        }
        else
        {
            return StepResult.Fail(
                "sql.query: either 'connectionString' or 'connectionName' is required.");
        }

        var parameters = ParseStringDictionary(config, "parameters", context);
        var requiredParams = ExtractParameterNames(query);

        foreach (var paramName in requiredParams)
        {
            if (!parameters.ContainsKey(paramName))
            {
                return StepResult.Fail(
                    $"sql.query: missing parameter '@{paramName}' in parameters config.");
            }
        }

        var queryPreview = BuildQueryPreview(query, parameters.Keys);
        _logger.LogInformation(
            "sql.query: connection={Connection}, timeout={TimeoutSeconds}s, parameters=[{Parameters}], query={QueryPreview}",
            connectionLabel,
            timeoutSeconds,
            string.Join(", ", parameters.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)),
            queryPreview);

        var stopwatch = Stopwatch.StartNew();
        var rows      = new List<Dictionary<string, string>>();
        var columns   = new List<string>();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(query, connection)
            {
                CommandTimeout = timeoutSeconds,
            };

            foreach (var (name, value) in parameters)
            {
                var paramName = name.StartsWith('@') ? name : $"@{name}";
                command.Parameters.AddWithValue(paramName, string.IsNullOrEmpty(value) ? DBNull.Value : value);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                if (!columns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                    columns.Add(columnName);
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = ConvertDbValue(reader.GetValue(i));
                }

                rows.Add(row);
            }
        }
        catch (SqlException ex) when (ex.Number is -2 or 1222)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "sql.query: timeout after {DurationMs}ms on {Connection}",
                stopwatch.ElapsedMilliseconds,
                connectionLabel);
            return StepResult.Fail($"sql.query: query timed out after {timeoutSeconds} second(s).");
        }
        catch (SqlException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "sql.query: SQL error after {DurationMs}ms on {Connection}",
                stopwatch.ElapsedMilliseconds,
                connectionLabel);
            return StepResult.Fail($"sql.query: SQL error: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "sql.query: connection/query failed after {DurationMs}ms on {Connection}",
                stopwatch.ElapsedMilliseconds,
                connectionLabel);
            return StepResult.Fail($"sql.query: {ex.Message}");
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "sql.query: completed on {Connection}, rows={RowCount}, duration={DurationMs}ms, timeout={TimeoutSeconds}s",
            connectionLabel,
            rows.Count,
            stopwatch.ElapsedMilliseconds,
            timeoutSeconds);

        return BuildResult(outputVariable, columns, rows, stopwatch.ElapsedMilliseconds);
    }

    private static StepResult BuildResult(
        string outputVar,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows,
        long durationMs)
    {
        var json        = JsonSerializer.Serialize(rows);
        var columnsJson = JsonSerializer.Serialize(columns);
        var firstJson   = rows.Count > 0 ? JsonSerializer.Serialize(rows[0]) : "{}";

        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]              = json,
            [$"{outputVar}_count"]   = rows.Count,
            [$"{outputVar}_columns"] = columnsJson,
            [$"{outputVar}_first"]   = firstJson,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonSerializer.Serialize(rows[i]);

        return StepResult.Ok(
            output: output,
            outputData:
                $"SQL query returned {rows.Count} row(s) in {durationMs}ms.");
    }

    private static bool IsSelectOnlyQuery(string query, out string error)
    {
        var normalized = NormalizeSqlForValidation(query);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Query is empty.";
            return false;
        }

        var withoutTrailingSemicolon = normalized.TrimEnd().TrimEnd(';').Trim();
        if (withoutTrailingSemicolon.Contains(';'))
        {
            error = "Multiple SQL statements are not allowed.";
            return false;
        }

        foreach (var keyword in ForbiddenKeywords)
        {
            if (Regex.IsMatch(normalized, $@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                error = $"Only SELECT queries are allowed. Found forbidden keyword '{keyword}'.";
                return false;
            }
        }

        if (!Regex.IsMatch(normalized, @"^\s*(WITH\b|SELECT\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            error = "Query must start with SELECT (or WITH ... SELECT).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeSqlForValidation(string query)
    {
        var text = query.Trim();

        text = Regex.Replace(
            text,
            @"/\*.*?\*/",
            " ",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        text = Regex.Replace(
            text,
            @"--[^\r\n]*",
            " ",
            RegexOptions.CultureInvariant);

        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static HashSet<string> ExtractParameterNames(string query)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ParameterNameRegex.Matches(query))
        {
            if (match.Groups.Count > 1)
                names.Add(match.Groups[1].Value);
        }

        return names;
    }

    private static string BuildQueryPreview(string query, IEnumerable<string> parameterNames)
    {
        var preview = Regex.Replace(query.Trim(), @"\s+", " ");
        if (preview.Length > 160)
            preview = preview[..160] + "…";

        if (!parameterNames.Any())
            return preview;

        return $"{preview} /* parameters: {string.Join(", ", parameterNames.Select(n => "@" + n))} */";
    }

    private static string MaskConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Password = string.Empty,
                UserID   = string.IsNullOrEmpty(new SqlConnectionStringBuilder(connectionString).UserID)
                    ? string.Empty
                    : "***",
            };

            var server   = string.IsNullOrWhiteSpace(builder.DataSource) ? "unknown-server" : builder.DataSource;
            var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "unknown-db" : builder.InitialCatalog;
            return $"{server}/{database}";
        }
        catch
        {
            return "(masked-connection)";
        }
    }

    private static string ConvertDbValue(object value)
    {
        if (value is DBNull or null)
            return string.Empty;

        return value switch
        {
            DateTime dt       => dt.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            bool b            => b ? "true" : "false",
            byte[] bytes      => Convert.ToBase64String(bytes),
            _                 => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
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

        return result;
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
}
