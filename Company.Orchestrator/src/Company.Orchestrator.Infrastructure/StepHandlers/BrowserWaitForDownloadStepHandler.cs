using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitForDownloadStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitForDownloadStepHandler> _logger;
    public string HandlerType => "browser.wait-for-download";

    public BrowserWaitForDownloadStepHandler(ILogger<BrowserWaitForDownloadStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "clickSelector", out var clickSelector, out var failure))
            return failure!;

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "artifactName", out var artifactName, out failure))
            return failure!;

        _logger.LogInformation(
            "BrowserWaitForDownload: session='{Session}', clickSelector='{Selector}', artifactName='{Name}'",
            sessionName, clickSelector, artifactName);

        var browser = context.GetCapability<IBrowserCapability>();
        var artifact = await browser.DownloadByClickAsync(clickSelector, artifactName, cancellationToken);
        context.Artifacts[artifact.Name] = artifact;

        return StepResult.WithArtifact(artifact, $"Downloaded file via click on '{clickSelector}'");
    }
}
