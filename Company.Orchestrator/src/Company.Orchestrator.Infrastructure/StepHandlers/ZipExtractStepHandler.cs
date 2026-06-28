using System.IO.Compression;
using System.Text.Json;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Extracts files from a ZIP artifact into individual workflow artifacts.
/// </summary>
public sealed class ZipExtractStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<ZipExtractStepHandler> _logger;

    public string HandlerType => "zip.extract";

    public ZipExtractStepHandler(IArtifactStore store, ILogger<ZipExtractStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "zip.extract";
        var config = context.StepDefinition.Config;

        var inputArtifactName = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "inputArtifactName"));
        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail($"{stepType}: 'inputArtifactName' is required.");

        var outputPrefix = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "outputPrefix"));
        if (string.IsNullOrWhiteSpace(outputPrefix))
            return StepResult.Fail($"{stepType}: 'outputPrefix' is required.");

        var outputVariable = ZipStepHandlerHelpers.GetString(config, "outputVariable").Trim();
        if (string.IsNullOrEmpty(outputVariable))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");

        var filterPattern   = ZipStepHandlerHelpers.GetString(config, "filterPattern", "*.*");
        var failIfNoMatches = ZipStepHandlerHelpers.GetBool(config, "failIfNoMatches", defaultValue: false);

        if (!context.HasArtifact(inputArtifactName))
            return StepResult.Fail($"{stepType}: input artifact '{inputArtifactName}' not found in context.");

        var inputArtifact = context.GetArtifact(inputArtifactName);
        byte[] zipBytes;
        try
        {
            zipBytes = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"{stepType}: failed to read artifact '{inputArtifactName}': {ex.Message}");
        }

        var produced       = new List<ArtifactReference>();
        var artifactNames  = new List<string>();
        var usedNames      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalEntries   = 0;
        var extractedCount = 0;
        var skippedCount   = 0;

        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var archive   = new ZipArchive(zipStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                totalEntries++;

                if (string.IsNullOrEmpty(entry.Name) ||
                    ZipStepHandlerHelpers.IsDirectoryEntry(entry.FullName))
                {
                    skippedCount++;
                    continue;
                }

                if (!ZipStepHandlerHelpers.IsSafeZipEntryPath(entry.FullName))
                {
                    skippedCount++;
                    _logger.LogWarning(
                        "{StepType}: skipped unsafe ZIP entry '{Entry}'", stepType, entry.FullName);
                    continue;
                }

                var entryFileName = ZipStepHandlerHelpers.GetEntryFileName(entry.FullName);
                if (string.IsNullOrWhiteSpace(entryFileName))
                {
                    skippedCount++;
                    continue;
                }

                if (!ZipStepHandlerHelpers.MatchesWildcardPattern(entryFileName, filterPattern))
                {
                    skippedCount++;
                    continue;
                }

                var baseArtifactName = ZipStepHandlerHelpers.BuildArtifactName(outputPrefix, entryFileName);
                var artifactName = ZipStepHandlerHelpers.MakeUniqueName(baseArtifactName, usedNames);

                await using var entryStream = entry.Open();
                using var ms = new MemoryStream();
                await entryStream.CopyToAsync(ms, cancellationToken);
                var bytes = ms.ToArray();

                var id   = Guid.NewGuid();
                var path = await _store.SaveAsync(id, artifactName, new MemoryStream(bytes), cancellationToken);

                produced.Add(new ArtifactReference
                {
                    Id          = id,
                    Name        = artifactName,
                    ContentType = ZipStepHandlerHelpers.ResolveMimeType(entryFileName),
                    StoragePath = path,
                    SizeBytes   = bytes.Length,
                    Metadata    = new Dictionary<string, string>
                    {
                        ["operation"]       = "zip.extract",
                        ["sourceZip"]       = inputArtifactName,
                        ["zipEntryPath"]    = entry.FullName,
                        ["originalFileName"] = entryFileName,
                    }
                });

                artifactNames.Add(artifactName);
                extractedCount++;
            }
        }
        catch (InvalidDataException ex)
        {
            return StepResult.Fail($"{stepType}: invalid ZIP in artifact '{inputArtifactName}': {ex.Message}");
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"{stepType}: failed to extract ZIP '{inputArtifactName}': {ex.Message}");
        }

        _logger.LogInformation(
            "{StepType}: inputArtifact='{InputArtifact}', totalEntries={TotalEntries}, extracted={Extracted}, " +
            "skipped={Skipped}, filterPattern='{FilterPattern}', outputVariable='{OutputVariable}'",
            stepType,
            inputArtifactName,
            totalEntries,
            extractedCount,
            skippedCount,
            filterPattern,
            outputVariable);

        if (failIfNoMatches && extractedCount == 0)
        {
            return StepResult.Fail(
                $"{stepType}: no files matched filterPattern '{filterPattern}' in '{inputArtifactName}'.");
        }

        var output = ZipStepHandlerHelpers.BuildArtifactArrayOutputs(outputVariable, artifactNames);

        return StepResult.Ok(
            output: output,
            artifacts: produced,
            outputData:
                $"Extracted {extractedCount} file(s) from '{inputArtifactName}' " +
                $"(total entries={totalEntries}, skipped={skippedCount}).");
    }
}
