using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserClickAndHandleAlertStepHandler : IStepHandler
{
    private readonly ILogger<BrowserClickAndHandleAlertStepHandler> _logger;
    public string HandlerType => "browser.click-and-handle-alert";

    public BrowserClickAndHandleAlertStepHandler(ILogger<BrowserClickAndHandleAlertStepHandler> logger)
        => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.click-and-handle-alert";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "clickSelector", out var clickSelector, out var failure))
            return failure!;

        var action = config.GetValueOrDefault("action")?.ToString()?.Trim().ToLowerInvariant() ?? "accept";
        if (action is not ("accept" or "dismiss"))
            return StepResult.Fail($"{stepType}: 'action' must be 'accept' or 'dismiss'.");

        var promptTextRaw = config.GetValueOrDefault("promptText")?.ToString();
        var promptText    = string.IsNullOrWhiteSpace(promptTextRaw)
            ? null
            : context.Interpolate(promptTextRaw);

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
            "{StepType}: session='{Session}', clickSelector='{Selector}', action='{Action}', timeoutMs={Timeout}",
            stepType, sessionName, clickSelector, action, timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            var result = await browser.ClickAndHandleDialogAsync(
                clickSelector, action, promptText, timeoutMs, cancellationToken);

            return StepResult.Ok(
                output: BrowserHandleAlertStepHandler.BuildDialogOutput(result),
                outputData: $"Clicked '{clickSelector}' and handled {result.DialogType} dialog.");
        }
        catch (TimeoutException ex)
        {
            return StepResult.Fail(
                $"{stepType}: timed out after {timeoutMs}ms waiting for dialog from '{clickSelector}'. {ex.Message}");
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail($"{stepType}: failed: {ex.Message}");
        }
    }
}
