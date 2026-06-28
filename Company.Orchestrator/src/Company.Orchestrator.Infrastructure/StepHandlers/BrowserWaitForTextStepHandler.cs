using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitForTextStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitForTextStepHandler> _logger;
    public string HandlerType => "browser.wait-for-text";

    public BrowserWaitForTextStepHandler(ILogger<BrowserWaitForTextStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);
        var timeoutMs = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("timeoutMs"), 30_000);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "text", out var text, out var failure))
            return failure!;

        _logger.LogInformation(
            "BrowserWaitForText: session='{Session}', text='{Text}', timeoutMs={Timeout}",
            sessionName, text, timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.WaitForTextAsync(text, timeoutMs, cancellationToken);

        return StepResult.Ok(outputData: $"Text '{text}' appeared on page");
    }
}
