using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Joins two DataTable-style JSON array variables in memory (left or inner join).
/// </summary>
public sealed class DataTableJoinStepHandler : IStepHandler
{
    private const char KeySeparator = '\u001F';

    private readonly ILogger<DataTableJoinStepHandler> _logger;

    public string HandlerType => "datatable.join";

    public DataTableJoinStepHandler(ILogger<DataTableJoinStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var leftVariable  = NormalizeVarName(GetString(config, "leftVariable"));
        var rightVariable = NormalizeVarName(GetString(config, "rightVariable"));
        var outputVariable = GetString(config, "outputVariable");

        if (string.IsNullOrWhiteSpace(leftVariable))
            return Task.FromResult(StepResult.Fail("datatable.join: 'leftVariable' is required."));
        if (string.IsNullOrWhiteSpace(rightVariable))
            return Task.FromResult(StepResult.Fail("datatable.join: 'rightVariable' is required."));
        if (string.IsNullOrWhiteSpace(outputVariable))
            return Task.FromResult(StepResult.Fail("datatable.join: 'outputVariable' is required."));

        var joinType = GetString(config, "joinType", "left").Trim().ToLowerInvariant();
        if (joinType is not ("left" or "inner"))
        {
            return Task.FromResult(StepResult.Fail(
                "datatable.join: 'joinType' must be 'left' or 'inner'."));
        }

        var leftKeyColumns  = ParseColumnList(config, "leftKeyColumns");
        var rightKeyColumns = ParseColumnList(config, "rightKeyColumns");
        var mappings        = ParseColumnMappings(config);

        if (leftKeyColumns.Count == 0)
            return Task.FromResult(StepResult.Fail("datatable.join: 'leftKeyColumns' is required."));
        if (rightKeyColumns.Count == 0)
            return Task.FromResult(StepResult.Fail("datatable.join: 'rightKeyColumns' is required."));
        if (leftKeyColumns.Count != rightKeyColumns.Count)
        {
            return Task.FromResult(StepResult.Fail(
                "datatable.join: leftKeyColumns and rightKeyColumns must have the same number of entries."));
        }
        if (mappings.Count == 0)
        {
            return Task.FromResult(StepResult.Fail(
                "datatable.join: 'rightColumnsToAdd' must contain at least one column mapping."));
        }

        var notFoundValue = GetString(config, "notFoundValue");
        var ignoreCase    = GetBool(config, "ignoreCase", defaultValue: true);
        var trimValues    = GetBool(config, "trimValues", defaultValue: true);

        if (!context.Variables.TryGetValue(leftVariable, out var leftRaw))
        {
            return Task.FromResult(StepResult.Fail(
                $"datatable.join: left variable '{leftVariable}' not found in workflow context."));
        }

        if (!context.Variables.TryGetValue(rightVariable, out var rightRaw))
        {
            return Task.FromResult(StepResult.Fail(
                $"datatable.join: right variable '{rightVariable}' not found in workflow context."));
        }

        List<JsonElement> leftRows;
        List<JsonElement> rightRows;
        try
        {
            leftRows  = ParseSourceRows(leftRaw, leftVariable);
            rightRows = ParseSourceRows(rightRaw, rightVariable);
        }
        catch (Exception ex)
        {
            return Task.FromResult(StepResult.Fail($"datatable.join: {ex.Message}"));
        }

        try
        {
            ValidateKeyColumns(leftRows, leftKeyColumns, $"left variable '{leftVariable}'");
            ValidateKeyColumns(rightRows, rightKeyColumns, $"right variable '{rightVariable}'");
            ValidateMappingColumns(rightRows, mappings, rightVariable);
        }
        catch (Exception ex)
        {
            return Task.FromResult(StepResult.Fail($"datatable.join: {ex.Message}"));
        }

        var rightLookup = BuildRightLookup(rightRows, rightKeyColumns, ignoreCase, trimValues);

        var outputRows = new List<Dictionary<string, string>>();
        var matches    = 0;
        var misses     = 0;

        foreach (var leftRow in leftRows)
        {
            if (leftRow.ValueKind != JsonValueKind.Object)
                continue;

            var key = BuildCompositeKey(leftRow, leftKeyColumns, ignoreCase, trimValues);
            var hasMatch = rightLookup.TryGetValue(key, out var rightRow);

            if (hasMatch)
                matches++;
            else
                misses++;

            if (joinType == "inner" && !hasMatch)
                continue;

            var outputRow = RowToDictionary(leftRow, trimValues);

            foreach (var mapping in mappings)
            {
                var value = notFoundValue;
                if (hasMatch && TryGetProperty(rightRow, mapping.SourceColumn, out var cell))
                    value = CellToString(cell, trimValues);

                outputRow[mapping.TargetColumn] = value;
            }

            outputRows.Add(outputRow);
        }

        var columnNames = BuildColumnNames(outputRows, mappings);

        _logger.LogInformation(
            "datatable.join: left rows={LeftCount}, right rows={RightCount}, joinType={JoinType}, " +
            "left keys=[{LeftKeys}], right keys=[{RightKeys}], matches={Matches}, misses={Misses}, output rows={OutputCount}",
            leftRows.Count,
            rightRows.Count,
            joinType,
            string.Join(", ", leftKeyColumns),
            string.Join(", ", rightKeyColumns),
            matches,
            misses,
            outputRows.Count);

        return Task.FromResult(BuildResult(outputVariable, columnNames, outputRows));
    }

    private static Dictionary<string, JsonElement> BuildRightLookup(
        List<JsonElement> rightRows,
        IReadOnlyList<string> keyColumns,
        bool ignoreCase,
        bool trimValues)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var lookup   = new Dictionary<string, JsonElement>(comparer);

        foreach (var row in rightRows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            var key = BuildCompositeKey(row, keyColumns, ignoreCase, trimValues);
            lookup.TryAdd(key, row);
        }

        return lookup;
    }

    private static string BuildCompositeKey(
        JsonElement row,
        IReadOnlyList<string> columns,
        bool ignoreCase,
        bool trimValues)
    {
        var parts = new string[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var value = TryGetProperty(row, columns[i], out var cell)
                ? CellToString(cell, trimValues)
                : string.Empty;

            if (ignoreCase)
                value = value.ToUpperInvariant();

            parts[i] = value;
        }

        return string.Join(KeySeparator, parts);
    }

    private static Dictionary<string, string> RowToDictionary(JsonElement row, bool trimValues)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in row.EnumerateObject())
            dict[prop.Name] = CellToString(prop.Value, trimValues);
        return dict;
    }

    private static List<string> BuildColumnNames(
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<ColumnMapping> mappings)
    {
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        if (rows.Count > 0)
        {
            foreach (var key in rows[0].Keys)
            {
                if (seen.Add(key))
                    result.Add(key);
            }
        }

        foreach (var row in rows.Skip(1))
        {
            foreach (var key in row.Keys)
            {
                if (seen.Add(key))
                    result.Add(key);
            }
        }

        foreach (var mapping in mappings)
        {
            if (seen.Add(mapping.TargetColumn))
                result.Add(mapping.TargetColumn);
        }

        return result;
    }

    private static StepResult BuildResult(
        string outputVar,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var json        = JsonSerializer.Serialize(rows);
        var columnsJson = JsonSerializer.Serialize(columns);
        var firstJson   = rows.Count > 0 ? JsonSerializer.Serialize(rows[0]) : "{}";

        var output = new Dictionary<string, object>
        {
            [outputVar]                  = json,
            [$"{outputVar}_count"]         = rows.Count,
            [$"{outputVar}_columns"]       = columnsJson,
            [$"{outputVar}_first"]         = firstJson,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonSerializer.Serialize(rows[i]);

        return StepResult.Ok(
            output: output,
            outputData:
                $"Joined DataTables into '{outputVar}' — {rows.Count} row(s), {columns.Count} column(s).");
    }

    private static void ValidateKeyColumns(
        List<JsonElement> rows,
        IReadOnlyList<string> columns,
        string tableLabel)
    {
        foreach (var col in columns)
        {
            var found = rows.Any(r =>
                r.ValueKind == JsonValueKind.Object &&
                TryGetProperty(r, col, out _));

            if (!found)
                throw new InvalidOperationException($"Key column '{col}' not found in {tableLabel}.");
        }
    }

    private static void ValidateMappingColumns(
        List<JsonElement> rightRows,
        IReadOnlyList<ColumnMapping> mappings,
        string rightVariable)
    {
        foreach (var mapping in mappings)
        {
            var found = rightRows.Any(r =>
                r.ValueKind == JsonValueKind.Object &&
                TryGetProperty(r, mapping.SourceColumn, out _));

            if (!found)
            {
                throw new InvalidOperationException(
                    $"Source column '{mapping.SourceColumn}' not found in right variable '{rightVariable}'.");
            }
        }
    }

    private static List<JsonElement> ParseSourceRows(object raw, string variableName)
    {
        var json = VariableToString(raw).Trim();
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException($"Variable '{variableName}' is empty.");

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Variable '{variableName}' is not a JSON array.");

        return doc.RootElement.EnumerateArray().ToList();
    }

    private static List<string> ParseColumnList(Dictionary<string, object> config, string key)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return [];

        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                return je.EnumerateArray()
                    .Select(e => e.GetString()?.Trim() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            if (je.ValueKind == JsonValueKind.String)
                return ParseDelimitedColumns(je.GetString() ?? "");
        }

        if (raw is IEnumerable<object> list && raw is not string)
        {
            return list
                .Select(item => item?.ToString()?.Trim() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        return ParseDelimitedColumns(raw.ToString() ?? "");
    }

    private static List<string> ParseDelimitedColumns(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

    private static List<ColumnMapping> ParseColumnMappings(Dictionary<string, object> config)
    {
        if (!config.TryGetValue("rightColumnsToAdd", out var raw) || raw is null)
            return [];

        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            return je.EnumerateArray()
                .Select(ParseMappingElement)
                .Where(m => m is not null)
                .Cast<ColumnMapping>()
                .ToList();
        }

        if (raw is IEnumerable<object> list && raw is not string)
        {
            var result = new List<ColumnMapping>();
            foreach (var item in list)
            {
                if (item is JsonElement el)
                {
                    var mapping = ParseMappingElement(el);
                    if (mapping is not null) result.Add(mapping);
                }
                else if (item is Dictionary<string, object> dict)
                {
                    var mapping = ParseMappingDictionary(dict);
                    if (mapping is not null) result.Add(mapping);
                }
            }

            return result;
        }

        return [];
    }

    private static ColumnMapping? ParseMappingElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var source = GetJsonString(element, "sourceColumn");
        var target = GetJsonString(element, "targetColumn");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return null;

        return new ColumnMapping(source.Trim(), target.Trim());
    }

    private static ColumnMapping? ParseMappingDictionary(Dictionary<string, object> dict)
    {
        var source = dict.GetValueOrDefault("sourceColumn")?.ToString()?.Trim() ?? "";
        var target = dict.GetValueOrDefault("targetColumn")?.ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return null;

        return new ColumnMapping(source, target);
    }

    private static string GetJsonString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, property, StringComparison.OrdinalIgnoreCase))
                    return prop.Value.GetString() ?? "";
            }

            return "";
        }

        return value.GetString() ?? "";
    }

    private static bool TryGetProperty(JsonElement row, string column, out JsonElement value)
    {
        if (row.TryGetProperty(column, out value))
            return true;

        foreach (var prop in row.EnumerateObject())
        {
            if (string.Equals(prop.Name, column, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string CellToString(JsonElement element, bool trimValues)
    {
        var text = element.ValueKind switch
        {
            JsonValueKind.String  => element.GetString() ?? "",
            JsonValueKind.Number  => element.GetRawText(),
            JsonValueKind.True    => "true",
            JsonValueKind.False   => "false",
            JsonValueKind.Null    => "",
            _                     => element.GetRawText()
        };

        return trimValues ? text.Trim() : text;
    }

    private static string NormalizeVarName(string name) => name.Trim().Trim('{', '}');

    private static string GetString(Dictionary<string, object> config, string key, string fallback = "")
    {
        if (!config.TryGetValue(key, out var raw)) return fallback;
        if (raw is JsonElement el) return el.GetString() ?? fallback;
        return raw?.ToString() ?? fallback;
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
                _                    => defaultValue
            };
        }

        if (raw is bool flag) return flag;
        return bool.TryParse(raw.ToString(), out var boolVal) ? boolVal : defaultValue;
    }

    private static string VariableToString(object? val)
    {
        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? "",
                JsonValueKind.Array   => je.GetRawText(),
                JsonValueKind.Object  => je.GetRawText(),
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => "",
                _                     => je.GetRawText()
            };
        }

        return val?.ToString() ?? "";
    }

    private sealed record ColumnMapping(string SourceColumn, string TargetColumn);
}
