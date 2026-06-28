using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Parses JSON from a workflow variable and extracts values using JSON Path.
/// </summary>
public sealed class JsonParseStepHandler : IStepHandler
{
    private static readonly HashSet<string> SupportedOutputModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "value", "json", "table",
    };

    private readonly ILogger<JsonParseStepHandler> _logger;

    public string HandlerType => "json.parse";

    public JsonParseStepHandler(ILogger<JsonParseStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var sourceVariable = NormalizeVarName(GetString(config, "sourceVariable"));
        var path           = NormalizePath(GetString(config, "path"));
        var outputVariable = GetString(config, "outputVariable");
        var outputMode     = GetString(config, "outputMode", "value").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(sourceVariable))
            return Task.FromResult(StepResult.Fail("json.parse: 'sourceVariable' is required."));
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(StepResult.Fail("json.parse: 'path' is required."));
        if (string.IsNullOrWhiteSpace(outputVariable))
            return Task.FromResult(StepResult.Fail("json.parse: 'outputVariable' is required."));
        if (!SupportedOutputModes.Contains(outputMode))
        {
            return Task.FromResult(StepResult.Fail(
                "json.parse: 'outputMode' must be 'value', 'json', or 'table'."));
        }

        if (!context.Variables.TryGetValue(sourceVariable, out var sourceRaw))
        {
            return Task.FromResult(StepResult.Fail(
                $"json.parse: source variable '{sourceVariable}' not found in workflow context."));
        }

        var jsonText = VariableToString(sourceRaw).Trim();
        if (string.IsNullOrEmpty(jsonText))
        {
            return Task.FromResult(StepResult.Fail(
                $"json.parse: source variable '{sourceVariable}' is empty."));
        }

        JToken root;
        try
        {
            root = JToken.Parse(jsonText);
        }
        catch (Exception ex)
        {
            return Task.FromResult(StepResult.Fail(
                $"json.parse: invalid JSON in '{sourceVariable}': {ex.Message}"));
        }

        List<JToken> matches;
        try
        {
            matches = EvaluatePath(root, path);
        }
        catch (Exception ex)
        {
            return Task.FromResult(StepResult.Fail($"json.parse: invalid path '{path}': {ex.Message}"));
        }

        if (matches.Count == 0)
        {
            return Task.FromResult(StepResult.Fail(
                $"json.parse: path '{path}' matched no values in '{sourceVariable}'."));
        }

        _logger.LogInformation(
            "json.parse: sourceVariable={SourceVariable}, path={Path}, outputMode={OutputMode}, " +
            "matchCount={MatchCount}, outputVariable={OutputVariable}",
            sourceVariable,
            path,
            outputMode,
            matches.Count,
            outputVariable);

        return outputMode switch
        {
            "value" => Task.FromResult(BuildValueResult(outputVariable, matches, path)),
            "json"  => Task.FromResult(BuildJsonResult(outputVariable, matches)),
            "table" => Task.FromResult(BuildTableResult(outputVariable, matches, path)),
            _       => Task.FromResult(StepResult.Fail("json.parse: unsupported output mode.")),
        };
    }

    private static StepResult BuildValueResult(string outputVar, IReadOnlyList<JToken> matches, string path)
    {
        if (matches.Count > 1)
        {
            return StepResult.Fail(
                $"json.parse: path '{path}' matched {matches.Count} values; " +
                "use outputMode 'json' or 'table' for multiple results.");
        }

        var value = TokenToScalarString(matches[0]);
        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar] = value,
        };

        return StepResult.Ok(
            output: output,
            outputData: $"Extracted value into '{outputVar}': {Truncate(value, 120)}");
    }

    private static StepResult BuildJsonResult(string outputVar, IReadOnlyList<JToken> matches)
    {
        var selected = matches.Count == 1 ? matches[0] : new JArray(matches);
        var json     = selected.ToString(Formatting.None);

        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar] = json,
        };

        if (selected is JArray array)
            AddArrayOutputs(output, outputVar, array);

        return StepResult.Ok(
            output: output,
            outputData: $"Extracted JSON into '{outputVar}' ({json.Length} character(s)).");
    }

    private static StepResult BuildTableResult(string outputVar, IReadOnlyList<JToken> matches, string path)
    {
        var rows = new List<Dictionary<string, string>>();

        foreach (var match in matches)
        {
            if (match is JArray array)
            {
                foreach (var item in array)
                {
                    if (item is not JObject obj)
                    {
                        return StepResult.Fail(
                            $"json.parse: table mode requires an array of objects at path '{path}'.");
                    }

                    rows.Add(JObjectToRow(obj));
                }
            }
            else if (match is JObject obj)
            {
                rows.Add(JObjectToRow(obj));
            }
            else
            {
                return StepResult.Fail(
                    $"json.parse: table mode requires an array of objects at path '{path}'.");
            }
        }

        if (rows.Count == 0)
        {
            return StepResult.Fail(
                $"json.parse: table mode found no object rows at path '{path}'.");
        }

        var columns = BuildColumnNames(rows);
        return BuildTableOutput(outputVar, columns, rows);
    }

    private static StepResult BuildTableOutput(
        string outputVar,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var json        = JsonConvert.SerializeObject(rows);
        var columnsJson = JsonConvert.SerializeObject(columns);
        var firstJson   = rows.Count > 0 ? JsonConvert.SerializeObject(rows[0]) : "{}";

        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]                = json,
            [$"{outputVar}_count"]     = rows.Count,
            [$"{outputVar}_columns"]   = columnsJson,
            [$"{outputVar}_first"]     = firstJson,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonConvert.SerializeObject(rows[i]);

        return StepResult.Ok(
            output: output,
            outputData:
                $"Parsed JSON table into '{outputVar}' — {rows.Count} row(s), {columns.Count} column(s).");
    }

    private static void AddArrayOutputs(
        Dictionary<string, object> output,
        string outputVar,
        JArray array)
    {
        output[$"{outputVar}_count"] = array.Count;
        output[$"{outputVar}_first"] = array.Count > 0
            ? array[0]!.ToString(Formatting.None)
            : "{}";

        for (var i = 0; i < Math.Min(array.Count, 10); i++)
            output[$"{outputVar}_{i}"] = array[i]!.ToString(Formatting.None);
    }

    private static List<string> BuildColumnNames(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
            {
                if (seen.Add(key))
                    result.Add(key);
            }
        }

        return result;
    }

    private static Dictionary<string, string> JObjectToRow(JObject obj)
    {
        var row = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in obj.Properties())
            row[prop.Name] = TokenToCellString(prop.Value);
        return row;
    }

    private static List<JToken> EvaluatePath(JToken root, string path)
    {
        if (path.Contains("[*]", StringComparison.Ordinal))
        {
            return root.SelectTokens(path).ToList();
        }

        var token = root.SelectToken(path);
        return token is null ? [] : [token];
    }

    private static string TokenToScalarString(JToken token)
    {
        return token.Type switch
        {
            JTokenType.String  => token.Value<string>() ?? string.Empty,
            JTokenType.Integer => token.ToString(Formatting.None),
            JTokenType.Float   => token.ToString(Formatting.None),
            JTokenType.Boolean => token.ToString().ToLowerInvariant(),
            JTokenType.Null    => string.Empty,
            JTokenType.Date    => token.ToString(Formatting.None),
            _                  => token.ToString(Formatting.None),
        };
    }

    private static string TokenToCellString(JToken token)
    {
        if (token is JValue)
            return TokenToScalarString(token);

        return token.ToString(Formatting.None);
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return trimmed;

        return trimmed.StartsWith('$') ? trimmed : $"$.{trimmed.TrimStart('.')}";
    }

    private static string NormalizeVarName(string name) => name.Trim().Trim('{', '}');

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";

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

    private static string VariableToString(object? val)
    {
        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? string.Empty,
                JsonValueKind.Array   => je.GetRawText(),
                JsonValueKind.Object  => je.GetRawText(),
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => string.Empty,
                _                     => je.GetRawText(),
            };
        }

        return val?.ToString() ?? string.Empty;
    }
}
