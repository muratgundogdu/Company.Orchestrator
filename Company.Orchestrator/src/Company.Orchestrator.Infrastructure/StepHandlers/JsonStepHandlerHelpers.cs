using System.Text.Json;
using Company.Orchestrator.Application.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class JsonStepHandlerHelpers
{
    public static readonly HashSet<string> SupportedOutputModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "value", "json", "table",
    };

    public static bool TryParseJson(string jsonText, out JToken root, out string error)
    {
        root  = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(jsonText))
        {
            error = "JSON content is empty.";
            return false;
        }

        try
        {
            root = JToken.Parse(jsonText);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryResolveMatches(
        JToken root,
        string path,
        out List<JToken> matches,
        out string error)
    {
        matches = [];
        error   = string.Empty;

        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            matches = [root];
            return true;
        }

        try
        {
            matches = EvaluatePath(root, normalizedPath);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        if (matches.Count == 0)
        {
            error = $"path '{normalizedPath}' matched no values.";
            return false;
        }

        return true;
    }

    public static StepResult BuildResult(
        string stepType,
        string outputVar,
        string path,
        string outputMode,
        IReadOnlyList<JToken> matches)
    {
        return outputMode switch
        {
            "value" => BuildValueResult(stepType, outputVar, matches, path),
            "json"  => BuildJsonResult(outputVar, matches),
            "table" => BuildTableResult(stepType, outputVar, matches, path),
            _       => StepResult.Fail($"{stepType}: unsupported output mode."),
        };
    }

    public static StepResult BuildValueResult(
        string stepType,
        string outputVar,
        IReadOnlyList<JToken> matches,
        string path)
    {
        if (matches.Count > 1)
        {
            return StepResult.Fail(
                $"{stepType}: path '{NormalizePath(path)}' matched {matches.Count} values; " +
                "use outputMode 'json' or 'table' for multiple results.");
        }

        var value = TokenToScalarString(matches[0]);
        return StepResult.Ok(
            output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [outputVar] = value,
            },
            outputData: $"Extracted value into '{outputVar}': {Truncate(value, 120)}");
    }

    public static StepResult BuildJsonResult(string outputVar, IReadOnlyList<JToken> matches)
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

    public static StepResult BuildTableResult(
        string stepType,
        string outputVar,
        IReadOnlyList<JToken> matches,
        string path)
    {
        var normalizedPath = NormalizePath(path);
        var rows           = new List<Dictionary<string, string>>();

        foreach (var match in matches)
        {
            if (match is JArray array)
            {
                foreach (var item in array)
                {
                    if (item is not JObject obj)
                    {
                        return StepResult.Fail(
                            $"{stepType}: table mode requires an array of objects at path '{normalizedPath}'.");
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
                    $"{stepType}: table mode requires an array of objects at path '{normalizedPath}'.");
            }
        }

        if (rows.Count == 0)
        {
            return StepResult.Fail(
                $"{stepType}: table mode found no object rows at path '{normalizedPath}'.");
        }

        var columns = BuildColumnNames(rows);
        return BuildTableOutput(outputVar, columns, rows);
    }

    public static StepResult BuildTableOutput(
        string outputVar,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var json        = JsonConvert.SerializeObject(rows);
        var columnsJson = JsonConvert.SerializeObject(columns);
        var firstJson   = rows.Count > 0 ? JsonConvert.SerializeObject(rows[0]) : "{}";

        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]              = json,
            [$"{outputVar}_count"]   = rows.Count,
            [$"{outputVar}_columns"] = columnsJson,
            [$"{outputVar}_first"]   = firstJson,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonConvert.SerializeObject(rows[i]);

        return StepResult.Ok(
            output: output,
            outputData:
                $"Parsed JSON table into '{outputVar}' — {rows.Count} row(s), {columns.Count} column(s).");
    }

    public static void AddArrayOutputs(
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

    public static int CountArrayItems(IReadOnlyList<JToken> matches)
    {
        if (matches.Count == 1 && matches[0] is JArray array)
            return array.Count;

        return matches.Count;
    }

    public static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return trimmed;

        return trimmed.StartsWith('$') ? trimmed : $"$.{trimmed.TrimStart('.')}";
    }

    public static string NormalizeVarName(string name) => name.Trim().Trim('{', '}');

    public static string GetString(Dictionary<string, object> config, string key, string fallback = "")
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

    public static bool GetBool(Dictionary<string, object> config, string key, bool defaultValue)
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

    public static string VariableToString(object? val)
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
            return root.SelectTokens(path).ToList();

        var token = root.SelectToken(path);
        return token is null ? [] : [token];
    }

    private static string TokenToScalarString(JToken token) =>
        token.Type switch
        {
            JTokenType.String  => token.Value<string>() ?? string.Empty,
            JTokenType.Integer => token.ToString(Formatting.None),
            JTokenType.Float   => token.ToString(Formatting.None),
            JTokenType.Boolean => token.ToString().ToLowerInvariant(),
            JTokenType.Null    => string.Empty,
            JTokenType.Date    => token.ToString(Formatting.None),
            _                  => token.ToString(Formatting.None),
        };

    private static string TokenToCellString(JToken token) =>
        token is JValue ? TokenToScalarString(token) : token.ToString(Formatting.None);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
