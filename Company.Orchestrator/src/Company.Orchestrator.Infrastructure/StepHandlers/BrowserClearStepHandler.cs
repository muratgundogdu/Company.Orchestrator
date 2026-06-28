using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserClearStepHandler : IStepHandler
{
    private readonly ILogger<BrowserClearStepHandler> _logger;
    public string HandlerType => "browser.clear";

    public BrowserClearStepHandler(ILogger<BrowserClearStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        _logger.LogInformation("BrowserClear: session='{Session}', selector='{Selector}'", sessionName, selector);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.ClearInputAsync(selector, cancellationToken);

        return StepResult.Ok(outputData: $"Cleared input '{selector}'");
    }
}
