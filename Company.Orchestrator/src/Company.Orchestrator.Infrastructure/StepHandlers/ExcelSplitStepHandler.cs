using System.Text.Json;
using ClosedXML.Excel;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Splits an Excel workbook into multiple output workbooks grouped by a column value.
///
/// Config keys:
///   inputArtifactName   (required)
///   sourceSheetName     (required)
///   splitColumn         (required) — column letter or number
///   outputNamePattern   (required) — supports {value} and {index}
///   includeHeader       (optional) — copy row 1 into each output (default true)
///   startRow            (optional) — first data row to group (default 2)
///
/// Output variables:
///   splitArtifacts, splitArtifacts_count, splitArtifacts_first, splitArtifacts_0, …
/// </summary>
public sealed class ExcelSplitStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelSplitStepHandler> _logger;

    public string HandlerType => "excel.split";

    public ExcelSplitStepHandler(IArtifactStore store, ILogger<ExcelSplitStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("inputArtifactName", out var inputRaw) || inputRaw is null)
            return StepResult.Fail("excel.split: 'inputArtifactName' is required.");

        var inputArtifactName = context.Interpolate(inputRaw.ToString()!);
        if (!context.HasArtifact(inputArtifactName))
            return StepResult.Fail($"excel.split: input artifact '{inputArtifactName}' not found in context.");

        var sourceSheet = context.Interpolate(
            config.GetValueOrDefault("sourceSheetName")?.ToString() ?? "Data");
        var splitColumnRef = config.GetValueOrDefault("splitColumn")?.ToString();
        if (string.IsNullOrWhiteSpace(splitColumnRef))
            return StepResult.Fail("excel.split: 'splitColumn' is required.");

        var splitCol = ParseColRef(splitColumnRef);
        var pattern  = config.GetValueOrDefault("outputNamePattern")?.ToString();
        if (string.IsNullOrWhiteSpace(pattern))
            return StepResult.Fail("excel.split: 'outputNamePattern' is required.");

        var includeHeader = GetBool(config, "includeHeader", defaultValue: true);
        var startRow      = ParseInt(config.GetValueOrDefault("startRow"), defaultValue: 2);
        if (startRow < 1)
            return StepResult.Fail("excel.split: 'startRow' must be at least 1.");

        var inputArtifact = context.GetArtifact(inputArtifactName);
        var bytes         = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);

        using var inWb = new XLWorkbook(new MemoryStream(bytes));
        var srcWs = inWb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(sourceSheet, StringComparison.OrdinalIgnoreCase));
        if (srcWs is null)
        {
            return StepResult.Fail(
                $"excel.split: sheet '{sourceSheet}' not found. Available: " +
                $"[{string.Join(", ", inWb.Worksheets.Select(s => s.Name))}]");
        }

        var lastRow = srcWs.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = srcWs.LastColumnUsed()?.ColumnNumber() ?? 1;

        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var r = startRow; r <= lastRow; r++)
        {
            var key = srcWs.Cell(r, splitCol).GetString().Trim();
            if (string.IsNullOrEmpty(key))
                key = "(blank)";

            if (!groups.TryGetValue(key, out var rows))
            {
                rows = [];
                groups[key] = rows;
            }

            rows.Add(r);
        }

        _logger.LogInformation(
            "excel.split: found {GroupCount} group(s) in sheet '{Sheet}', column '{Column}'",
            groups.Count, sourceSheet, splitColumnRef);

        var produced     = new List<ArtifactReference>();
        var artifactNames = new List<string>();
        var index = 0;

        foreach (var (groupValue, rowIndexes) in groups)
        {
            var outputName = BuildOutputName(pattern, groupValue, index);
            var uniqueName = outputName;
            var suffix = 1;
            while (artifactNames.Contains(uniqueName))
            {
                var baseName = Path.GetFileNameWithoutExtension(outputName);
                var ext      = Path.GetExtension(outputName);
                uniqueName   = $"{baseName}-{suffix}{ext}";
                suffix++;
            }
            outputName = uniqueName;

            using var outWb = new XLWorkbook();
            var outWs = outWb.Worksheets.Add(sourceSheet);
            var outRow = 1;

            if (includeHeader && startRow > 1)
            {
                CopyRow(srcWs, outWs, 1, outRow, lastCol);
                outRow++;
            }

            foreach (var srcRow in rowIndexes)
            {
                CopyRow(srcWs, outWs, srcRow, outRow, lastCol);
                outRow++;
            }

            var artifactId = Guid.NewGuid();
            using var outStream = new MemoryStream();
            outWb.SaveAs(outStream);
            outStream.Position = 0;

            var storagePath = await _store.SaveAsync(artifactId, outputName, outStream, cancellationToken);

            const string xlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var artifact = new ArtifactReference
            {
                Id          = artifactId,
                Name        = outputName,
                ContentType = xlsxMime,
                StoragePath = storagePath,
                SizeBytes   = outStream.Length,
                Metadata    = new Dictionary<string, string>
                {
                    ["splitValue"] = groupValue,
                    ["rowCount"]   = rowIndexes.Count.ToString(),
                    ["sourceArtifact"] = inputArtifactName
                }
            };

            produced.Add(artifact);
            artifactNames.Add(outputName);
            context.Artifacts[outputName] = artifact;

            _logger.LogInformation(
                "excel.split: group '{Value}' — {RowCount} row(s) → artifact '{Name}'",
                groupValue, rowIndexes.Count, outputName);

            index++;
        }

        _logger.LogInformation(
            "excel.split: completed — {GroupCount} group(s), artifacts=[{Names}]",
            groups.Count, string.Join(", ", artifactNames));

        var json      = JsonSerializer.Serialize(artifactNames);
        var firstName = artifactNames.Count > 0 ? artifactNames[0] : string.Empty;

        var output = new Dictionary<string, object>
        {
            ["splitArtifacts"]       = json,
            ["splitArtifacts_count"] = artifactNames.Count,
            ["splitArtifacts_first"] = firstName,
        };

        for (var i = 0; i < artifactNames.Count; i++)
            output[$"splitArtifacts_{i}"] = artifactNames[i];

        return StepResult.Ok(
            output: output,
            artifacts: produced,
            outputData:
                $"Split '{inputArtifactName}' into {artifactNames.Count} workbook(s) " +
                $"by column '{splitColumnRef}'");
    }

    private static void CopyRow(IXLWorksheet src, IXLWorksheet tgt, int srcRow, int tgtRow, int colCount)
    {
        for (var c = 1; c <= colCount; c++)
            tgt.Cell(tgtRow, c).Value = src.Cell(srcRow, c).Value;
    }

    private static string BuildOutputName(string pattern, string value, int index)
    {
        var name = pattern
            .Replace("{value}", SanitizeFileName(value), StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", index.ToString(), StringComparison.OrdinalIgnoreCase);

        if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            name += ".xlsx";

        return name;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "blank";

        var invalid = Path.GetInvalidFileNameChars();
        var chars   = value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim().Replace(' ', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "blank" : sanitized;
    }

    private static int ParseColRef(string colRef)
    {
        if (int.TryParse(colRef, out var n)) return n;

        var result = 0;
        foreach (var ch in colRef.ToUpperInvariant())
        {
            if (ch < 'A' || ch > 'Z')
                throw new InvalidOperationException(
                    $"Invalid column reference '{colRef}'. Use a letter (e.g. 'C') or number (e.g. '3').");
            result = result * 26 + (ch - 'A' + 1);
        }
        return result;
    }

    private static int ParseInt(object? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
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
}
