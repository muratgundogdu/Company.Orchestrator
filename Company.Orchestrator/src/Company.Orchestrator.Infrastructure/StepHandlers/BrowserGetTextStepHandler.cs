using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserGetTextStepHandler : IStepHandler
{
    private readonly ILogger<BrowserGetTextStepHandler> _logger;
    public string HandlerType => "browser.get-text";

    public BrowserGetTextStepHandler(ILogger<BrowserGetTextStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "outputVariable", out var outputVar, out failure))
            return failure!;

        _logger.LogInformation(
            "BrowserGetText: session='{Session}', selector='{Selector}', outputVariable='{Var}'",
            sessionName, selector, outputVar);

        var browser = context.GetCapability<IBrowserCapability>();
        var text = await browser.GetTextAsync(selector, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object> { [outputVar] = text },
            outputData: $"Read text from '{selector}' ({text.Length} chars)");
    }
}
