using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserScrollStepHandler : IStepHandler
{
    private readonly ILogger<BrowserScrollStepHandler> _logger;
    public string HandlerType => "browser.scroll";

    public BrowserScrollStepHandler(ILogger<BrowserScrollStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        var direction = config.GetValueOrDefault("direction")?.ToString()?.Trim().ToLowerInvariant() ?? "down";
        var amount    = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("amount"), defaultValue: 1000);

        if (amount <= 0)
            return StepResult.Fail("browser.scroll: 'amount' must be greater than 0.");

        var selectorRaw = config.GetValueOrDefault("selector")?.ToString();
        string? selector = string.IsNullOrWhiteSpace(selectorRaw)
            ? null
            : context.Interpolate(selectorRaw);

        _logger.LogInformation(
            "BrowserScroll: session='{Session}', selector='{Selector}', direction='{Direction}', amount={Amount}",
            sessionName,
            selector ?? "(page)",
            direction,
            amount);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.ScrollAsync(selector, direction, amount, cancellationToken);

        return StepResult.Ok(outputData: $"Scrolled {direction} by {amount}px");
    }
}
