using System.Text;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Reads a JSON artifact and extracts values using optional JSON Path.
/// </summary>
public sealed class JsonReadFileStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<JsonReadFileStepHandler> _logger;

    public string HandlerType => "json.read-file";

    public JsonReadFileStepHandler(IArtifactStore store, ILogger<JsonReadFileStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "json.read-file";
        var config = context.StepDefinition.Config;

        var inputArtifactName = context.Interpolate(JsonStepHandlerHelpers.GetString(config, "inputArtifactName"));
        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail($"{stepType}: 'inputArtifactName' is required.");

        var outputVariable = JsonStepHandlerHelpers.GetString(config, "outputVariable").Trim();
        if (string.IsNullOrEmpty(outputVariable))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");

        var outputMode = JsonStepHandlerHelpers.GetString(config, "outputMode", "json").Trim().ToLowerInvariant();
        if (!JsonStepHandlerHelpers.SupportedOutputModes.Contains(outputMode))
        {
            return StepResult.Fail(
                $"{stepType}: 'outputMode' must be 'value', 'json', or 'table'.");
        }

        var path = JsonStepHandlerHelpers.GetString(config, "path");

        if (!context.HasArtifact(inputArtifactName))
            return StepResult.Fail($"{stepType}: input artifact '{inputArtifactName}' not found in context.");

        var inputArtifact = context.GetArtifact(inputArtifactName);
        string jsonText;
        try
        {
            var bytes = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);
            jsonText  = Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            return StepResult.Fail(
                $"{stepType}: failed to read artifact '{inputArtifactName}': {ex.Message}");
        }

        if (!JsonStepHandlerHelpers.TryParseJson(jsonText, out var root, out var parseError))
        {
            return StepResult.Fail(
                $"{stepType}: invalid JSON in artifact '{inputArtifactName}': {parseError}");
        }

        if (!JsonStepHandlerHelpers.TryResolveMatches(root, path, out var matches, out var pathError))
        {
            return StepResult.Fail($"{stepType}: {pathError}");
        }

        var arrayCount = JsonStepHandlerHelpers.CountArrayItems(matches);

        _logger.LogInformation(
            "{StepType}: artifact='{Artifact}', path='{Path}', outputMode={OutputMode}, " +
            "outputVariable='{OutputVariable}', count={Count}",
            stepType,
            inputArtifactName,
            string.IsNullOrWhiteSpace(path) ? "(root)" : JsonStepHandlerHelpers.NormalizePath(path),
            outputMode,
            outputVariable,
            arrayCount);

        return JsonStepHandlerHelpers.BuildResult(
            stepType,
            outputVariable,
            path,
            outputMode,
            matches);
    }
}
