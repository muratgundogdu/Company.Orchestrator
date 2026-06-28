using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserHandleAlertStepHandler : IStepHandler
{
    private readonly ILogger<BrowserHandleAlertStepHandler> _logger;
    public string HandlerType => "browser.handle-alert";

    public BrowserHandleAlertStepHandler(ILogger<BrowserHandleAlertStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.handle-alert";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        var action = config.GetValueOrDefault("action")?.ToString()?.Trim().ToLowerInvariant() ?? "accept";
        if (action is not ("accept" or "dismiss"))
            return StepResult.Fail($"{stepType}: 'action' must be 'accept' or 'dismiss'.");

        var promptTextRaw = config.GetValueOrDefault("promptText")?.ToString();
        var promptText    = string.IsNullOrWhiteSpace(promptTextRaw)
            ? null
            : context.Interpolate(promptTextRaw);

        var clickSelectorRaw = config.GetValueOrDefault("clickSelector")?.ToString();
        var clickSelector    = string.IsNullOrWhiteSpace(clickSelectorRaw)
            ? null
            : context.Interpolate(clickSelectorRaw);

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
            "{StepType}: session='{Session}', action='{Action}', clickSelector='{ClickSelector}', timeoutMs={Timeout}",
            stepType,
            sessionName,
            action,
            clickSelector ?? "(wait only)",
            timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            BrowserDialogResult result = string.IsNullOrWhiteSpace(clickSelector)
                ? await browser.WaitForDialogAsync(action, promptText, timeoutMs, cancellationToken)
                : await browser.ClickAndHandleDialogAsync(
                    clickSelector, action, promptText, timeoutMs, cancellationToken);

            return StepResult.Ok(
                output: BuildDialogOutput(result),
                outputData: $"Handled {result.DialogType} dialog: {result.Message}");
        }
        catch (TimeoutException ex)
        {
            return StepResult.Fail(
                $"{stepType}: timed out after {timeoutMs}ms waiting for dialog. {ex.Message}");
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail($"{stepType}: failed to handle dialog: {ex.Message}");
        }
    }

    internal static Dictionary<string, object> BuildDialogOutput(BrowserDialogResult result) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dialogType"]    = result.DialogType,
            ["dialogMessage"] = result.Message,
            ["dialogHandled"] = result.Handled,
        };
}
