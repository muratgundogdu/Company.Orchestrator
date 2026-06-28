using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Evaluates a condition and routes to trueStepId or falseStepId.
///
/// Expected workflow JSON step shape (produced by the Admin Panel Workflow Designer):
/// <code>
/// {
///   "type": "condition.if",
///   "trueStepId":  "...",        // top-level — deserialized into StepDefinition.TrueStepId
///   "falseStepId": "...",        // top-level — deserialized into StepDefinition.FalseStepId
///   "config": {
///     "condition": {
///       "left":     "{{mailArtifacts_count}}",
///       "operator": ">",          // exists | == | != | > | <
///       "right":    "0"
///     }
///   }
/// }
/// </code>
///
/// Routing works via the engine's OutputVariables["nextStepId"] override mechanism.
/// </summary>
public class ConditionIfStepHandler : IStepHandler
{
    private readonly ILogger<ConditionIfStepHandler> _logger;

    public string HandlerType => "condition.if";

    public ConditionIfStepHandler(ILogger<ConditionIfStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stepDef = context.StepDefinition;
        var config  = stepDef.Config;

        // ── Read condition fields from config.condition ──────────────────────
        string left = "", @operator = ">", right = "";

        if (config.TryGetValue("condition", out var condRaw))
        {
            if (condRaw is JsonElement el && el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty("left",     out var lEl)) left      = lEl.GetString() ?? "";
                if (el.TryGetProperty("operator", out var oEl)) @operator = oEl.GetString() ?? ">";
                if (el.TryGetProperty("right",    out var rEl))
                {
                    // right can be a JSON number (e.g. 0) or a quoted string ("0")
                    right = rEl.ValueKind == JsonValueKind.Number
                        ? rEl.GetRawText()
                        : rEl.GetString() ?? "";
                }
            }
            else if (condRaw is Dictionary<string, object> dict)
            {
                left      = dict.GetValueOrDefault("left")?.ToString()     ?? "";
                @operator = dict.GetValueOrDefault("operator")?.ToString() ?? ">";
                right     = dict.GetValueOrDefault("right")?.ToString()    ?? "";
            }
        }

        // ── Interpolate left/right with workflow context variables ───────────
        var leftResolved  = context.Interpolate(left);
        var rightResolved = context.Interpolate(right);

        bool result = Evaluate(leftResolved, @operator, rightResolved);

        _logger.LogInformation(
            "condition.if: '{Left}' {Op} '{Right}' => {Result}  (trueStep={True}, falseStep={False})",
            leftResolved, @operator, rightResolved, result,
            stepDef.TrueStepId, stepDef.FalseStepId);

        // ── Pick next step; emit via output variable for engine routing ───────
        var nextStepId = result ? stepDef.TrueStepId : stepDef.FalseStepId;

        return Task.FromResult(StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["conditionResult"] = result,
                ["nextStepId"]      = nextStepId ?? "",
            },
            outputData: $"Condition [{leftResolved} {@operator} {rightResolved}] = {result}, NextStep = {nextStepId ?? "none"}"));
    }

    /// <summary>
    /// Evaluates the condition expression.
    /// Operators: exists | == | != | &gt; | &lt;
    /// Numeric comparisons use <see cref="double"/> parsing; string comparisons are ordinal-case-insensitive.
    /// </summary>
    private static bool Evaluate(string left, string @operator, string right)
    {
        return @operator switch
        {
            "exists"
                => !string.IsNullOrEmpty(left),

            "==" or "equals"
                => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),

            "!=" or "notEquals" or "notequals"
                => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),

            ">" or "greaterThan" or "greaterthan"
                => double.TryParse(left,  System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l1)
                && double.TryParse(right, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r1)
                && l1 > r1,

            "<" or "lessThan" or "lessthan"
                => double.TryParse(left,  System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l2)
                && double.TryParse(right, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r2)
                && l2 < r2,

            _ => false,
        };
    }
}
