using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Iterates over rows in a DataTable-style JSON array (e.g. from excel.read-range or mail.extract-table).
/// Routes to <c>LoopStepId</c> once per row; after the last row routes to <c>CompletedStepId</c>.
/// </summary>
public sealed class ForEachRowStepHandler : IStepHandler
{
    private readonly ILogger<ForEachRowStepHandler> _logger;

    public string HandlerType => "foreach.row";

    public ForEachRowStepHandler(ILogger<ForEachRowStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stepDef  = context.StepDefinition;
        var config   = stepDef.Config;
        var stepId   = stepDef.Id;

        var collectionVar = GetString(config, "collectionVariable");
        var rowVarName    = GetString(config, "rowVariable",   "currentRow");
        var indexVarName  = GetString(config, "indexVariable", "currentIndex");

        var loopStepId      = stepDef.LoopStepId;
        var completedStepId = stepDef.CompletedStepId;

        var itemsKey = $"__foreachrow_{stepId}_items__";
        var indexKey = $"__foreachrow_{stepId}_index__";

        List<string> rows;
        if (!context.Variables.ContainsKey(itemsKey))
        {
            var collectionRaw = ResolveCollectionVariable(context, collectionVar);
            rows = ResolveRows(collectionRaw);

            context.Variables[itemsKey] = JsonSerializer.Serialize(rows);
            context.Variables[indexKey] = 0;

            _logger.LogInformation(
                "ForEachRow {StepId}: collectionVariable='{CollectionVar}', row count={Count}",
                stepId, collectionVar, rows.Count);
        }
        else
        {
            var raw = context.Variables[itemsKey]?.ToString() ?? "[]";
            rows = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        }

        var index = Convert.ToInt32(context.Variables.GetValueOrDefault(indexKey, 0));

        ClearRowFieldVariables(context, rowVarName);

        if (index >= rows.Count)
        {
            context.Variables.Remove(itemsKey);
            context.Variables.Remove(indexKey);
            context.Variables.Remove(rowVarName);
            context.Variables.Remove(indexVarName);

            _logger.LogInformation(
                "ForEachRow {StepId}: completed — processed {Count} row(s) → completedStepId {CompletedStepId}",
                stepId, rows.Count, completedStepId ?? "(none)");

            return Task.FromResult(StepResult.Ok(
                output: new Dictionary<string, object>
                {
                    ["nextStepId"]       = completedStepId ?? "",
                    ["foreachCompleted"] = true,
                    ["foreachItemCount"] = rows.Count,
                },
                outputData: $"ForEachRow complete — {rows.Count} row(s) processed. → {completedStepId ?? "end"}"));
        }

        var currentRow = rows[index];
        var preview    = currentRow.Length > 120 ? currentRow[..120] + "…" : currentRow;

        _logger.LogInformation(
            "ForEachRow {StepId}: iteration {Index}/{Total}, row preview={Preview} → loopStepId {LoopStepId}",
            stepId, index, rows.Count, preview, loopStepId ?? "(none)");

        context.Variables[rowVarName]    = currentRow;
        context.Variables[indexVarName]  = index;
        context.Variables[indexKey]      = index + 1;
        ExposeRowFieldVariables(context, rowVarName, currentRow);

        return Task.FromResult(StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["nextStepId"]       = loopStepId ?? "",
                [rowVarName]         = currentRow,
                [indexVarName]       = index,
                ["foreachCompleted"] = false,
            },
            outputData: $"ForEachRow [{index + 1}/{rows.Count}] → {loopStepId ?? "end"}"));
    }

    private static string ResolveCollectionVariable(WorkflowContext context, string collectionExpr)
    {
        var expr = collectionExpr.Trim();
        if (string.IsNullOrEmpty(expr))
            return string.Empty;

        if (expr.StartsWith("{{", StringComparison.Ordinal) && expr.EndsWith("}}", StringComparison.Ordinal))
            return context.Interpolate(expr);

        if (context.Variables.TryGetValue(expr, out var raw))
            return VariableToString(raw);

        return context.Interpolate($"{{{{{expr}}}}}");
    }

    private static List<string> ResolveRows(string resolved)
    {
        if (string.IsNullOrWhiteSpace(resolved))
            return [];

        var trimmed = resolved.Trim();
        if (!trimmed.StartsWith('['))
            return [trimmed];

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [trimmed];

            return doc.RootElement.EnumerateArray()
                .Select(ElementToRowJson)
                .ToList();
        }
        catch
        {
            return [trimmed];
        }
    }

    private static string ElementToRowJson(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            _                    => element.GetRawText()
        };

    private static void ExposeRowFieldVariables(WorkflowContext context, string rowVarName, string rowJson)
    {
        if (string.IsNullOrWhiteSpace(rowJson) || !rowJson.TrimStart().StartsWith('{'))
            return;

        try
        {
            using var doc = JsonDocument.Parse(rowJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var fieldKey = $"{rowVarName}.{prop.Name}";
                context.Variables[fieldKey] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Null   => "",
                    _                    => prop.Value.GetRawText()
                };
            }
        }
        catch
        {
            // Non-object row — field variables are not exposed.
        }
    }

    private static void ClearRowFieldVariables(WorkflowContext context, string rowVarName)
    {
        var prefix = rowVarName + ".";
        var keys = context.Variables.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keys)
            context.Variables.Remove(key);
    }

    private static string GetString(
        Dictionary<string, object> config,
        string key,
        string fallback = "")
    {
        if (!config.TryGetValue(key, out var raw)) return fallback;
        if (raw is JsonElement el) return el.GetString() ?? fallback;
        return raw?.ToString() ?? fallback;
    }

    private static string VariableToString(object? val)
    {
        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? "",
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => "",
                JsonValueKind.Array   => je.GetRawText(),
                JsonValueKind.Object  => je.GetRawText(),
                _                     => je.GetRawText()
            };
        }

        return val?.ToString() ?? "";
    }
}
