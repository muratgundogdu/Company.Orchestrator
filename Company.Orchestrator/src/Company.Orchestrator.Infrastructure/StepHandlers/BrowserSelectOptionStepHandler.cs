using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserSelectOptionStepHandler : IStepHandler
{
    private readonly ILogger<BrowserSelectOptionStepHandler> _logger;
    public string HandlerType => "browser.select-option";

    public BrowserSelectOptionStepHandler(ILogger<BrowserSelectOptionStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        var valueRaw = config.GetValueOrDefault("value")?.ToString();
        var labelRaw = config.GetValueOrDefault("label")?.ToString();
        var value = string.IsNullOrWhiteSpace(valueRaw) ? null : context.Interpolate(valueRaw);
        var label = string.IsNullOrWhiteSpace(labelRaw) ? null : context.Interpolate(labelRaw);

        if (value is null && label is null)
            return StepResult.Fail("browser.select-option: either 'value' or 'label' is required.");

        _logger.LogInformation(
            "BrowserSelectOption: session='{Session}', selector='{Selector}', value='{Value}', label='{Label}'",
            sessionName, selector, value, label);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.SelectOptionByValueOrLabelAsync(selector, value, label, cancellationToken);

        return StepResult.Ok(outputData: $"Selected option on '{selector}'");
    }
}
