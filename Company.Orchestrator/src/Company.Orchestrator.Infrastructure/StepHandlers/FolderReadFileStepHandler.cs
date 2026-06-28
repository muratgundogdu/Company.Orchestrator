using Company.Orchestrator.Application.Capabilities.Folder;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Reads a file from the local or UNC file system and stores it as a named artifact
/// in the WorkflowContext. Downstream steps (e.g. excel.read) can reference it by name.
///
/// Config keys:
///   sourcePath    (required) — absolute or UNC path, supports {{variable}} interpolation
///                              e.g. "C:\Temp\input.xlsx" or "\\server\share\data.xlsx"
///   artifactName  (optional) — logical name for the artifact in the workflow context
///                              (default: the file name without the full path)
///
/// Produces:
///   artifact      — stored in WorkflowContext.Artifacts[artifactName]
///   {artifactName}_path      — original source path (variable)
///   {artifactName}_sizeBytes — byte count of the read file (variable)
///
/// JobLog:
///   The WorkflowEngine writes a Start and Complete/Fail JobLog entry per step.
///   Step OutputData records: "Read {N} bytes from '{path}'"
/// </summary>
public sealed class FolderReadFileStepHandler : IStepHandler
{
    private readonly ILogger<FolderReadFileStepHandler> _logger;
    public string HandlerType => "folder.read-file";

    public FolderReadFileStepHandler(ILogger<FolderReadFileStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("sourcePath", out var sourcePathRaw) || sourcePathRaw is null)
            return StepResult.Fail("FolderReadFile: 'sourcePath' is required.");

        var sourcePath   = context.Interpolate(sourcePathRaw.ToString()!);
        var artifactName = config.GetValueOrDefault("artifactName")?.ToString()
                           ?? Path.GetFileName(sourcePath);

        _logger.LogInformation(
            "FolderReadFile: reading '{Path}' → context artifact '{Name}'",
            sourcePath, artifactName);

        var folder = context.GetCapability<ISharedFolderCapability>();

        if (!await folder.FileExistsAsync(sourcePath, cancellationToken))
            return StepResult.Fail(
                $"FolderReadFile: file not found: '{sourcePath}'");

        var artifact = await folder.ReadFileAsync(sourcePath, cancellationToken);

        // Override the artifact's logical name so downstream steps use the expected key
        artifact = artifact with { Name = artifactName };

        // Also register it directly in the context so the next step can reference it immediately
        context.Artifacts[artifactName] = artifact;

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                [$"{artifactName}_path"]      = sourcePath,
                [$"{artifactName}_sizeBytes"] = artifact.SizeBytes
            },
            artifacts: new List<Application.Artifacts.ArtifactReference> { artifact },
            outputData: $"Read {artifact.SizeBytes:N0} bytes from '{sourcePath}' → artifact '{artifactName}'");
    }
}
