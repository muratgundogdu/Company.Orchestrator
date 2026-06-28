using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Captures a screenshot of the current browser page as an artifact.
/// Requires an open browser session (browser.open).
///
/// Config keys:
///   sessionName  — logical session label (default: "default")
///   artifactName — context name for the screenshot artifact (supports {{variable}})
/// </summary>
public sealed class BrowserScreenshotStepHandler : IStepHandler
{
    private readonly ILogger<BrowserScreenshotStepHandler> _logger;
    public string HandlerType => "browser.screenshot";

    public BrowserScreenshotStepHandler(ILogger<BrowserScreenshotStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "artifactName", out var artifactName, out var failure))
            return failure!;

        var fileName = BrowserStepHandlerHelpers.EnsurePngExtension(artifactName);

        _logger.LogInformation(
            "BrowserScreenshot: session='{Session}', artifactName='{Name}', fileName='{FileName}'",
            sessionName, artifactName, fileName);

        var browser = context.GetCapability<IBrowserCapability>();
        var artifact = await browser.TakeScreenshotAsync(fileName, cancellationToken);
        artifact = artifact with { Name = fileName };
        context.Artifacts[fileName] = artifact;

        return StepResult.WithArtifact(artifact, $"Screenshot captured ({artifact.SizeBytes} bytes)");
    }
}
