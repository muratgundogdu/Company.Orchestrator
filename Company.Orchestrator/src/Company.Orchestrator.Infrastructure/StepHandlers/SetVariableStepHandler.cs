using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Expressions;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Creates or updates a workflow variable with literal interpolation or expression evaluation.
/// </summary>
public sealed class SetVariableStepHandler : IStepHandler
{
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ILogger<SetVariableStepHandler> _logger;

    public string HandlerType => "set.variable";

    public SetVariableStepHandler(
        IExpressionEvaluator expressionEvaluator,
        ILogger<SetVariableStepHandler> logger)
    {
        _expressionEvaluator = expressionEvaluator;
        _logger              = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "set.variable";
        var config = context.StepDefinition.Config;

        var variableName = SetVariableStepHandlerHelpers.NormalizeVariableName(
            SetVariableStepHandlerHelpers.GetString(config, "variableName"));
        if (string.IsNullOrWhiteSpace(variableName))
            return StepResult.Fail($"{stepType}: 'variableName' is required.");

        if (!config.TryGetValue("value", out var valueRaw) || valueRaw is null)
            return StepResult.Fail($"{stepType}: 'value' is required.");

        var valueTemplate = SetVariableStepHandlerHelpers.GetString(config, "value");
        var valueType     = SetVariableStepHandlerHelpers.GetString(config, "valueType", "string")
            .Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(valueType))
            valueType = "string";

        var mode = SetVariableStepHandlerHelpers.GetString(config, "mode", "literal")
            .Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(mode))
            mode = "literal";

        if (!SetVariableStepHandlerHelpers.SupportedValueTypes.Contains(valueType))
        {
            return StepResult.Fail(
                $"{stepType}: 'valueType' must be 'string', 'number', 'boolean', or 'json'.");
        }

        if (!SetVariableStepHandlerHelpers.SupportedModes.Contains(mode))
        {
            return StepResult.Fail(
                $"{stepType}: 'mode' must be 'literal' or 'expression'.");
        }

        object rawValue;
        try
        {
            rawValue = mode == "expression"
                ? await _expressionEvaluator.EvaluateAsync(valueTemplate, context, cancellationToken)
                : context.Interpolate(valueTemplate);
        }
        catch (ExpressionEvaluationException ex)
        {
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }

        object coerced;
        try
        {
            coerced = SetVariableStepHandlerHelpers.CoerceObject(rawValue, valueType, stepType);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        var valueLength = SetVariableStepHandlerHelpers.ValueLength(coerced);

        _logger.LogInformation(
            "{StepType}: variable='{VariableName}', mode={Mode}, expressionLength={ExpressionLength}, resultType={ResultType}",
            stepType,
            variableName,
            mode,
            valueTemplate.Length,
            coerced.GetType().Name);

        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [variableName] = coerced,
        };

        return StepResult.Ok(
            output: output,
            outputData: $"Set '{variableName}' ({mode}, {valueType}, {valueLength} character(s)).");
    }
}
