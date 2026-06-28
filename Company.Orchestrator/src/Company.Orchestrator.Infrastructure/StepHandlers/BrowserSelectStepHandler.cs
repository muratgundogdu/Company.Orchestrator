using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserSelectStepHandler : IStepHandler
{
    private readonly ILogger<BrowserSelectStepHandler> _logger;
    public string HandlerType => "browser.select";

    public BrowserSelectStepHandler(ILogger<BrowserSelectStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        var mode = config.GetValueOrDefault("mode")?.ToString()?.Trim().ToLowerInvariant() ?? "value";

        var browser = context.GetCapability<IBrowserCapability>();

        switch (mode)
        {
            case "value":
            {
                if (!BrowserStepHandlerHelpers.TryRequire(context, config, "value", out var value, out failure))
                    return failure!;
                _logger.LogInformation(
                    "BrowserSelect: session='{Session}', selector='{Selector}', mode=value, value='{Value}'",
                    sessionName, selector, value);
                await browser.SelectOptionAsync(selector, value, cancellationToken);
                break;
            }
            case "label":
            {
                if (!BrowserStepHandlerHelpers.TryRequire(context, config, "label", out var label, out failure))
                    return failure!;
                _logger.LogInformation(
                    "BrowserSelect: session='{Session}', selector='{Selector}', mode=label, label='{Label}'",
                    sessionName, selector, label);
                await browser.SelectOptionByValueOrLabelAsync(selector, value: null, label, cancellationToken);
                break;
            }
            case "index":
            {
                var index = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("index"), defaultValue: -1);
                if (index < 0)
                    return StepResult.Fail("browser.select: 'index' must be >= 0 when mode is 'index'.");
                _logger.LogInformation(
                    "BrowserSelect: session='{Session}', selector='{Selector}', mode=index, index={Index}",
                    sessionName, selector, index);
                await browser.SelectOptionByIndexAsync(selector, index, cancellationToken);
                break;
            }
            default:
                return StepResult.Fail(
                    "browser.select: 'mode' must be 'value', 'label', or 'index'.");
        }

        return StepResult.Ok(outputData: $"Selected option on '{selector}' (mode={mode})");
    }
}
