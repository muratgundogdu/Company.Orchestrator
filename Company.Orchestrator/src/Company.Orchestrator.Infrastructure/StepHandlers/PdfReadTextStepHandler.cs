using System.Diagnostics;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Exceptions;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Extracts text from a PDF artifact (text-based PDFs only; no OCR).
/// </summary>
public sealed class PdfReadTextStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<PdfReadTextStepHandler> _logger;

    public string HandlerType => "pdf.read-text";

    public PdfReadTextStepHandler(IArtifactStore store, ILogger<PdfReadTextStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "pdf.read-text";
        var config = context.StepDefinition.Config;
        var stopwatch = Stopwatch.StartNew();

        var inputArtifactName = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "inputArtifactName"));
        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail($"{stepType}: 'inputArtifactName' is required.");

        var outputVar = ZipStepHandlerHelpers.GetString(config, "outputVariable").Trim();
        if (string.IsNullOrEmpty(outputVar))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");

        var pageRange           = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "pageRange"));
        var normalizeWhitespace = ZipStepHandlerHelpers.GetBool(config, "normalizeWhitespace", defaultValue: true);
        var failIfEmpty         = ZipStepHandlerHelpers.GetBool(config, "failIfEmpty", defaultValue: false);

        if (!context.HasArtifact(inputArtifactName))
            return StepResult.Fail($"{stepType}: input artifact '{inputArtifactName}' not found in context.");

        var inputArtifact = context.GetArtifact(inputArtifactName);
        byte[] bytes;
        try
        {
            bytes = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"{stepType}: failed to read artifact '{inputArtifactName}': {ex.Message}");
        }

        string extractedText;
        int pageCount;
        try
        {
            (extractedText, pageCount) = PdfStepHandlerHelpers.ExtractTextFromPdf(
                bytes, pageRange, normalizeWhitespace, stepType);
        }
        catch (PdfDocumentEncryptedException)
        {
            return StepResult.Fail(
                $"{stepType}: PDF '{inputArtifactName}' is encrypted or password protected.");
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"{stepType}: invalid PDF in artifact '{inputArtifactName}': {ex.Message}");
        }

        stopwatch.Stop();

        if (failIfEmpty && string.IsNullOrWhiteSpace(extractedText))
        {
            return StepResult.Fail(
                $"{stepType}: no text found in PDF '{inputArtifactName}' for pageRange '{pageRangeOrAll(pageRange)}'.");
        }

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            _logger.LogWarning(
                "{StepType}: no text extracted from '{Artifact}' (scanned/image PDF or empty pages?)",
                stepType, inputArtifactName);
        }

        _logger.LogInformation(
            "{StepType}: artifact='{Artifact}', pageCount={PageCount}, characters={CharacterCount}, durationMs={DurationMs}",
            stepType,
            inputArtifactName,
            pageCount,
            extractedText.Length,
            stopwatch.ElapsedMilliseconds);

        return StepResult.Ok(
            output: BuildOutput(outputVar, extractedText, pageCount),
            outputData:
                $"Extracted {extractedText.Length} character(s) from {pageCount} page(s) of '{inputArtifactName}' " +
                $"in {stopwatch.ElapsedMilliseconds}ms.");
    }

    private static Dictionary<string, object> BuildOutput(string outputVar, string text, int pageCount)
    {
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]                  = text,
            [$"{outputVar}_length"]      = text.Length,
            [$"{outputVar}_pageCount"]   = pageCount,
            [$"{outputVar}_first500"]    = PdfStepHandlerHelpers.TakePrefix(text, 500),
            [$"{outputVar}_first1000"]   = PdfStepHandlerHelpers.TakePrefix(text, 1000),
        };
    }

    private static string pageRangeOrAll(string pageRange) =>
        string.IsNullOrWhiteSpace(pageRange) ? "all" : pageRange;
}
