using System.Text;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Writes a workflow variable to a JSON artifact.
/// </summary>
public sealed class JsonWriteFileStepHandler : IStepHandler
{
    private const string JsonMime = "application/json";

    private readonly IArtifactStore _store;
    private readonly ILogger<JsonWriteFileStepHandler> _logger;

    public string HandlerType => "json.write-file";

    public JsonWriteFileStepHandler(IArtifactStore store, ILogger<JsonWriteFileStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "json.write-file";
        var config = context.StepDefinition.Config;

        var sourceVariable = JsonStepHandlerHelpers.NormalizeVarName(
            JsonStepHandlerHelpers.GetString(config, "sourceVariable"));
        if (string.IsNullOrWhiteSpace(sourceVariable))
            return StepResult.Fail($"{stepType}: 'sourceVariable' is required.");

        var outputName = context.Interpolate(JsonStepHandlerHelpers.GetString(config, "outputName"));
        if (string.IsNullOrWhiteSpace(outputName))
            return StepResult.Fail($"{stepType}: 'outputName' is required.");

        var prettyPrint = JsonStepHandlerHelpers.GetBool(config, "prettyPrint", defaultValue: true);

        if (!context.Variables.TryGetValue(sourceVariable, out var sourceRaw))
        {
            return StepResult.Fail(
                $"{stepType}: source variable '{sourceVariable}' not found in workflow context.");
        }

        var sourceText = JsonStepHandlerHelpers.VariableToString(sourceRaw).Trim();
        if (string.IsNullOrEmpty(sourceText))
            return StepResult.Fail($"{stepType}: source variable '{sourceVariable}' is empty.");

        string jsonOutput;
        try
        {
            jsonOutput = SerializeSource(sourceText, prettyPrint);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"{stepType}: failed to serialize JSON: {ex.Message}");
        }

        var id    = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes(jsonOutput);
        using var ms = new MemoryStream(bytes);
        var path = await _store.SaveAsync(id, outputName, ms, cancellationToken);

        var artifact = new ArtifactReference
        {
            Id          = id,
            Name        = outputName,
            ContentType = JsonMime,
            StoragePath = path,
            SizeBytes   = bytes.Length,
            Metadata    = new Dictionary<string, string>
            {
                ["operation"]      = "json.write-file",
                ["sourceVariable"] = sourceVariable,
                ["prettyPrint"]    = prettyPrint.ToString(),
            }
        };

        _logger.LogInformation(
            "{StepType}: sourceVariable='{SourceVariable}', outputName='{OutputName}', bytesWritten={BytesWritten}",
            stepType,
            sourceVariable,
            outputName,
            bytes.Length);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["outputName"]   = outputName,
                ["bytesWritten"] = bytes.Length,
            },
            artifacts: [artifact],
            outputData:
                $"Wrote JSON artifact '{outputName}' ({bytes.Length} byte(s)) from '{sourceVariable}'.");
    }

    private static string SerializeSource(string sourceText, bool prettyPrint)
    {
        var formatting = prettyPrint ? Formatting.Indented : Formatting.None;

        try
        {
            var token = JToken.Parse(sourceText);
            return token.ToString(formatting);
        }
        catch (JsonReaderException)
        {
            return JsonConvert.SerializeObject(sourceText, formatting);
        }
    }
}
