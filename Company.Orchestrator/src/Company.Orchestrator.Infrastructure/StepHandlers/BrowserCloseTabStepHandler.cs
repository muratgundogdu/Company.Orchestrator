using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserCloseTabStepHandler : IStepHandler
{
    private readonly ILogger<BrowserCloseTabStepHandler> _logger;
    public string HandlerType => "browser.close-tab";

    public BrowserCloseTabStepHandler(ILogger<BrowserCloseTabStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.close-tab";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        var mode = config.GetValueOrDefault("mode")?.ToString()?.Trim().ToLowerInvariant() ?? "current";
        if (mode is not ("current" or "last" or "first"))
        {
            return StepResult.Fail(
                $"{stepType}: 'mode' must be 'current', 'last', or 'first'.");
        }

        _logger.LogInformation(
            "{StepType}: session='{Session}', mode='{Mode}'",
            stepType, sessionName, mode);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            var page = await browser.ClosePageAsync(mode, cancellationToken);
            var output = BrowserSwitchTabStepHandler.BuildPageOutput(page, tabClosed: true);
            return StepResult.Ok(
                output: output,
                outputData: $"Closed tab and switched to {page.Url}");
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail($"{stepType}: failed to close tab: {ex.Message}");
        }
    }
}
