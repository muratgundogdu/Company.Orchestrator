using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserElementExistsStepHandler : IStepHandler
{
    private readonly ILogger<BrowserElementExistsStepHandler> _logger;
    public string HandlerType => "browser.element-exists";

    public BrowserElementExistsStepHandler(ILogger<BrowserElementExistsStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.element-exists";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        var outputVariable = config.GetValueOrDefault("outputVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(outputVariable))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");

        int timeoutMs;
        try
        {
            timeoutMs = BrowserStepHandlerHelpers.ParseTimeoutMs(
                config.GetValueOrDefault("timeoutMs"), 5_000, stepType);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        var visibleOnly = BrowserStepHandlerHelpers.ParseBool(config.GetValueOrDefault("visibleOnly"), defaultValue: true);

        _logger.LogInformation(
            "{StepType}: session='{Session}', selector='{Selector}', visibleOnly={VisibleOnly}, timeoutMs={Timeout}",
            stepType,
            sessionName,
            selector,
            visibleOnly,
            timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        var exists  = await browser.ElementExistsAsync(selector, timeoutMs, visibleOnly, cancellationToken);

        _logger.LogInformation(
            "{StepType}: selector='{Selector}' exists={Exists}",
            stepType,
            selector,
            exists);

        return StepResult.Ok(
            output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [outputVariable]                  = exists,
                [$"{outputVariable}_selector"]    = selector,
            },
            outputData: $"Element '{selector}' exists={exists}");
    }
}
