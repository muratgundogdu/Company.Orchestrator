using Company.Orchestrator.Application.Capabilities.Folder;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Copies a file from one path to another on the local or UNC file system.
/// The original file is not modified. Creates missing destination directories.
///
/// Config keys:
///   sourcePath        (required) — source file path, supports {{variable}} interpolation
///   destinationPath   (required) — destination file path, supports {{variable}} interpolation
///   overwrite         (optional) — "true" to overwrite if destination exists (default: "false")
///
/// Output variables:
///   copiedFrom    — resolved source path
///   copiedTo      — resolved destination path
///
/// JobLog: engine logs start/complete. OutputData: "Copied '{src}' → '{dst}'"
/// </summary>
public sealed class FolderCopyFileStepHandler : IStepHandler
{
    private readonly ILogger<FolderCopyFileStepHandler> _logger;
    public string HandlerType => "folder.copy-file";

    public FolderCopyFileStepHandler(ILogger<FolderCopyFileStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("sourcePath", out var srcRaw) || srcRaw is null)
            return StepResult.Fail("FolderCopyFile: 'sourcePath' is required.");

        if (!config.TryGetValue("destinationPath", out var dstRaw) || dstRaw is null)
            return StepResult.Fail("FolderCopyFile: 'destinationPath' is required.");

        var src       = context.Interpolate(srcRaw.ToString()!);
        var dst       = context.Interpolate(dstRaw.ToString()!);
        var overwrite = string.Equals(
            config.GetValueOrDefault("overwrite")?.ToString(), "true",
            StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "FolderCopyFile: '{Src}' → '{Dst}' (overwrite={O})", src, dst, overwrite);

        var folder = context.GetCapability<ISharedFolderCapability>();

        if (!await folder.FileExistsAsync(src, cancellationToken))
            return StepResult.Fail($"FolderCopyFile: source file not found: '{src}'");

        await folder.CopyFileAsync(src, dst, overwrite, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["copiedFrom"] = src,
                ["copiedTo"]   = dst
            },
            outputData: $"Copied '{src}' → '{dst}'");
    }
}
