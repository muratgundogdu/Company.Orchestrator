using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Reads a rectangular Excel range into a structured DataTable-style JSON variable.
///
/// Config keys:
///   inputArtifactName  (required)
///   sheetName          (required)
///   range              (optional) — e.g. "A1:D100"; when empty, uses the sheet used range
///   hasHeader          (optional) — default true
///   outputVariable     (required)
///   trimValues         (optional) — default true
///   includeEmptyRows   (optional) — default false
///
/// Output variables:
///   {outputVariable}, {outputVariable}_count, {outputVariable}_columns,
///   {outputVariable}_first, {outputVariable}_0 … {outputVariable}_9
/// </summary>
public sealed class ExcelReadRangeStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelReadRangeStepHandler> _logger;

    public string HandlerType => "excel.read-range";

    public ExcelReadRangeStepHandler(IArtifactStore store, ILogger<ExcelReadRangeStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("inputArtifactName", out var inputRaw) || inputRaw is null)
            return StepResult.Fail("excel.read-range: 'inputArtifactName' is required.");

        var inputRawStr       = inputRaw.ToString()!;
        var inputArtifactName = context.Interpolate(inputRawStr);
        if (!string.Equals(inputRawStr, inputArtifactName, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "excel.read-range: resolved inputArtifactName '{Raw}' -> '{Resolved}'",
                inputRawStr, inputArtifactName);
        }

        if (!context.HasArtifact(inputArtifactName))
            return StepResult.Fail($"excel.read-range: input artifact '{inputArtifactName}' not found in context.");

        var sheetNameRaw = config.GetValueOrDefault("sheetName")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(sheetNameRaw))
            return StepResult.Fail("excel.read-range: 'sheetName' is required.");

        var sheetName = context.Interpolate(sheetNameRaw);

        var outputVar = config.GetValueOrDefault("outputVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(outputVar))
            return StepResult.Fail("excel.read-range: 'outputVariable' is required.");

        var rangeSpec       = config.GetValueOrDefault("range")?.ToString()?.Trim();
        var hasHeader       = GetBool(config, "hasHeader", defaultValue: true);
        var trimValues      = GetBool(config, "trimValues", defaultValue: true);
        var includeEmptyRows = GetBool(config, "includeEmptyRows", defaultValue: false);

        var inputArtifact = context.GetArtifact(inputArtifactName);
        var bytes         = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
        if (ws is null)
        {
            return StepResult.Fail(
                $"excel.read-range: sheet '{sheetName}' not found. Available: " +
                $"[{string.Join(", ", wb.Worksheets.Select(s => s.Name))}]");
        }

        var range = ResolveRange(ws, rangeSpec);
        if (range is null)
        {
            _logger.LogInformation(
                "excel.read-range: sheet '{Sheet}' has no used cells — returning empty table",
                sheetName);

            return BuildResult(outputVar, sheetName, "(empty)", [], []);
        }

        var rangeUsed = range.RangeAddress.ToString() ?? "(unknown)";
        var firstRow  = range.FirstRow().RowNumber();
        var lastRow   = range.LastRow().RowNumber();
        var firstCol  = range.FirstColumn().ColumnNumber();
        var lastCol   = range.LastColumn().ColumnNumber();
        var colCount  = lastCol - firstCol + 1;

        string[] headers;
        int dataStartRow;

        if (hasHeader)
        {
            headers = BuildHeaderNames(ws, firstRow, firstCol, colCount);
            dataStartRow = firstRow + 1;
        }
        else
        {
            headers      = Enumerable.Range(1, colCount).Select(i => $"Column{i}").ToArray();
            dataStartRow = firstRow;
        }

        var rows = new List<Dictionary<string, string>>();
        for (var r = dataStartRow; r <= lastRow; r++)
        {
            if (!includeEmptyRows && IsRowEmpty(ws, r, firstCol, colCount, trimValues))
                continue;

            var row = new Dictionary<string, string>(colCount, StringComparer.Ordinal);
            for (var c = 0; c < colCount; c++)
            {
                var val = GetCellDisplayString(ws.Cell(r, firstCol + c));
                if (trimValues)
                    val = val.Trim();
                row[headers[c]] = val;
            }

            rows.Add(row);
        }

        _logger.LogInformation(
            "excel.read-range: artifact='{Artifact}', sheet='{Sheet}', range='{Range}', " +
            "columns={ColumnCount}, rows={RowCount}, outputVariable='{OutputVar}'",
            inputArtifactName,
            sheetName,
            rangeUsed,
            headers.Length,
            rows.Count,
            outputVar);

        return BuildResult(outputVar, sheetName, rangeUsed, headers, rows);
    }

    private static StepResult BuildResult(
        string outputVar,
        string sheetName,
        string rangeUsed,
        IReadOnlyList<string> headers,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var json       = JsonSerializer.Serialize(rows);
        var columnsJson = JsonSerializer.Serialize(headers);
        var firstJson  = rows.Count > 0 ? JsonSerializer.Serialize(rows[0]) : "{}";

        var output = new Dictionary<string, object>
        {
            [outputVar]               = json,
            [$"{outputVar}_count"]    = rows.Count,
            [$"{outputVar}_columns"]  = columnsJson,
            [$"{outputVar}_first"]    = firstJson,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonSerializer.Serialize(rows[i]);

        return StepResult.Ok(
            output: output,
            outputData:
                $"Read {rows.Count} row(s) from sheet '{sheetName}' range '{rangeUsed}' into '{outputVar}'");
    }

    private static IXLRange? ResolveRange(IXLWorksheet ws, string? rangeSpec)
    {
        if (!string.IsNullOrWhiteSpace(rangeSpec))
            return ws.Range(rangeSpec);

        return ws.RangeUsed();
    }

    private static string[] BuildHeaderNames(IXLWorksheet ws, int headerRow, int firstCol, int colCount)
    {
        var seen   = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new string[colCount];

        for (var c = 0; c < colCount; c++)
        {
            var raw = GetCellDisplayString(ws.Cell(headerRow, firstCol + c)).Trim();
            var name = string.IsNullOrEmpty(raw) ? $"Column{c + 1}" : raw;

            if (seen.TryGetValue(name, out var count))
            {
                count++;
                seen[name] = count;
                name = $"{name}_{count}";
            }
            else
            {
                seen[name] = 1;
            }

            result[c] = name;
        }

        return result;
    }

    private static bool IsRowEmpty(IXLWorksheet ws, int row, int firstCol, int colCount, bool trimValues)
    {
        for (var c = 0; c < colCount; c++)
        {
            var val = GetCellDisplayString(ws.Cell(row, firstCol + c));
            if (trimValues)
                val = val.Trim();
            if (!string.IsNullOrEmpty(val))
                return false;
        }

        return true;
    }

    private static string GetCellDisplayString(IXLCell cell)
    {
        if (cell.Value.IsBlank)
            return string.Empty;

        try
        {
            var formatted = cell.GetFormattedString();
            if (!string.IsNullOrEmpty(formatted))
                return formatted;
        }
        catch
        {
            // Fall back to typed string conversion below.
        }

        return GetCellValueAsString(cell);
    }

    private static string GetCellValueAsString(IXLCell cell)
    {
        if (cell.Value.IsBlank) return string.Empty;
        if (cell.Value.IsText) return cell.GetString();
        if (cell.Value.IsNumber)
            return cell.Value.GetNumber().ToString(CultureInfo.InvariantCulture);
        if (cell.Value.IsDateTime)
            return cell.Value.GetDateTime().ToString(CultureInfo.InvariantCulture);
        if (cell.Value.IsBoolean)
            return cell.Value.GetBoolean().ToString();
        return cell.GetString();
    }

    private static bool GetBool(Dictionary<string, object> config, string key, bool defaultValue)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.String => bool.TryParse(je.GetString(), out var parsed) ? parsed : defaultValue,
                _                    => defaultValue
            };
        }

        if (raw is bool flag) return flag;
        return bool.TryParse(raw.ToString(), out var boolVal) ? boolVal : defaultValue;
    }
}
