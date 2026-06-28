using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitNetworkIdleStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitNetworkIdleStepHandler> _logger;
    public string HandlerType => "browser.wait-network-idle";

    public BrowserWaitNetworkIdleStepHandler(ILogger<BrowserWaitNetworkIdleStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.wait-network-idle";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        int timeoutMs;
        try
        {
            timeoutMs = BrowserStepHandlerHelpers.ParseTimeoutMs(
                config.GetValueOrDefault("timeoutMs"), 30_000, stepType);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        _logger.LogInformation(
            "{StepType}: session='{Session}', timeoutMs={Timeout}",
            stepType,
            sessionName,
            timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            await browser.WaitForNetworkIdleAsync(timeoutMs, cancellationToken);
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail(
                $"{stepType}: timed out after {timeoutMs}ms waiting for network idle. {ex.Message}");
        }

        return StepResult.Ok(outputData: "Page reached network idle state");
    }
}
