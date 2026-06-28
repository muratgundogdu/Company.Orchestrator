using System.IO.Compression;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Creates a ZIP artifact from multiple workflow artifacts.
/// </summary>
public sealed class ZipCreateStepHandler : IStepHandler
{
    private const string ZipMime = "application/zip";

    private readonly IArtifactStore _store;
    private readonly ILogger<ZipCreateStepHandler> _logger;

    public string HandlerType => "zip.create";

    public ZipCreateStepHandler(IArtifactStore store, ILogger<ZipCreateStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "zip.create";
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("inputArtifactNames", out var namesRaw) || namesRaw is null)
            return StepResult.Fail($"{stepType}: 'inputArtifactNames' is required.");

        List<string> artifactNames;
        try
        {
            artifactNames = ZipStepHandlerHelpers.ResolveArtifactNames(namesRaw, context);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"{stepType}: failed to parse 'inputArtifactNames': {ex.Message}");
        }

        if (artifactNames.Count == 0)
            return StepResult.Fail($"{stepType}: 'inputArtifactNames' must list at least one artifact.");

        var outputName = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "outputName"));
        if (string.IsNullOrWhiteSpace(outputName))
            return StepResult.Fail($"{stepType}: 'outputName' is required.");

        if (!outputName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            outputName += ".zip";

        CompressionLevel compressionLevel;
        try
        {
            compressionLevel = ZipStepHandlerHelpers.ResolveCompressionLevel(
                ZipStepHandlerHelpers.GetString(config, "compressionLevel", "optimal"));
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }

        foreach (var name in artifactNames)
        {
            if (!context.HasArtifact(name))
                return StepResult.Fail($"{stepType}: input artifact '{name}' not found in context.");
        }

        _logger.LogInformation(
            "{StepType}: inputCount={InputCount}, outputName='{OutputName}', compressionLevel={CompressionLevel}",
            stepType,
            artifactNames.Count,
            outputName,
            compressionLevel);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var artifactName in artifactNames)
            {
                var sourceArtifact = context.GetArtifact(artifactName);
                var bytes          = await _store.ReadAllBytesAsync(sourceArtifact.StoragePath, cancellationToken);
                var entryName      = ZipStepHandlerHelpers.MakeUniqueZipEntryName(
                    sourceArtifact.Name,
                    usedEntryNames);

                var entry = archive.CreateEntry(entryName, compressionLevel);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(bytes, cancellationToken);
            }
        }

        var zipBytes = zipStream.ToArray();
        var id       = Guid.NewGuid();
        using var saveStream = new MemoryStream(zipBytes);
        var path = await _store.SaveAsync(id, outputName, saveStream, cancellationToken);

        var artifact = new ArtifactReference
        {
            Id          = id,
            Name        = outputName,
            ContentType = ZipMime,
            StoragePath = path,
            SizeBytes   = zipBytes.Length,
            Metadata    = new Dictionary<string, string>
            {
                ["operation"]    = "zip.create",
                ["filesZipped"]  = artifactNames.Count.ToString(),
                ["sourceArtifacts"] = string.Join(",", artifactNames),
            }
        };

        _logger.LogInformation(
            "{StepType}: outputName='{OutputName}', filesZipped={FilesZipped}, zipSizeBytes={ZipSizeBytes}",
            stepType,
            outputName,
            artifactNames.Count,
            zipBytes.Length);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["outputName"]   = outputName,
                ["filesZipped"]  = artifactNames.Count,
                ["zipSizeBytes"] = zipBytes.Length,
            },
            artifacts: [artifact],
            outputData:
                $"Created ZIP '{outputName}' with {artifactNames.Count} file(s) ({zipBytes.Length:N0} bytes).");
    }
}
