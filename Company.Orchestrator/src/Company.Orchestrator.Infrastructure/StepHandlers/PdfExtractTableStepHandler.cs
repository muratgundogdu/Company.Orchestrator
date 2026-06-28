using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Exceptions;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Extracts simple text-based tables from PDF artifacts into DataTable JSON variables.
/// </summary>
public sealed class PdfExtractTableStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<PdfExtractTableStepHandler> _logger;

    public string HandlerType => "pdf.extract-table";

    public PdfExtractTableStepHandler(IArtifactStore store, ILogger<PdfExtractTableStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "pdf.extract-table";
        var config = context.StepDefinition.Config;

        var inputArtifactName = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "inputArtifactName"));
        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail($"{stepType}: 'inputArtifactName' is required.");

        var outputVar = ZipStepHandlerHelpers.GetString(config, "outputVariable").Trim();
        if (string.IsNullOrEmpty(outputVar))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");

        var pageRange           = context.Interpolate(ZipStepHandlerHelpers.GetString(config, "pageRange"));
        var parserMode          = ZipStepHandlerHelpers.GetString(config, "parserMode", "auto");
        var delimiter           = ZipStepHandlerHelpers.GetString(config, "delimiter");
        var tableIndex          = ParseInt(config.GetValueOrDefault("tableIndex"), defaultValue: 0);
        var hasHeader           = ZipStepHandlerHelpers.GetBool(config, "hasHeader", defaultValue: true);
        var normalizeWhitespace = ZipStepHandlerHelpers.GetBool(config, "normalizeWhitespace", defaultValue: true);
        var failIfNoTable       = ZipStepHandlerHelpers.GetBool(config, "failIfNoTable", defaultValue: true);

        if (tableIndex < 0)
            return StepResult.Fail($"{stepType}: 'tableIndex' must be >= 0.");

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

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            var noTextMessage =
                $"{stepType}: no text found in PDF '{inputArtifactName}' (text-based PDF required).";
            return failIfNoTable ? StepResult.Fail(noTextMessage) : StepResult.Ok(
                output: EmptyTableOutput(outputVar),
                outputData: noTextMessage);
        }

        IReadOnlyList<string> columns;
        IReadOnlyList<Dictionary<string, string>> rows;
        try
        {
            var parser = new PdfTableParser(stepType, parserMode, delimiter, hasHeader);
            (columns, rows) = parser.Parse(extractedText, tableIndex);
        }
        catch (InvalidOperationException ex)
        {
            if (failIfNoTable)
                return StepResult.Fail(ex.Message);

            _logger.LogWarning("{StepType}: {Message}", stepType, ex.Message);
            return StepResult.Ok(
                output: EmptyTableOutput(outputVar),
                outputData: ex.Message);
        }

        _logger.LogInformation(
            "{StepType}: artifact='{Artifact}', pageCount={PageCount}, parserMode={ParserMode}, " +
            "tableIndex={TableIndex}, rowCount={RowCount}, columnCount={ColumnCount}",
            stepType,
            inputArtifactName,
            pageCount,
            parserMode,
            tableIndex,
            rows.Count,
            columns.Count);

        if (failIfNoTable && rows.Count == 0)
        {
            return StepResult.Fail(
                $"{stepType}: no table rows found in PDF '{inputArtifactName}'.");
        }

        return PdfStepHandlerHelpers.BuildDataTableOutput(outputVar, columns, rows);
    }

    private static Dictionary<string, object> EmptyTableOutput(string outputVar) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]              = "[]",
            [$"{outputVar}_count"]   = 0,
            [$"{outputVar}_columns"] = "[]",
            [$"{outputVar}_first"]   = "{}",
        };

    private static int ParseInt(object? value, int defaultValue)
    {
        if (value is null)
            return defaultValue;

        if (value is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var n))
                return n;
            if (je.ValueKind == System.Text.Json.JsonValueKind.String &&
                int.TryParse(je.GetString(), out var parsed))
                return parsed;
            return defaultValue;
        }

        return int.TryParse(value.ToString(), out var result) ? result : defaultValue;
    }
}
