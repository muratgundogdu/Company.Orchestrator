using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserNavigateStepHandler : IStepHandler
{
    private readonly ILogger<BrowserNavigateStepHandler> _logger;
    public string HandlerType => "browser.navigate";

    public BrowserNavigateStepHandler(ILogger<BrowserNavigateStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "url", out var url, out var failure))
            return failure!;

        _logger.LogInformation("BrowserNavigate: session='{Session}', url='{Url}'", sessionName, url);

        var browser = context.GetCapability<IBrowserCapability>();
        await browser.NavigateAsync(url, cancellationToken);

        return StepResult.Ok(outputData: $"Navigated to '{url}'");
    }
}
