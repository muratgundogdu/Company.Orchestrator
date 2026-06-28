using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitUrlStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitUrlStepHandler> _logger;
    public string HandlerType => "browser.wait-url";

    public BrowserWaitUrlStepHandler(ILogger<BrowserWaitUrlStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.wait-url";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        string pattern;
        try
        {
            pattern = BrowserStepHandlerHelpers.ResolveUrlPattern(config, context);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }

        var matchMode = BrowserStepHandlerHelpers.ResolveMatchMode(config);
        if (matchMode is not ("contains" or "equals" or "startswith" or "regex"))
        {
            return StepResult.Fail(
                $"{stepType}: 'matchMode' must be 'contains', 'equals', 'startsWith', or 'regex'.");
        }

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
            "{StepType}: session='{Session}', pattern='{Pattern}', matchMode='{MatchMode}', timeoutMs={Timeout}",
            stepType,
            sessionName,
            pattern,
            matchMode,
            timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            await browser.WaitForUrlAsync(pattern, matchMode, timeoutMs, cancellationToken);
        }
        catch (PlaywrightException ex)
        {
            var currentUrl = await browser.GetCurrentUrlAsync(cancellationToken);
            return StepResult.Fail(
                $"{stepType}: timed out after {timeoutMs}ms waiting for URL {matchMode} '{pattern}'. Current URL: {currentUrl}. {ex.Message}");
        }

        return StepResult.Ok(outputData: $"URL matched ({matchMode}) '{pattern}'");
    }
}
