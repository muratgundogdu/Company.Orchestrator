using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitPopupStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitPopupStepHandler> _logger;
    public string HandlerType => "browser.wait-popup";

    public BrowserWaitPopupStepHandler(ILogger<BrowserWaitPopupStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.wait-popup";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "clickSelector", out var clickSelector, out var failure))
            return failure!;

        var switchToPopup = BrowserStepHandlerHelpers.ParseBool(
            config.GetValueOrDefault("switchToPopup"), defaultValue: true);

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
            "{StepType}: session='{Session}', clickSelector='{Selector}', switchToPopup={Switch}, timeoutMs={Timeout}",
            stepType,
            sessionName,
            clickSelector,
            switchToPopup,
            timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            var popup = await browser.ClickAndWaitForPopupAsync(
                clickSelector, timeoutMs, switchToPopup, cancellationToken);

            return StepResult.Ok(
                output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["popupUrl"]   = popup.Url,
                    ["popupTitle"] = popup.Title,
                    ["activePageUrl"]   = popup.Url,
                    ["activePageTitle"] = popup.Title,
                    ["activePageIndex"] = popup.Index,
                },
                outputData: $"Popup opened: {popup.Url}");
        }
        catch (TimeoutException ex)
        {
            return StepResult.Fail(
                $"{stepType}: timed out after {timeoutMs}ms waiting for popup from '{clickSelector}'. {ex.Message}");
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail($"{stepType}: failed waiting for popup: {ex.Message}");
        }
    }
}
