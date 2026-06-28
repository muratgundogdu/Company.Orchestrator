using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserTypeStepHandler : IStepHandler
{
    private readonly ILogger<BrowserTypeStepHandler> _logger;
    public string HandlerType => "browser.type";

    public BrowserTypeStepHandler(ILogger<BrowserTypeStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "text", out var text, out failure))
            return failure!;

        _logger.LogInformation("BrowserType: session='{Session}', selector='{Selector}'", sessionName, selector);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.TypeAsync(selector, text, cancellationToken);

        return StepResult.Ok(outputData: $"Typed into '{selector}'");
    }
}
