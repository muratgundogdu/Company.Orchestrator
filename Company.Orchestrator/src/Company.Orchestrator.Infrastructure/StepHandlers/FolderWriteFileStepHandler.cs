using Company.Orchestrator.Application.Capabilities.Folder;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Company.Orchestrator.Infrastructure.Capabilities.Folder;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Writes an existing artifact from the WorkflowContext to a local or UNC file system path.
/// Creates any missing parent directories automatically.
///
/// Config keys:
///   artifactName      (required) — name of the artifact in WorkflowContext.Artifacts to write
///   destinationPath   (required) — target file path or directory (absolute or UNC).
///                                  When a directory, the artifact's file name is appended.
///                                  Supports {{variable}} interpolation
///   overwrite         (optional) — "true" to overwrite an existing file (default: "false")
///
/// Output variables:
///   writtenPath       — the resolved destination path
///   writtenSizeBytes  — byte count written to disk
///
/// JobLog:
///   The engine logs start/complete/fail. OutputData records: "Wrote {N} bytes → '{path}'"
/// </summary>
public sealed class FolderWriteFileStepHandler : IStepHandler
{
    private readonly ILogger<FolderWriteFileStepHandler> _logger;
    public string HandlerType => "folder.write-file";

    public FolderWriteFileStepHandler(ILogger<FolderWriteFileStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("artifactName", out var artifactNameRaw) || artifactNameRaw is null)
            return StepResult.Fail("FolderWriteFile: 'artifactName' is required.");

        if (!config.TryGetValue("destinationPath", out var destPathRaw) || destPathRaw is null)
            return StepResult.Fail("FolderWriteFile: 'destinationPath' is required.");

        var artifactNameRaw2 = artifactNameRaw.ToString()!;
        var destPathRaw2     = destPathRaw.ToString()!;
        var artifactName     = context.Interpolate(artifactNameRaw2);
        var destPath         = context.Interpolate(destPathRaw2);
        var overwrite        = string.Equals(
            config.GetValueOrDefault("overwrite")?.ToString(), "true",
            StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(artifactNameRaw2, artifactName, StringComparison.Ordinal))
            _logger.LogInformation(
                "FolderWriteFile: resolved artifactName: '{Raw}' -> '{Resolved}'",
                artifactNameRaw2, artifactName);

        if (!string.Equals(destPathRaw2, destPath, StringComparison.Ordinal))
            _logger.LogInformation(
                "FolderWriteFile: resolved destinationPath: '{Raw}' -> '{Resolved}'",
                destPathRaw2, destPath);

        if (!context.HasArtifact(artifactName))
            return StepResult.Fail(
                $"FolderWriteFile: artifact '{artifactName}' not found in context.");

        var artifact = context.GetArtifact(artifactName);
        var resolvedDestPath = FolderWriteDestinationPathResolver.Resolve(destPath, artifact.Name);

        if (!string.Equals(destPath, resolvedDestPath, StringComparison.Ordinal))
            _logger.LogInformation(
                "FolderWriteFile: resolved file path: '{Raw}' -> '{Resolved}'",
                destPath, resolvedDestPath);

        _logger.LogInformation(
            "FolderWriteFile: writing artifact '{Name}' ({Bytes:N0} bytes) → '{Dest}' (overwrite={O})",
            artifactName, artifact.SizeBytes, resolvedDestPath, overwrite);

        var folder = context.GetCapability<ISharedFolderCapability>();
        await folder.WriteFileAsync(artifact, resolvedDestPath, overwrite, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["writtenPath"]      = resolvedDestPath,
                ["writtenSizeBytes"] = artifact.SizeBytes
            },
            outputData: $"Wrote {artifact.SizeBytes:N0} bytes → '{resolvedDestPath}'");
    }
}
