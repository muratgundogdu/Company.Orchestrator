using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserEvaluateStepHandler : IStepHandler
{
    private readonly ILogger<BrowserEvaluateStepHandler> _logger;
    public string HandlerType => "browser.evaluate";

    public BrowserEvaluateStepHandler(ILogger<BrowserEvaluateStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "script", out var script, out var failure))
            return failure!;

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "outputVariable", out var outputVar, out failure))
            return failure!;

        _logger.LogInformation(
            "BrowserEvaluate: session='{Session}', outputVariable='{Var}', scriptLength={Len}",
            sessionName, outputVar, script.Length);

        var browser = context.GetCapability<IBrowserCapability>();
        var result = await browser.EvaluateScriptAsync(script, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object> { [outputVar] = result },
            outputData: $"Evaluated script → {outputVar} ({result.Length} chars)");
    }
}
