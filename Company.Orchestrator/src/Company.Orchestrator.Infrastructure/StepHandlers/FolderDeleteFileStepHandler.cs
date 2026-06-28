using Company.Orchestrator.Application.Capabilities.Folder;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Deletes a file from the local or UNC file system.
/// Silently succeeds if the file does not exist (idempotent).
///
/// Config keys:
///   path              (required) — file to delete, supports {{variable}} interpolation
///   failIfNotFound    (optional) — "true" to fail when file is missing (default: "false")
///
/// Output variables:
///   deletedPath       — resolved path that was (attempted to be) deleted
///   fileExisted       — "true"/"false" — whether the file was actually present
///
/// JobLog: engine logs start/complete. OutputData: "Deleted '{path}'" or "File not found: '{path}'"
/// </summary>
public sealed class FolderDeleteFileStepHandler : IStepHandler
{
    private readonly ILogger<FolderDeleteFileStepHandler> _logger;
    public string HandlerType => "folder.delete-file";

    public FolderDeleteFileStepHandler(ILogger<FolderDeleteFileStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("path", out var pathRaw) || pathRaw is null)
            return StepResult.Fail("FolderDeleteFile: 'path' is required.");

        var path         = context.Interpolate(pathRaw.ToString()!);
        var failIfMissing = string.Equals(
            config.GetValueOrDefault("failIfNotFound")?.ToString(), "true",
            StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("FolderDeleteFile: deleting '{Path}'", path);

        var folder    = context.GetCapability<ISharedFolderCapability>();
        var existed   = await folder.FileExistsAsync(path, cancellationToken);

        if (!existed && failIfMissing)
            return StepResult.Fail($"FolderDeleteFile: file not found: '{path}'");

        await folder.DeleteFileAsync(path, cancellationToken);

        var msg = existed
            ? $"Deleted '{path}'"
            : $"File not found (no-op): '{path}'";

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["deletedPath"] = path,
                ["fileExisted"] = existed.ToString().ToLower()
            },
            outputData: msg);
    }
}
