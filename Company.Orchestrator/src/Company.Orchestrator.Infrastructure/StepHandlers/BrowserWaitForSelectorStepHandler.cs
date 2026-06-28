using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitForSelectorStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitForSelectorStepHandler> _logger;
    public string HandlerType => "browser.wait-for-selector";

    public BrowserWaitForSelectorStepHandler(ILogger<BrowserWaitForSelectorStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);
        var timeoutMs = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("timeoutMs"), 30_000);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        _logger.LogInformation(
            "BrowserWaitForSelector: session='{Session}', selector='{Selector}', timeoutMs={Timeout}",
            sessionName, selector, timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.WaitForSelectorAsync(selector, timeoutMs, cancellationToken);

        return StepResult.Ok(outputData: $"Selector '{selector}' appeared");
    }
}
