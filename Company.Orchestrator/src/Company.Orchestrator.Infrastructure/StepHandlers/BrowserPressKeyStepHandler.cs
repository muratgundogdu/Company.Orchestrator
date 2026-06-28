using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserPressKeyStepHandler : IStepHandler
{
    private readonly ILogger<BrowserPressKeyStepHandler> _logger;
    public string HandlerType => "browser.press-key";

    public BrowserPressKeyStepHandler(ILogger<BrowserPressKeyStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "key", out var key, out var failure))
            return failure!;

        var selectorRaw = config.GetValueOrDefault("selector")?.ToString();
        var selector = string.IsNullOrWhiteSpace(selectorRaw)
            ? null
            : context.Interpolate(selectorRaw);

        _logger.LogInformation(
            "BrowserPressKey: session='{Session}', key='{Key}', selector='{Selector}'",
            sessionName,
            key,
            selector ?? "(page)");

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.PressKeyAsync(key, selector, cancellationToken);

        return StepResult.Ok(outputData: $"Pressed key '{key}'");
    }
}
