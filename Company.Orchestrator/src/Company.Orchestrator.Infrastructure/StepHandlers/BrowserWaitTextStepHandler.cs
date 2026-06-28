using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitTextStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitTextStepHandler> _logger;
    public string HandlerType => "browser.wait-text";

    public BrowserWaitTextStepHandler(ILogger<BrowserWaitTextStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.wait-text";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "text", out var text, out var failure))
            return failure!;

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

        var selectorRaw = config.GetValueOrDefault("selector")?.ToString();
        var selector = string.IsNullOrWhiteSpace(selectorRaw)
            ? null
            : context.Interpolate(selectorRaw);

        _logger.LogInformation(
            "{StepType}: session='{Session}', text='{Text}', selector='{Selector}', timeoutMs={Timeout}",
            stepType,
            sessionName,
            text,
            selector ?? "(page)",
            timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            if (string.IsNullOrWhiteSpace(selector))
                await browser.WaitForTextAsync(text, timeoutMs, cancellationToken);
            else
                await browser.WaitForTextAsync(text, selector, timeoutMs, cancellationToken);
        }
        catch (PlaywrightException ex)
        {
            var scope = string.IsNullOrWhiteSpace(selector) ? "page" : $"selector '{selector}'";
            return StepResult.Fail(
                $"{stepType}: timed out after {timeoutMs}ms waiting for text '{text}' in {scope}. {ex.Message}");
        }

        return StepResult.Ok(outputData: $"Text '{text}' appeared");
    }
}
