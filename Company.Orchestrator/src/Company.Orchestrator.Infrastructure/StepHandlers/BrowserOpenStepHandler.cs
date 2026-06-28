using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserOpenStepHandler : IStepHandler
{
    private readonly ILogger<BrowserOpenStepHandler> _logger;
    public string HandlerType => "browser.open";

    public BrowserOpenStepHandler(ILogger<BrowserOpenStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);
        var headless = BrowserStepHandlerHelpers.ParseBool(config.GetValueOrDefault("headless"), defaultValue: true);
        var width  = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("viewportWidth"), 1366);
        var height = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("viewportHeight"), 768);

        _logger.LogInformation(
            "BrowserOpen: session='{Session}', headless={Headless}, viewport={W}x{H}",
            sessionName, headless, width, height);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.OpenAsync(new BrowserOptions
        {
            Headless       = headless,
            ViewportWidth  = width,
            ViewportHeight = height,
        }, cancellationToken);

        return StepResult.Ok(outputData: $"Browser session '{sessionName}' opened");
    }
}
