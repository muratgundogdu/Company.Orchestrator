using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class CsvStepHandlerHelpers
{
    public static string ResolveDelimiter(Dictionary<string, object> config)
    {
        var custom = GetString(config, "customDelimiter");
        var direct = UnescapeDelimiter(GetString(config, "delimiter"));

        if (!string.IsNullOrEmpty(direct))
            return direct;

        var mode = GetString(config, "delimiterMode").ToLowerInvariant();
        return mode switch
        {
            "semicolon" => ";",
            "tab"       => "\t",
            "pipe"      => "|",
            "custom"    => string.IsNullOrEmpty(custom)
                ? throw new InvalidOperationException("csv: custom delimiter is required when delimiter mode is 'custom'.")
                : custom,
            _           => ",",
        };
    }

    public static void ValidateDelimiter(string delimiter, string stepType)
    {
        if (string.IsNullOrEmpty(delimiter))
            throw new InvalidOperationException($"{stepType}: 'delimiter' is required.");

        if (delimiter.Length != 1)
        {
            throw new InvalidOperationException(
                $"{stepType}: delimiter must be a single character (got '{delimiter}' with length {delimiter.Length}).");
        }
    }

    public static Encoding ResolveEncoding(string encodingName, string stepType)
    {
        var name = encodingName.Trim();
        if (string.IsNullOrEmpty(name))
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        try
        {
            return name.ToUpperInvariant() switch
            {
                "UTF-8" or "UTF8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                "UTF-8-BOM" or "UTF8-BOM" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                _ => Encoding.GetEncoding(name),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new InvalidOperationException($"{stepType}: unsupported encoding '{encodingName}'.", ex);
        }
    }

    public static CsvConfiguration BuildCsvConfiguration(string delimiter, bool trimValues) =>
        new(CultureInfo.InvariantCulture)
        {
            Delimiter           = delimiter,
            TrimOptions         = trimValues ? TrimOptions.Trim : TrimOptions.None,
            BadDataFound        = args => throw new InvalidOperationException(
                $"csv: invalid CSV data near row {args.Context.Parser?.Row}: {args.Field}"),
            MissingFieldFound   = null,
            IgnoreBlankLines    = true,
        };

    public static List<Dictionary<string, string>> ParseDataTableRows(
        object raw,
        out List<string> firstRowColumnOrder)
    {
        firstRowColumnOrder = [];

        var json = VariableToString(raw).Trim();
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException("source variable is empty.");

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("source variable is not a JSON array.");

        var rows    = new List<Dictionary<string, string>>();
        var isFirst = true;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("source variable is not a JSON array of objects.");

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in element.EnumerateObject())
            {
                if (isFirst)
                    firstRowColumnOrder.Add(prop.Name);

                row[prop.Name] = JsonElementToString(prop.Value);
            }

            rows.Add(row);
            isFirst = false;
        }

        return rows;
    }

    public static List<string> BuildColumns(
        IReadOnlyList<string> firstRowColumnOrder,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var columns = new List<string>(firstRowColumnOrder);
        var seen    = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
            {
                if (seen.Add(key))
                    columns.Add(key);
            }
        }

        return columns;
    }

    public static string[] BuildHeaderNames(IReadOnlyList<string> rawHeaders)
    {
        var seen   = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new string[rawHeaders.Count];

        for (var i = 0; i < rawHeaders.Count; i++)
        {
            var name = string.IsNullOrWhiteSpace(rawHeaders[i]) ? $"Column{i + 1}" : rawHeaders[i]!.Trim();
            if (seen.TryGetValue(name, out var count))
            {
                count++;
                seen[name] = count;
                name = $"{name}_{count}";
            }
            else
            {
                seen[name] = 1;
            }

            result[i] = name;
        }

        return result;
    }

    public static bool IsEmptyRow(IReadOnlyDictionary<string, string> row) =>
        row.Values.All(string.IsNullOrWhiteSpace);

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
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => string.Empty,
                JsonValueKind.Array   => je.GetRawText(),
                JsonValueKind.Object  => je.GetRawText(),
                _                     => je.GetRawText(),
            };
        }

        return val?.ToString() ?? string.Empty;
    }

    private static string JsonElementToString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String  => element.GetString() ?? string.Empty,
            JsonValueKind.Number  => element.GetRawText(),
            JsonValueKind.True    => "true",
            JsonValueKind.False   => "false",
            JsonValueKind.Null    => string.Empty,
            _                     => element.GetRawText(),
        };

    private static string UnescapeDelimiter(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value switch
        {
            "\\t" => "\t",
            "tab" => "\t",
            _     => value,
        };
    }
}
