using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Extracts a value from a workflow variable (typically mail body text) by label or regex.
///
/// Config keys:
///   sourceVariable  — variable containing text to search (required)
///   label           — label to find, e.g. "Tutar" (optional if pattern set)
///   pattern         — regex pattern with optional capture group (optional if label set)
///   outputVariable  — variable to store extracted value (required)
/// </summary>
public sealed class MailExtractValueStepHandler : IStepHandler
{
    private readonly ILogger<MailExtractValueStepHandler> _logger;

    public string HandlerType => "mail.extract-value";

    public MailExtractValueStepHandler(ILogger<MailExtractValueStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var sourceVar = config.GetValueOrDefault("sourceVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(sourceVar))
            return Task.FromResult(StepResult.Fail("mail.extract-value: 'sourceVariable' is required."));

        sourceVar = MailVariableHelper.NormalizeVariableName(context.Interpolate(sourceVar));

        var outputVar = config.GetValueOrDefault("outputVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(outputVar))
            return Task.FromResult(StepResult.Fail("mail.extract-value: 'outputVariable' is required."));

        var label   = config.GetValueOrDefault("label")?.ToString()?.Trim();
        var pattern = config.GetValueOrDefault("pattern")?.ToString();

        var hasLabel   = !string.IsNullOrWhiteSpace(label);
        var hasPattern = !string.IsNullOrWhiteSpace(pattern);

        if (!hasLabel && !hasPattern)
        {
            return Task.FromResult(StepResult.Fail(
                "mail.extract-value: either 'label' or 'pattern' is required."));
        }

        if (!MailVariableHelper.TryGetVariableString(context.Variables, sourceVar, out var content, out var varError))
            return Task.FromResult(StepResult.Fail($"mail.extract-value: {varError}"));

        string? extracted = null;
        string method;

        if (hasLabel)
        {
            method    = "label";
            extracted = MailVariableHelper.ExtractByLabel(content, label!);
            if (extracted is null)
            {
                return Task.FromResult(StepResult.Fail(
                    $"mail.extract-value: label '{label}' not found in variable '{sourceVar}'."));
            }
        }
        else
        {
            method = "regex";
            try
            {
                var regex = new Regex(pattern!, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var match = regex.Match(content);
                if (!match.Success)
                {
                    return Task.FromResult(StepResult.Fail(
                        $"mail.extract-value: pattern did not match content in '{sourceVar}'."));
                }

                extracted = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                extracted = extracted.Trim();
            }
            catch (Exception ex)
            {
                return Task.FromResult(StepResult.Fail(
                    $"mail.extract-value: invalid regex pattern: {ex.Message}"));
            }
        }

        _logger.LogInformation(
            "mail.extract-value: extracted '{Value}' from '{SourceVar}' via {Method} → '{OutputVar}'",
            extracted, sourceVar, method, outputVar);

        return Task.FromResult(StepResult.Ok(
            output: new Dictionary<string, object> { [outputVar] = extracted },
            outputData: $"Extracted '{extracted}' from '{sourceVar}' ({method})"));
    }
}
