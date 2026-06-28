using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal sealed class SqlConnectionResolution
{
    public required string ConnectionString { get; init; }
    public required string Label { get; init; }
}

internal sealed class SqlParameterSpec
{
    public required string Name { get; init; }
    public string Value { get; init; } = string.Empty;
    public string Direction { get; init; } = "Input";
}

internal static class SqlStepHandlerHelpers
{
    private static readonly Regex ParameterNameRegex = new(
        @"@([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ExecuteForbiddenKeywords =
    [
        "SELECT", "EXEC", "EXECUTE", "DROP", "ALTER", "TRUNCATE", "CREATE",
        "GRANT", "REVOKE", "DENY", "BACKUP", "RESTORE",
    ];

    private static readonly string[] QueryForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "TRUNCATE",
        "EXEC", "EXECUTE", "CREATE", "GRANT", "REVOKE", "DENY", "BACKUP", "RESTORE",
    ];

    public static async Task<(SqlConnectionResolution? Connection, StepResult? Failure)> ResolveConnectionAsync(
        Dictionary<string, object> config,
        WorkflowContext context,
        IConfiguration configuration,
        ICredentialResolver credentialResolver,
        string stepType,
        CancellationToken cancellationToken)
    {
        var directConnection = context.Interpolate(GetString(config, "connectionString")).Trim();
        var connectionName   = GetString(config, "connectionName").Trim();

        if (!string.IsNullOrWhiteSpace(directConnection))
        {
            return (new SqlConnectionResolution
            {
                ConnectionString = directConnection,
                Label            = MaskConnectionString(directConnection),
            }, null);
        }

        if (!string.IsNullOrWhiteSpace(connectionName))
        {
            var vaultSecret = await credentialResolver.TryGetSecretByNameAsync(
                connectionName, cancellationToken);

            if (!string.IsNullOrWhiteSpace(vaultSecret))
            {
                return (new SqlConnectionResolution
                {
                    ConnectionString = vaultSecret,
                    Label            = $"vault:{connectionName} ({MaskConnectionString(vaultSecret)})",
                }, null);
            }

            var resolved = configuration.GetConnectionString(connectionName);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return (null, StepResult.Fail(
                    $"{stepType}: connection name '{connectionName}' not found in credential vault or configuration."));
            }

            return (new SqlConnectionResolution
            {
                ConnectionString = resolved,
                Label            = $"config:{connectionName} ({MaskConnectionString(resolved)})",
            }, null);
        }

        return (null, StepResult.Fail(
            $"{stepType}: either 'connectionString' or 'connectionName' is required."));
    }

    public static bool IsSelectOnlyQuery(string query, out string error)
    {
        var normalized = NormalizeSqlForValidation(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Query is empty.";
            return false;
        }

        if (!IsSingleStatement(normalized, out error))
            return false;

        foreach (var keyword in QueryForbiddenKeywords)
        {
            if (ContainsKeyword(normalized, keyword))
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

    public static bool IsAllowedExecuteStatement(string sql, out string error)
    {
        var normalized = NormalizeSqlForValidation(sql);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "SQL statement is empty.";
            return false;
        }

        if (!IsSingleStatement(normalized, out error))
            return false;

        foreach (var keyword in ExecuteForbiddenKeywords)
        {
            if (ContainsKeyword(normalized, keyword))
            {
                error = $"Statement type '{keyword}' is not allowed for sql.execute. Use UPDATE, INSERT, DELETE, or MERGE.";
                return false;
            }
        }

        if (!Regex.IsMatch(
                normalized,
                @"^\s*(INSERT\b|UPDATE\b|DELETE\b|MERGE\b)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            error = "Statement must start with INSERT, UPDATE, DELETE, or MERGE.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static string DetectStatementType(string sql)
    {
        var normalized = NormalizeSqlForValidation(sql);
        foreach (var keyword in new[] { "MERGE", "UPDATE", "INSERT", "DELETE" })
        {
            if (Regex.IsMatch(normalized, $@"^\s*{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return keyword;
        }

        return "UNKNOWN";
    }

    public static HashSet<string> ExtractParameterNames(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ParameterNameRegex.Matches(sql))
        {
            if (match.Groups.Count > 1)
                names.Add(match.Groups[1].Value);
        }

        return names;
    }

    public static Dictionary<string, string> ParseParametersObject(
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
        }

        return result;
    }

    public static List<SqlParameterSpec> ParseParametersArray(
        Dictionary<string, object> config,
        string key,
        WorkflowContext context)
    {
        var result = new List<SqlParameterSpec>();
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return result;

        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var name = GetJsonString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                result.Add(new SqlParameterSpec
                {
                    Name      = name.Trim(),
                    Value     = context.Interpolate(GetJsonString(item, "value")),
                    Direction = GetJsonString(item, "direction", "Input"),
                });
            }

            return result;
        }

        if (raw is IEnumerable<object> list)
        {
            foreach (var item in list)
            {
                if (item is not Dictionary<string, object> dict)
                    continue;

                var name = GetString(dict, "name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                result.Add(new SqlParameterSpec
                {
                    Name      = name.Trim(),
                    Value     = context.Interpolate(GetString(dict, "value")),
                    Direction = GetString(dict, "direction", "Input"),
                });
            }
        }

        return result;
    }

    public static Dictionary<string, string> ParametersArrayToDictionary(IEnumerable<SqlParameterSpec> specs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.Name))
                continue;

            var direction = spec.Direction.Trim();
            if (direction.Equals("Output", StringComparison.OrdinalIgnoreCase)
                || direction.Equals("ReturnValue", StringComparison.OrdinalIgnoreCase))
                continue;

            result[spec.Name] = spec.Value;
        }

        return result;
    }

    public static StepResult BuildDataTableOutput(
        string outputVar,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows,
        string outputData)
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

        return StepResult.Ok(output: output, outputData: outputData);
    }

    public static List<Dictionary<string, string>> ReadResultSet(SqlDataReader reader)
    {
        var rows = new List<Dictionary<string, string>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = ConvertDbValue(reader.GetValue(i));
            rows.Add(row);
        }

        return rows;
    }

    public static List<string> ReadColumnNames(SqlDataReader reader)
    {
        var columns = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            if (!columns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                columns.Add(columnName);
        }

        return columns;
    }

    public static string BuildSqlPreview(string sql, IEnumerable<string> parameterNames)
    {
        var preview = Regex.Replace(sql.Trim(), @"\s+", " ");
        if (preview.Length > 160)
            preview = preview[..160] + "…";

        var names = parameterNames.ToList();
        if (names.Count == 0)
            return preview;

        return $"{preview} /* parameters: {string.Join(", ", names.Select(n => n.StartsWith('@') ? n : "@" + n))} */";
    }

    public static string MaskConnectionString(string connectionString)
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

    public static string ConvertDbValue(object value)
    {
        if (value is DBNull or null)
            return string.Empty;

        return value switch
        {
            DateTime dt        => dt.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
            bool b             => b ? "true" : "false",
            byte[] bytes       => Convert.ToBase64String(bytes),
            _                  => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    public static ParameterDirection ParseParameterDirection(string direction) =>
        direction.Trim().ToLowerInvariant() switch
        {
            "output"      => ParameterDirection.Output,
            "inputoutput" => ParameterDirection.InputOutput,
            "returnvalue" => ParameterDirection.ReturnValue,
            _             => ParameterDirection.Input,
        };

    public static void AddInputParameter(SqlCommand command, string name, string? value)
    {
        var paramName = name.StartsWith('@') ? name : $"@{name}";
        command.Parameters.AddWithValue(paramName, string.IsNullOrEmpty(value) ? DBNull.Value : value);
    }

    public static SqlParameter CreateProcedureParameter(SqlParameterSpec spec)
    {
        var direction = ParseParameterDirection(spec.Direction);
        var paramName = spec.Name.StartsWith('@') ? spec.Name : $"@{spec.Name}";

        var parameter = direction switch
        {
            ParameterDirection.Output or ParameterDirection.InputOutput =>
                new SqlParameter(paramName, SqlDbType.NVarChar, 4000) { Direction = direction },
            ParameterDirection.ReturnValue =>
                new SqlParameter(paramName, SqlDbType.Int) { Direction = ParameterDirection.ReturnValue },
            _ => new SqlParameter(paramName, SqlDbType.NVarChar, 4000) { Direction = ParameterDirection.Input },
        };

        if (direction is ParameterDirection.Input or ParameterDirection.InputOutput)
            parameter.Value = string.IsNullOrEmpty(spec.Value) ? DBNull.Value : spec.Value;

        return parameter;
    }

    public static StepResult FailSql(
        string stepType,
        string connectionLabel,
        long durationMs,
        string message) =>
        StepResult.Fail($"{stepType}: {message}");

    public static bool IsSqlTimeout(SqlException ex) => ex.Number is -2 or 1222;

    public static string GetString(IReadOnlyDictionary<string, object> config, string key, string fallback = "")
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

    public static int GetInt(IReadOnlyDictionary<string, object> config, string key, int defaultValue)
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

    private static string GetJsonString(JsonElement item, string property, string fallback = "")
    {
        if (!item.TryGetProperty(property, out var prop))
            return fallback;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? fallback,
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True   => "true",
            JsonValueKind.False  => "false",
            _                    => prop.GetRawText(),
        };
    }

    private static string NormalizeSqlForValidation(string sql)
    {
        var text = sql.Trim();
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"--[^\r\n]*", " ", RegexOptions.CultureInvariant);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static bool IsSingleStatement(string normalized, out string error)
    {
        var withoutTrailingSemicolon = normalized.TrimEnd().TrimEnd(';').Trim();
        if (withoutTrailingSemicolon.Contains(';'))
        {
            error = "Multiple SQL statements are not allowed.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ContainsKeyword(string normalized, string keyword) =>
        Regex.IsMatch(normalized, $@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
