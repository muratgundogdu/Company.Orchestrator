using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitForUrlStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitForUrlStepHandler> _logger;
    public string HandlerType => "browser.wait-for-url";

    public BrowserWaitForUrlStepHandler(ILogger<BrowserWaitForUrlStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);
        var timeoutMs = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("timeoutMs"), 30_000);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "urlContains", out var urlContains, out var failure))
            return failure!;

        _logger.LogInformation(
            "BrowserWaitForUrl: session='{Session}', urlContains='{Part}', timeoutMs={Timeout}",
            sessionName, urlContains, timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.WaitForUrlContainsAsync(urlContains, timeoutMs, cancellationToken);

        return StepResult.Ok(outputData: $"URL contains '{urlContains}'");
    }
}
