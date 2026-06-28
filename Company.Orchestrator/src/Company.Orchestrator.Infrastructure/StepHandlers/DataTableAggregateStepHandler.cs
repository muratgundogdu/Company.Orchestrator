using System.Globalization;
using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Aggregates values from a DataTable-style JSON array variable.
/// </summary>
public sealed class DataTableAggregateStepHandler : IStepHandler
{
    private readonly ILogger<DataTableAggregateStepHandler> _logger;

    private static readonly HashSet<string> ValidOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "count", "countNonEmpty", "countDistinct", "sum", "average", "min", "max"
    };

    public string HandlerType => "datatable.aggregate";

    public DataTableAggregateStepHandler(ILogger<DataTableAggregateStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var sourceVariable = GetString(config, "sourceVariable");
        if (string.IsNullOrWhiteSpace(sourceVariable))
            return Task.FromResult(StepResult.Fail("datatable.aggregate: 'sourceVariable' is required."));

        var operation = GetString(config, "operation").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(operation))
            return Task.FromResult(StepResult.Fail("datatable.aggregate: 'operation' is required."));
        if (!ValidOperations.Contains(operation))
        {
            return Task.FromResult(StepResult.Fail(
                "datatable.aggregate: 'operation' must be count, countNonEmpty, countDistinct, sum, average, min, or max."));
        }

        var outputVariable = GetString(config, "outputVariable");
        if (string.IsNullOrWhiteSpace(outputVariable))
            return Task.FromResult(StepResult.Fail("datatable.aggregate: 'outputVariable' is required."));

        var column      = GetString(config, "column");
        var ignoreEmpty = GetBool(config, "ignoreEmpty", defaultValue: true);

        if (operation is not "count" && string.IsNullOrWhiteSpace(column))
        {
            return Task.FromResult(StepResult.Fail(
                $"datatable.aggregate: 'column' is required for operation '{operation}'."));
        }

        var sourceKey = sourceVariable.Trim().Trim('{', '}');
        if (!context.Variables.TryGetValue(sourceKey, out var sourceRaw))
        {
            return Task.FromResult(StepResult.Fail(
                $"datatable.aggregate: source variable '{sourceKey}' not found in workflow context."));
        }

        List<JsonElement> rows;
        try
        {
            rows = ParseSourceRows(sourceRaw);
        }
        catch (Exception ex)
        {
            return Task.FromResult(StepResult.Fail(
                $"datatable.aggregate: failed to parse source variable '{sourceKey}': {ex.Message}"));
        }

        if (rows.Count == 0)
        {
            return Task.FromResult(StepResult.Fail(
                $"datatable.aggregate: source variable '{sourceKey}' contains no rows."));
        }

        if (operation is not "count" && !string.IsNullOrWhiteSpace(column))
        {
            var hasColumn = rows.Any(r =>
                r.ValueKind == JsonValueKind.Object &&
                TryGetProperty(r, column, out _));

            if (!hasColumn)
            {
                return Task.FromResult(StepResult.Fail(
                    $"datatable.aggregate: column '{column}' not found in source variable '{sourceKey}'."));
            }
        }

        string result;
        try
        {
            result = operation switch
            {
                "count"          => ComputeCount(rows, column, ignoreEmpty),
                "countnonempty"  => ComputeCountNonEmpty(rows, column!, ignoreEmpty),
                "countdistinct"  => ComputeCountDistinct(rows, column!, ignoreEmpty),
                "sum"            => ComputeSum(rows, column!, ignoreEmpty),
                "average"        => ComputeAverage(rows, column!, ignoreEmpty),
                "min"            => ComputeMin(rows, column!, ignoreEmpty),
                "max"            => ComputeMax(rows, column!, ignoreEmpty),
                _                => throw new InvalidOperationException($"Unsupported operation '{operation}'.")
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(StepResult.Fail($"datatable.aggregate: {ex.Message}"));
        }

        var sourceCount = rows.Count;
        var columnUsed  = operation == "count" && string.IsNullOrWhiteSpace(column) ? "" : column;

        _logger.LogInformation(
            "datatable.aggregate: sourceVariable='{SourceVar}', operation='{Operation}', column='{Column}', source row count={SourceCount}, result={Result}",
            sourceKey,
            operation,
            string.IsNullOrEmpty(columnUsed) ? "(none)" : columnUsed,
            sourceCount,
            result);

        var output = new Dictionary<string, object>
        {
            [outputVariable]                    = result,
            [$"{outputVariable}_operation"]     = operation,
            [$"{outputVariable}_column"]        = columnUsed,
            [$"{outputVariable}_sourceCount"]   = sourceCount,
        };

        return Task.FromResult(StepResult.Ok(
            output: output,
            outputData:
                $"Aggregate {operation} on '{sourceKey}' column '{columnUsed}' = {result} ({sourceCount} row(s))"));
    }

    private static string ComputeCount(List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        if (string.IsNullOrWhiteSpace(column))
            return rows.Count.ToString(CultureInfo.InvariantCulture);

        var count = 0;
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetProperty(row, column, out var prop))
                continue;

            var value = CellToString(prop);
            if (ignoreEmpty && string.IsNullOrWhiteSpace(value))
                continue;

            count++;
        }

        return count.ToString(CultureInfo.InvariantCulture);
    }

    private static string ComputeCountNonEmpty(List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var count = 0;
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetProperty(row, column, out var prop))
            {
                if (!ignoreEmpty) count++;
                continue;
            }

            var value = CellToString(prop);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!ignoreEmpty) count++;
                continue;
            }

            count++;
        }

        return count.ToString(CultureInfo.InvariantCulture);
    }

    private static string ComputeCountDistinct(List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetProperty(row, column, out var prop))
                continue;

            var value = CellToString(prop).Trim();
            if (ignoreEmpty && string.IsNullOrEmpty(value))
                continue;

            seen.Add(value);
        }

        return seen.Count.ToString(CultureInfo.InvariantCulture);
    }

    private static string ComputeSum(List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var values = ExtractNumericValues(rows, column, ignoreEmpty);
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                $"No numeric values found in column '{column}' for sum operation.");
        }

        var total = values.Aggregate(0m, (acc, v) => acc + v);
        return FormatDecimal(total);
    }

    private static string ComputeAverage(List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var values = ExtractNumericValues(rows, column, ignoreEmpty);
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                $"No numeric values found in column '{column}' for average operation.");
        }

        var avg = values.Aggregate(0m, (acc, v) => acc + v) / values.Count;
        return FormatDecimal(avg);
    }

    private static string ComputeMin(List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var values = ExtractComparableValues(rows, column, ignoreEmpty);
        if (values.Count == 0)
            throw new InvalidOperationException($"No values found in column '{column}' for min operation.");

        if (values.All(v => v.IsNumeric))
        {
            var min = values.Min(v => v.Numeric!.Value);
            return FormatDecimal(min);
        }

        return values.Select(v => v.Text).Min(StringComparer.Ordinal) ?? string.Empty;
    }

    private static string ComputeMax(List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var values = ExtractComparableValues(rows, column, ignoreEmpty);
        if (values.Count == 0)
            throw new InvalidOperationException($"No values found in column '{column}' for max operation.");

        if (values.All(v => v.IsNumeric))
        {
            var max = values.Max(v => v.Numeric!.Value);
            return FormatDecimal(max);
        }

        return values.Select(v => v.Text).Max(StringComparer.Ordinal) ?? string.Empty;
    }

    private static List<decimal> ExtractNumericValues(
        List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var values = new List<decimal>();
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetProperty(row, column, out var prop))
                continue;

            var text = CellToString(prop);
            if (ignoreEmpty && string.IsNullOrWhiteSpace(text))
                continue;

            if (TryParseDecimal(text, out var number))
                values.Add(number);
        }

        return values;
    }

    private static List<ComparableValue> ExtractComparableValues(
        List<JsonElement> rows, string column, bool ignoreEmpty)
    {
        var values = new List<ComparableValue>();
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            if (!TryGetProperty(row, column, out var prop))
                continue;

            var text = CellToString(prop);
            if (ignoreEmpty && string.IsNullOrWhiteSpace(text))
                continue;

            values.Add(new ComparableValue(
                text,
                TryParseDecimal(text, out var number) ? number : null));
        }

        return values;
    }

    private static List<JsonElement> ParseSourceRows(object sourceRaw)
    {
        var json = VariableToString(sourceRaw).Trim();
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException("Source variable is empty.");

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Source variable is not a JSON array.");

        return doc.RootElement.EnumerateArray().ToList();
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

    private static string CellToString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String  => element.GetString() ?? "",
            JsonValueKind.Number  => element.GetRawText(),
            JsonValueKind.True    => "true",
            JsonValueKind.False   => "false",
            JsonValueKind.Null    => "",
            _                     => element.GetRawText()
        };

    private static bool TryParseDecimal(string text, out decimal value)
    {
        text = text.Trim();
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

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

    private sealed record ComparableValue(string Text, decimal? Numeric)
    {
        public bool IsNumeric => Numeric.HasValue;
    }
}
