using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserGetAttributeStepHandler : IStepHandler
{
    private readonly ILogger<BrowserGetAttributeStepHandler> _logger;
    public string HandlerType => "browser.get-attribute";

    public BrowserGetAttributeStepHandler(ILogger<BrowserGetAttributeStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "attribute", out var attribute, out failure))
            return failure!;

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "outputVariable", out var outputVar, out failure))
            return failure!;

        _logger.LogInformation(
            "BrowserGetAttribute: session='{Session}', selector='{Selector}', attribute='{Attr}', outputVariable='{Var}'",
            sessionName, selector, attribute, outputVar);

        var browser = context.GetCapability<IBrowserCapability>();
        var value = await browser.GetAttributeAsync(selector, attribute, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object> { [outputVar] = value },
            outputData: $"Read attribute '{attribute}' from '{selector}'");
    }
}
