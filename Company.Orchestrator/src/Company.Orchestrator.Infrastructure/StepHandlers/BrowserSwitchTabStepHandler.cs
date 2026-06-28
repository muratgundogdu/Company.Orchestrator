using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserSwitchTabStepHandler : IStepHandler
{
    private readonly ILogger<BrowserSwitchTabStepHandler> _logger;
    public string HandlerType => "browser.switch-tab";

    public BrowserSwitchTabStepHandler(ILogger<BrowserSwitchTabStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.switch-tab";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        var mode = config.GetValueOrDefault("mode")?.ToString()?.Trim().ToLowerInvariant() ?? "last";
        if (mode is not ("last" or "first" or "byurl" or "bytitle"))
        {
            return StepResult.Fail(
                $"{stepType}: 'mode' must be 'last', 'first', 'byUrl', or 'byTitle'.");
        }

        var urlContains   = context.Interpolate(config.GetValueOrDefault("urlContains")?.ToString() ?? string.Empty);
        var titleContains = context.Interpolate(config.GetValueOrDefault("titleContains")?.ToString() ?? string.Empty);

        if (mode == "byurl" && string.IsNullOrWhiteSpace(urlContains))
            return StepResult.Fail($"{stepType}: 'urlContains' is required when mode is 'byUrl'.");
        if (mode == "bytitle" && string.IsNullOrWhiteSpace(titleContains))
            return StepResult.Fail($"{stepType}: 'titleContains' is required when mode is 'byTitle'.");

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
            "{StepType}: session='{Session}', mode='{Mode}', timeoutMs={Timeout}",
            stepType, sessionName, mode, timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            var page = await browser.SwitchPageAsync(
                mode,
                string.IsNullOrWhiteSpace(urlContains) ? null : urlContains,
                string.IsNullOrWhiteSpace(titleContains) ? null : titleContains,
                timeoutMs,
                cancellationToken);

            return StepResult.Ok(
                output: BuildPageOutput(page, tabClosed: null),
                outputData: $"Switched to tab {page.Index}: {page.Url}");
        }
        catch (TimeoutException ex)
        {
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail($"{stepType}: failed to switch tab: {ex.Message}");
        }
    }

    internal static Dictionary<string, object> BuildPageOutput(BrowserPageInfo page, bool? tabClosed) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["activePageUrl"]   = page.Url,
            ["activePageTitle"] = page.Title,
            ["activePageIndex"] = page.Index,
            ["tabClosed"]       = tabClosed ?? false,
        };
}
