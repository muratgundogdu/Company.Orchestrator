using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.File;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Reads a file from the file system and stores it as a named artifact.
///
/// Config keys:
///   sourcePath   — path to the source file (supports {{variable}} interpolation)
///   artifactName — name to assign in WorkflowContext.Artifacts (default: filename)
///   persistent   — bool, whether to keep artifact after process completes (default: true)
/// </summary>
public sealed class FileReadStepHandler : IStepHandler
{
    private readonly ILogger<FileReadStepHandler> _logger;
    public string HandlerType => "file.read";

    public FileReadStepHandler(ILogger<FileReadStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("sourcePath", out var sourcePathRaw) || sourcePathRaw is null)
            return StepResult.Fail("FileReadStepHandler: 'sourcePath' is required.");

        var sourcePath = context.Interpolate(sourcePathRaw.ToString()!);
        var artifactName = config.GetValueOrDefault("artifactName")?.ToString()
                           ?? Path.GetFileName(sourcePath);

        _logger.LogInformation("FileReadStepHandler: reading '{Path}' as artifact '{Name}'", sourcePath, artifactName);

        var file = context.GetCapability<IFileCapability>();

        if (!await file.ExistsAsync(sourcePath, cancellationToken))
            return StepResult.Fail($"FileReadStepHandler: file not found: '{sourcePath}'");

        var artifact = await file.ReadFileAsync(sourcePath, cancellationToken);

        // Override name so downstream steps can refer to it predictably
        artifact = artifact with { Name = artifactName };

        context.Artifacts[artifactName] = artifact;

        return StepResult.WithArtifact(artifact,
            $"Read {artifact.SizeBytes:N0} bytes from '{sourcePath}'");
    }
}
