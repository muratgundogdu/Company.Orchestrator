using Company.Orchestrator.Application.Capabilities.File;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Writes an artifact from the WorkflowContext to the file system.
///
/// Config keys:
///   artifactName    — name of the artifact in WorkflowContext.Artifacts
///   destinationPath — target file path (supports {{variable}} interpolation)
/// </summary>
public sealed class FileWriteStepHandler : IStepHandler
{
    private readonly ILogger<FileWriteStepHandler> _logger;
    public string HandlerType => "file.write";

    public FileWriteStepHandler(ILogger<FileWriteStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("artifactName", out var artifactNameRaw))
            return StepResult.Fail("FileWriteStepHandler: 'artifactName' is required.");

        if (!config.TryGetValue("destinationPath", out var destPathRaw))
            return StepResult.Fail("FileWriteStepHandler: 'destinationPath' is required.");

        var artifactName = artifactNameRaw?.ToString() ?? "";
        var destPath = context.Interpolate(destPathRaw?.ToString() ?? "");

        if (!context.HasArtifact(artifactName))
            return StepResult.Fail($"FileWriteStepHandler: artifact '{artifactName}' not found in context.");

        var artifact = context.GetArtifact(artifactName);
        var file = context.GetCapability<IFileCapability>();

        _logger.LogInformation("FileWriteStepHandler: writing artifact '{Name}' → '{Dest}'", artifactName, destPath);
        await file.WriteFileAsync(artifact, destPath, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object> { ["writtenPath"] = destPath },
            outputData: $"Wrote {artifact.SizeBytes:N0} bytes to '{destPath}'");
    }
}
