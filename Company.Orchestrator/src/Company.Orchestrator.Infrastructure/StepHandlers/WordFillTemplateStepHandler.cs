using System.Diagnostics;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Xceed.Words.NET;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Fills a Word (.docx) template by replacing {{placeholder}} tokens with workflow variables.
/// </summary>
public sealed class WordFillTemplateStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<WordFillTemplateStepHandler> _logger;

    public string HandlerType => "word.fill-template";

    public WordFillTemplateStepHandler(IArtifactStore store, ILogger<WordFillTemplateStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "word.fill-template";
        var config = context.StepDefinition.Config;
        var stopwatch = Stopwatch.StartNew();

        var inputArtifactName = context.Interpolate(
            ZipStepHandlerHelpers.GetString(config, "inputArtifactName"));
        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail($"{stepType}: 'inputArtifactName' is required.");

        var outputName = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "outputName"));
        if (string.IsNullOrWhiteSpace(outputName))
            return StepResult.Fail($"{stepType}: 'outputName' is required.");

        if (!outputName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            outputName += ".docx";

        var outputVariable = ZipStepHandlerHelpers.GetString(config, "outputVariable").Trim();
        if (string.IsNullOrEmpty(outputVariable))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");

        var strictMode = ZipStepHandlerHelpers.GetBool(config, "strictMode", defaultValue: false);

        if (!context.HasArtifact(inputArtifactName))
        {
            return StepResult.Fail(
                $"{stepType}: input artifact '{inputArtifactName}' not found in context.");
        }

        var inputArtifact = context.GetArtifact(inputArtifactName);
        byte[] templateBytes;
        try
        {
            templateBytes = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);
        }
        catch (Exception ex)
        {
            return StepResult.Fail(
                $"{stepType}: failed to read template artifact '{inputArtifactName}': {ex.Message}");
        }

        DocX document;
        try
        {
            using var loadStream = new MemoryStream(templateBytes);
            document = DocX.Load(loadStream);
        }
        catch (Exception ex)
        {
            return StepResult.Fail(
                $"{stepType}: invalid or corrupted DOCX template '{inputArtifactName}': {ex.Message}");
        }

        using (document)
        {
            var placeholders = WordStepHandlerHelpers.CollectDocumentPlaceholders(document);
            var missing      = new List<string>();
            var replaced     = 0;

            foreach (var placeholder in placeholders)
            {
                var (found, value) = WordStepHandlerHelpers.ResolvePlaceholder(context, placeholder);
                if (!found)
                {
                    if (strictMode)
                    {
                        return StepResult.Fail(
                            $"{stepType}: Variable not found: {placeholder}");
                    }

                    missing.Add(placeholder);
                    WordStepHandlerHelpers.ReplacePlaceholder(document, placeholder, string.Empty);
                    continue;
                }

                WordStepHandlerHelpers.ReplacePlaceholder(document, placeholder, value);
                replaced++;
            }

            byte[] outputBytes;
            try
            {
                using var saveStream = new MemoryStream();
                document.SaveAs(saveStream);
                outputBytes = saveStream.ToArray();
            }
            catch (Exception ex)
            {
                return StepResult.Fail(
                    $"{stepType}: failed to save generated document '{outputName}': {ex.Message}");
            }

            var id = Guid.NewGuid();
            using var artifactStream = new MemoryStream(outputBytes);
            string path;
            try
            {
                path = await _store.SaveAsync(id, outputName, artifactStream, cancellationToken);
            }
            catch (Exception ex)
            {
                return StepResult.Fail(
                    $"{stepType}: failed to persist artifact '{outputName}': {ex.Message}");
            }

            var artifact = new ArtifactReference
            {
                Id          = id,
                Name        = outputName,
                ContentType = WordStepHandlerHelpers.DocxMime,
                StoragePath = path,
                SizeBytes   = outputBytes.Length,
                Metadata    = new Dictionary<string, string>
                {
                    ["operation"]            = stepType,
                    ["templateArtifact"]     = inputArtifactName,
                    ["placeholdersReplaced"] = replaced.ToString(),
                    ["missingPlaceholders"]  = string.Join(",", missing),
                }
            };

            stopwatch.Stop();

            _logger.LogInformation(
                "{StepType}: template='{Template}', outputName='{OutputName}', " +
                "placeholderCount={PlaceholderCount}, replacedCount={ReplacedCount}, " +
                "missingCount={MissingCount}, durationMs={DurationMs}",
                stepType,
                inputArtifactName,
                outputName,
                placeholders.Count,
                replaced,
                missing.Count,
                stopwatch.ElapsedMilliseconds);

            var missingText = missing.Count == 0
                ? string.Empty
                : string.Join(", ", missing);

            return StepResult.Ok(
                output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    [outputVariable]         = outputName,
                    ["outputName"]           = outputName,
                    ["placeholdersReplaced"] = replaced,
                    ["missingPlaceholders"]  = missingText,
                },
                artifacts: [artifact],
                outputData:
                    $"Generated Word document '{outputName}' from template '{inputArtifactName}' " +
                    $"({replaced}/{placeholders.Count} placeholder(s) replaced).");
        }
    }
}
