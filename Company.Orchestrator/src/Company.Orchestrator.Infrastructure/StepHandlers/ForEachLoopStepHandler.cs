using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Iterates over a collection variable and routes to <c>LoopStepId</c> once per item.
/// After the last item, routes to <c>CompletedStepId</c>.
/// </summary>
public class ForEachLoopStepHandler : IStepHandler
{
    private readonly ILogger<ForEachLoopStepHandler> _logger;

    public string HandlerType => "foreach.loop";

    public ForEachLoopStepHandler(ILogger<ForEachLoopStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stepDef  = context.StepDefinition;
        var config   = stepDef.Config;
        var stepId   = stepDef.Id;

        var collectionExpr = GetString(config, "collection");
        var itemVarName    = GetString(config, "itemVariable",  "currentItem");
        var indexVarName   = GetString(config, "indexVariable", "currentIndex");

        var loopStepId      = stepDef.LoopStepId;
        var completedStepId = stepDef.CompletedStepId;

        var itemsKey = $"__foreach_{stepId}_items__";
        var indexKey = $"__foreach_{stepId}_index__";

        List<string> items;
        if (!context.Variables.ContainsKey(itemsKey))
        {
            var resolved = context.Interpolate(collectionExpr);
            items = ResolveCollection(resolved, collectionExpr);

            context.Variables[itemsKey] = JsonSerializer.Serialize(items);
            context.Variables[indexKey] = 0;

            _logger.LogInformation(
                "ForEach {StepId}: initialized collection count = {Count}",
                stepId, items.Count);
        }
        else
        {
            var raw = context.Variables[itemsKey]?.ToString() ?? "[]";
            items = JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
        }

        var index = Convert.ToInt32(context.Variables.GetValueOrDefault(indexKey, 0));

        // All items processed (or empty collection) → route to completed branch.
        if (index >= items.Count)
        {
            _logger.LogInformation(
                "ForEach {StepId}: completed → routing to completedStepId {CompletedStepId}",
                stepId, completedStepId ?? "(none)");

            context.Variables.Remove(itemsKey);
            context.Variables.Remove(indexKey);

            return Task.FromResult(StepResult.Ok(
                output: new Dictionary<string, object>
                {
                    ["nextStepId"]       = completedStepId ?? "",
                    ["foreachCompleted"] = true,
                    ["foreachItemCount"] = items.Count,
                },
                outputData: $"ForEach complete — {items.Count} item(s) processed. → {completedStepId ?? "end"}"));
        }

        var currentItem = items[index];

        _logger.LogInformation(
            "ForEach {StepId}: iteration {Index} item = {Item} → routing to loopStepId {LoopStepId}",
            stepId, index, currentItem, loopStepId ?? "(none)");

        context.Variables[itemVarName]  = currentItem;
        context.Variables[indexVarName] = index;
        context.Variables[indexKey]     = index + 1;

        return Task.FromResult(StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["nextStepId"]       = loopStepId ?? "",
                [itemVarName]        = currentItem,
                [indexVarName]       = index,
                ["foreachCompleted"] = false,
            },
            outputData: $"ForEach item [{index + 1}/{items.Count}] = '{currentItem}'. → {loopStepId ?? "end"}"));
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

    private static List<string> ResolveCollection(string resolved, string originalExpr)
    {
        if (string.IsNullOrWhiteSpace(resolved))
            return new List<string>();

        var trimmed = resolved.Trim();

        if (trimmed.StartsWith('['))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (list != null) return list;
            }
            catch { /* fall through */ }

            try
            {
                using var doc  = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                    return root.EnumerateArray()
                               .Select(e => e.ValueKind == JsonValueKind.String
                                            ? e.GetString()! : e.GetRawText())
                               .ToList();
            }
            catch { /* fall through */ }
        }

        if (trimmed.Contains(','))
            return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .ToList();

        return new List<string> { trimmed };
    }
}
