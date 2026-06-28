using System.Text.Json;
using Company.Orchestrator.Application.Capabilities.Folder;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Lists files in a folder and stores the result as a JSON array in a workflow variable.
/// Each element is a serialized FileEntry object (name, fullPath, directory, extension, sizeBytes, lastModified).
///
/// Config keys:
///   folderPath      (required) — directory to scan, supports {{variable}} interpolation
///   searchPattern   (optional) — glob pattern (default: "*.*")  e.g. "*.xlsx", "report_*.pdf"
///   pattern         (legacy)   — alias for searchPattern
///   recursive       (optional) — "true" to scan sub-folders (default: "false")
///   outputVariable  (required) — variable name for the JSON result
///
/// Output variables:
///   {outputVariable}        — JSON array of FileEntry objects
///   {outputVariable}_count  — number of files found (int)
///   {outputVariable}_first  — first FileEntry as JSON (or "{}")
///   {outputVariable}_0 … {outputVariable}_9 — indexed FileEntry JSON (max 10)
/// </summary>
public sealed class FolderListFilesStepHandler : IStepHandler
{
    private readonly ILogger<FolderListFilesStepHandler> _logger;
    public string HandlerType => "folder.list-files";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FolderListFilesStepHandler(ILogger<FolderListFilesStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("folderPath", out var folderRaw) || folderRaw is null)
            return StepResult.Fail("FolderListFiles: 'folderPath' is required.");

        if (!config.TryGetValue("outputVariable", out var outputRaw) ||
            string.IsNullOrWhiteSpace(outputRaw?.ToString()))
            return StepResult.Fail("FolderListFiles: 'outputVariable' is required.");

        var folderPath    = context.Interpolate(folderRaw.ToString()!);
        var searchPattern = ResolveSearchPattern(config);
        var recursive     = string.Equals(
            config.GetValueOrDefault("recursive")?.ToString(), "true",
            StringComparison.OrdinalIgnoreCase);
        var outputVar     = outputRaw.ToString()!.Trim();

        _logger.LogInformation(
            "FolderListFiles: folder='{Folder}' pattern='{Pattern}' recursive={Recursive}",
            folderPath, searchPattern, recursive);

        var folder  = context.GetCapability<ISharedFolderCapability>();
        var entries = await folder.ListFilesAsync(
            folderPath, searchPattern, recursive, cancellationToken);

        _logger.LogInformation(
            "FolderListFiles: found {Count} file(s) in '{Folder}'",
            entries.Count, folderPath);

        return BuildResult(outputVar, folderPath, searchPattern, recursive, entries);
    }

    private static string ResolveSearchPattern(IReadOnlyDictionary<string, object> config)
    {
        var searchPattern = config.GetValueOrDefault("searchPattern")?.ToString();
        if (!string.IsNullOrWhiteSpace(searchPattern))
            return searchPattern;

        var legacyPattern = config.GetValueOrDefault("pattern")?.ToString();
        if (!string.IsNullOrWhiteSpace(legacyPattern))
            return legacyPattern;

        return "*.*";
    }

    private static StepResult BuildResult(
        string outputVar,
        string folderPath,
        string searchPattern,
        bool recursive,
        IReadOnlyList<FileEntry> entries)
    {
        var json     = JsonSerializer.Serialize(entries, SerializerOptions);
        var firstJson = entries.Count > 0
            ? JsonSerializer.Serialize(entries[0], SerializerOptions)
            : "{}";

        var output = new Dictionary<string, object>
        {
            [outputVar]              = json,
            [$"{outputVar}_count"]   = entries.Count,
            [$"{outputVar}_first"]   = firstJson,
        };

        for (var i = 0; i < Math.Min(entries.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonSerializer.Serialize(entries[i], SerializerOptions);

        return StepResult.Ok(
            output: output,
            outputData:
                $"Found {entries.Count} file(s) in '{folderPath}' " +
                $"(pattern='{searchPattern}', recursive={recursive})");
    }
}
