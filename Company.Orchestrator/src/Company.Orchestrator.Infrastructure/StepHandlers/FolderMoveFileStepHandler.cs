using Company.Orchestrator.Application.Capabilities.Folder;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Moves (renames) a file on the local or UNC file system.
/// Creates missing destination directories. Cross-device moves use copy+delete.
///
/// Config keys:
///   sourcePath          (required) — source path, supports {{variable}} interpolation
///   destinationPath     (required) — destination path, supports {{variable}} interpolation
///   overwrite           (optional) — "true" to replace if destination exists (default: "false")
///   createDestinationDir(optional) — "true" ensures destination dir exists (default: "true")
///
/// Output variables:
///   movedFrom   — resolved source path
///   movedTo     — resolved destination path
///
/// JobLog: engine logs start/complete. OutputData: "Moved '{src}' → '{dst}'"
/// </summary>
public sealed class FolderMoveFileStepHandler : IStepHandler
{
    private readonly ILogger<FolderMoveFileStepHandler> _logger;
    public string HandlerType => "folder.move-file";

    public FolderMoveFileStepHandler(ILogger<FolderMoveFileStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("sourcePath", out var srcRaw) || srcRaw is null)
            return StepResult.Fail("FolderMoveFile: 'sourcePath' is required.");

        if (!config.TryGetValue("destinationPath", out var dstRaw) || dstRaw is null)
            return StepResult.Fail("FolderMoveFile: 'destinationPath' is required.");

        var src       = context.Interpolate(srcRaw.ToString()!);
        var dst       = context.Interpolate(dstRaw.ToString()!);
        var overwrite = string.Equals(
            config.GetValueOrDefault("overwrite")?.ToString(), "true",
            StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "FolderMoveFile: '{Src}' → '{Dst}' (overwrite={O})", src, dst, overwrite);

        var folder = context.GetCapability<ISharedFolderCapability>();

        if (!await folder.FileExistsAsync(src, cancellationToken))
            return StepResult.Fail($"FolderMoveFile: source file not found: '{src}'");

        await folder.MoveFileAsync(src, dst, overwrite, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["movedFrom"] = src,
                ["movedTo"]   = dst
            },
            outputData: $"Moved '{src}' → '{dst}'");
    }
}
