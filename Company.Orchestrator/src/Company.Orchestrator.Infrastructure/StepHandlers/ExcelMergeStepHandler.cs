using System.Text.Json;
using ClosedXML.Excel;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Merges multiple Excel workbook artifacts into a single output workbook.
///
/// Config keys:
///   inputArtifactNames  (required) — JSON array, comma-separated string, or {{variable}}
///   outputName          (required) — output artifact name (.xlsx appended if absent)
///   mergeMode           (required) — "appendRows" (only mode supported)
///   sourceSheetName     (required) — sheet to read from each input workbook
///   targetSheetName     (required) — sheet to write merged rows into
///   includeHeaderOnce   (optional) — when true, row 1 is copied only from the first file
///
/// Output variables:
///   mergedArtifactName, mergedArtifact_sheetNames, mergedArtifact_rowCount,
///   mergedArtifact_colCount, mergedArtifact_sourceCount
/// </summary>
public sealed class ExcelMergeStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelMergeStepHandler> _logger;

    public string HandlerType => "excel.merge";

    public ExcelMergeStepHandler(IArtifactStore store, ILogger<ExcelMergeStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("inputArtifactNames", out var namesRaw) || namesRaw is null)
            return StepResult.Fail("excel.merge: 'inputArtifactNames' is required.");

        List<string> artifactNames;
        try { artifactNames = ResolveArtifactNames(namesRaw, context); }
        catch (Exception ex)
        {
            return StepResult.Fail($"excel.merge: failed to parse 'inputArtifactNames': {ex.Message}");
        }

        if (artifactNames.Count == 0)
            return StepResult.Fail("excel.merge: 'inputArtifactNames' must list at least one artifact.");

        var outputNameRaw = config.GetValueOrDefault("outputName")?.ToString() ?? "merged-excel";
        var outputName    = context.Interpolate(outputNameRaw);
        if (!outputName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            outputName += ".xlsx";

        var mergeMode = config.GetValueOrDefault("mergeMode")?.ToString();
        if (mergeMode is not null
            && !string.Equals(mergeMode, "appendRows", StringComparison.OrdinalIgnoreCase))
            return StepResult.Fail($"excel.merge: unsupported mergeMode '{mergeMode}'. Supported: appendRows.");

        var sourceSheet = context.Interpolate(
            config.GetValueOrDefault("sourceSheetName")?.ToString() ?? "Data");
        var targetSheet = context.Interpolate(
            config.GetValueOrDefault("targetSheetName")?.ToString() ?? "Merged");
        var includeHeaderOnce = GetBool(config, "includeHeaderOnce", defaultValue: true);
        var configStartRow    = ParseInt(config.GetValueOrDefault("startRow"), defaultValue: 1);
        if (configStartRow < 1)
            return StepResult.Fail("excel.merge: 'startRow' must be at least 1.");

        foreach (var name in artifactNames)
        {
            if (!context.HasArtifact(name))
                return StepResult.Fail($"excel.merge: input artifact '{name}' not found in context.");
        }

        _logger.LogInformation(
            "excel.merge: input file count={Count}, merging from sheet '{Source}' into '{Target}' → '{Output}'",
            artifactNames.Count, sourceSheet, targetSheet, outputName);

        using var outWb = new XLWorkbook();
        var tgtWs = outWb.Worksheets.Add(targetSheet);
        var targetRow = 1;
        var totalRowsWritten = 0;

        for (var i = 0; i < artifactNames.Count; i++)
        {
            var artifactName = artifactNames[i];
            var inputArtifact = context.GetArtifact(artifactName);
            var bytes         = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);

            using var inWb = new XLWorkbook(new MemoryStream(bytes));
            var srcWs = inWb.Worksheets.FirstOrDefault(
                s => s.Name.Equals(sourceSheet, StringComparison.OrdinalIgnoreCase));
            if (srcWs is null)
            {
                return StepResult.Fail(
                    $"excel.merge: sheet '{sourceSheet}' not found in artifact '{artifactName}'. " +
                    $"Available: [{string.Join(", ", inWb.Worksheets.Select(s => s.Name))}]");
            }

            var lastRow = srcWs.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow == 0)
            {
                _logger.LogInformation(
                    "excel.merge: artifact '{Name}' sheet '{Sheet}' is empty — skipping",
                    artifactName, sourceSheet);
                continue;
            }

            var fileStartRow = includeHeaderOnce && i > 0
                ? Math.Max(configStartRow, 2)
                : configStartRow;

            var lastCol = srcWs.LastColumnUsed()?.ColumnNumber() ?? 1;
            var rowsCopied = 0;

            for (var r = fileStartRow; r <= lastRow; r++)
            {
                CopyRow(srcWs, tgtWs, r, targetRow, lastCol);
                targetRow++;
                rowsCopied++;
            }

            totalRowsWritten += rowsCopied;

            _logger.LogInformation(
                "excel.merge: file '{Name}' — copied {RowsCopied} row(s) (source rows {Start}-{End})",
                artifactName, rowsCopied, fileStartRow, lastRow);
        }

        var sheetNames = outWb.Worksheets.Select(ws => ws.Name).ToList();
        var rowCount   = tgtWs.LastRowUsed()?.RowNumber() ?? 0;
        var colCount   = tgtWs.LastColumnUsed()?.ColumnNumber() ?? 0;

        var artifactId = Guid.NewGuid();
        using var outStream = new MemoryStream();
        outWb.SaveAs(outStream);
        var sizeBytes = outStream.Length;
        outStream.Position = 0;

        var storagePath = await _store.SaveAsync(artifactId, outputName, outStream, cancellationToken);

        const string xlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var artifact = new ArtifactReference
        {
            Id          = artifactId,
            Name        = outputName,
            ContentType = xlsxMime,
            StoragePath = storagePath,
            SizeBytes   = sizeBytes,
            Metadata    = new Dictionary<string, string>
            {
                ["sourceCount"]  = artifactNames.Count.ToString(),
                ["mergeMode"]    = mergeMode ?? "appendRows",
                ["sourceSheet"]  = sourceSheet,
                ["targetSheet"]  = targetSheet,
                ["sheetNames"]   = string.Join(", ", sheetNames),
                ["rowCount"]     = rowCount.ToString(),
                ["columnCount"]  = colCount.ToString()
            }
        };

        context.Artifacts[outputName] = artifact;

        _logger.LogInformation(
            "excel.merge: completed — input files={InputCount}, total rows written={TotalRows}, " +
            "output '{Output}' ({Size:N0} bytes)",
            artifactNames.Count, totalRowsWritten, outputName, sizeBytes);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["mergedArtifactName"]        = outputName,
                ["mergedArtifact_sheetNames"] = string.Join(", ", sheetNames),
                ["mergedArtifact_rowCount"]   = rowCount,
                ["mergedArtifact_colCount"]   = colCount,
                ["mergedArtifact_sourceCount"]= artifactNames.Count
            },
            artifacts: new List<ArtifactReference> { artifact },
            outputData:
                $"Merged {artifactNames.Count} file(s) into '{outputName}' " +
                $"({rowCount} rows, {sizeBytes:N0} bytes)");
    }

    private static void CopyRow(IXLWorksheet src, IXLWorksheet tgt, int srcRow, int tgtRow, int colCount)
    {
        for (var c = 1; c <= colCount; c++)
            tgt.Cell(tgtRow, c).Value = src.Cell(srcRow, c).Value;
    }

    private static List<string> ResolveArtifactNames(object raw, WorkflowContext context)
    {
        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                return je.EnumerateArray()
                    .Select(e => context.Interpolate(
                        e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText()))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            if (je.ValueKind == JsonValueKind.String)
                return ResolveCollection(context.Interpolate(je.GetString() ?? ""));
        }

        var str = raw.ToString() ?? "";
        return ResolveCollection(context.Interpolate(str));
    }

    private static List<string> ResolveCollection(string resolved)
    {
        if (string.IsNullOrWhiteSpace(resolved))
            return [];

        var trimmed = resolved.Trim();

        if (trimmed.StartsWith('['))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (list is { Count: > 0 })
                    return list.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
            catch { /* fall through */ }

            try
            {
                using var doc  = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                    return root.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
            }
            catch { /* fall through */ }
        }

        if (trimmed.Contains(','))
        {
            return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        return [trimmed];
    }

    private static bool GetBool(Dictionary<string, object> config, string key, bool defaultValue)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.True  => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
                _ => defaultValue
            };
        }

        if (raw is bool flag) return flag;

        return bool.TryParse(raw.ToString(), out var boolVal) ? boolVal : defaultValue;
    }

    private static int ParseInt(object? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
    }
}
