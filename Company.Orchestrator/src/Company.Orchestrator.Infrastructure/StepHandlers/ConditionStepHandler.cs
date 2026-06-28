using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Evaluates a simple condition and routes to trueStepId or falseStepId.
/// Config: { "variable": "{{status}}", "operator": "equals", "value": "approved",
///           "trueStepId": "...", "falseStepId": "..." }
/// Operators: equals, notEquals, greaterThan, lessThan, contains, isNull, isNotNull
/// </summary>
public class ConditionStepHandler : IStepHandler
{
    private readonly ILogger<ConditionStepHandler> _logger;

    public string HandlerType => "Condition";

    public ConditionStepHandler(ILogger<ConditionStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var variableExpr = context.Interpolate(config.GetValueOrDefault("variable")?.ToString() ?? "");
        var @operator    = config.GetValueOrDefault("operator")?.ToString() ?? "equals";
        var compareValue = config.GetValueOrDefault("value")?.ToString() ?? "";

        var result = @operator.ToLowerInvariant() switch
        {
            "equals"     => string.Equals(variableExpr, compareValue, StringComparison.OrdinalIgnoreCase),
            "notequals"  => !string.Equals(variableExpr, compareValue, StringComparison.OrdinalIgnoreCase),
            "contains"   => variableExpr.Contains(compareValue, StringComparison.OrdinalIgnoreCase),
            "isnull"     => string.IsNullOrEmpty(variableExpr),
            "isnotnull"  => !string.IsNullOrEmpty(variableExpr),
            "greaterthan" => double.TryParse(variableExpr, out var l1) && double.TryParse(compareValue, out var r1) && l1 > r1,
            "lessthan"   => double.TryParse(variableExpr, out var l2) && double.TryParse(compareValue, out var r2) && l2 < r2,
            _            => false
        };

        _logger.LogInformation("Condition: '{Variable}' {Operator} '{Value}' => {Result}",
            variableExpr, @operator, compareValue, result);

        var nextStepKey = result ? "trueStepId" : "falseStepId";
        var nextStepId  = config.GetValueOrDefault(nextStepKey)?.ToString();

        return Task.FromResult(StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["conditionResult"] = result,
                ["nextStepId"]      = nextStepId ?? ""
            },
            outputData: $"Condition={result}, Next={nextStepId}"));
    }
}
