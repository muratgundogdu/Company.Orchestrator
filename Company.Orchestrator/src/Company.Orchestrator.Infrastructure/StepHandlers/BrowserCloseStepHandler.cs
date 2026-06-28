using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserCloseStepHandler : IStepHandler
{
    private readonly ILogger<BrowserCloseStepHandler> _logger;
    public string HandlerType => "browser.close";

    public BrowserCloseStepHandler(ILogger<BrowserCloseStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        _logger.LogInformation("BrowserClose: session='{Session}'", sessionName);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.CloseAsync(cancellationToken);

        return StepResult.Ok(outputData: $"Browser session '{sessionName}' closed");
    }
}
