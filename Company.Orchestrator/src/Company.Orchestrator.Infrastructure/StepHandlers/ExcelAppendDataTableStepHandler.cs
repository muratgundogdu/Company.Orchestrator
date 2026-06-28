using System.Text.Json;
using ClosedXML.Excel;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Appends DataTable JSON rows to an existing Excel worksheet without overwriting prior data.
/// </summary>
public sealed class ExcelAppendDataTableStepHandler : IStepHandler
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelAppendDataTableStepHandler> _logger;

    public string HandlerType => "excel.append-datatable";

    public ExcelAppendDataTableStepHandler(
        IArtifactStore store,
        ILogger<ExcelAppendDataTableStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var inputArtifactName    = context.Interpolate(GetString(config, "inputArtifactName"));
        var sheetName            = context.Interpolate(GetString(config, "sheetName"));
        var sourceVariable       = NormalizeVarName(GetString(config, "sourceVariable"));
        var createSheetIfMissing = GetBool(config, "createSheetIfMissing", defaultValue: true);
        var includeHeadersIfEmpty = GetBool(config, "includeHeadersIfEmpty", defaultValue: true);
        var matchColumnsByName   = GetBool(config, "matchColumnsByName", defaultValue: true);

        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail("excel.append-datatable: 'inputArtifactName' is required.");
        if (string.IsNullOrWhiteSpace(sheetName))
            return StepResult.Fail("excel.append-datatable: 'sheetName' is required.");
        if (string.IsNullOrWhiteSpace(sourceVariable))
            return StepResult.Fail("excel.append-datatable: 'sourceVariable' is required.");

        if (!context.HasArtifact(inputArtifactName))
        {
            return StepResult.Fail(
                $"excel.append-datatable: workbook artifact '{inputArtifactName}' not found in context.");
        }

        if (!context.Variables.TryGetValue(sourceVariable, out var sourceRaw))
        {
            return StepResult.Fail(
                $"excel.append-datatable: source variable '{sourceVariable}' not found in workflow context.");
        }

        List<Dictionary<string, string>> rows;
        List<string> sourceColumnOrder;
        try
        {
            rows = ParseDataTableRows(sourceRaw, out sourceColumnOrder);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"excel.append-datatable: {ex.Message}");
        }

        var sourceColumns = BuildSourceColumns(sourceColumnOrder, rows);
        var inputArtifact = context.GetArtifact(inputArtifactName);
        var bytes         = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

        if (ws is null)
        {
            if (!createSheetIfMissing)
            {
                return StepResult.Fail(
                    $"excel.append-datatable: sheet '{sheetName}' not found. Available: " +
                    $"[{string.Join(", ", wb.Worksheets.Select(s => s.Name))}]");
            }

            ws = wb.Worksheets.Add(sheetName);
        }

        var isEmptySheet = ws.RangeUsed() is null;
        var writeRow     = DetermineAppendStartRow(ws, isEmptySheet, includeHeadersIfEmpty, sourceColumns);

        var columnsWritten = matchColumnsByName
            ? AppendMatchByName(ws, rows, sourceColumns, isEmptySheet, includeHeadersIfEmpty, writeRow)
            : AppendBySourceOrder(ws, rows, sourceColumns, isEmptySheet, includeHeadersIfEmpty, writeRow);

        var rowsAppended       = rows.Count;
        var lastRowAfterAppend = rowsAppended > 0 ? writeRow + rowsAppended - 1 : Math.Max(writeRow - 1, 0);

        using var outStream = new MemoryStream();
        wb.SaveAs(outStream);
        var sizeBytes = outStream.Length;
        outStream.Position = 0;

        var storagePath = await _store.SaveAsync(
            inputArtifact.Id,
            inputArtifactName,
            outStream,
            cancellationToken);

        var updatedArtifact = new ArtifactReference
        {
            Id          = inputArtifact.Id,
            Name        = inputArtifactName,
            ContentType = string.IsNullOrWhiteSpace(inputArtifact.ContentType) ? XlsxMime : inputArtifact.ContentType,
            StoragePath = storagePath,
            SizeBytes   = sizeBytes,
            Metadata    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rowsAppended"]       = rowsAppended.ToString(),
                ["columnsWritten"]     = columnsWritten.ToString(),
                ["lastRowAfterAppend"] = lastRowAfterAppend.ToString(),
                ["sheetName"]          = sheetName,
            },
        };

        _logger.LogInformation(
            "excel.append-datatable: artifact='{Artifact}', sheet='{Sheet}', sourceVariable='{SourceVariable}', " +
            "rowsAppended={RowsAppended}, columnsWritten={ColumnsWritten}, lastRow={LastRow}",
            inputArtifactName,
            sheetName,
            sourceVariable,
            rowsAppended,
            columnsWritten,
            lastRowAfterAppend);

        return StepResult.Ok(
            output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["rowsAppended"]       = rowsAppended,
                ["columnsWritten"]     = columnsWritten,
                ["lastRowAfterAppend"] = lastRowAfterAppend,
            },
            artifacts: [updatedArtifact],
            outputData:
                $"Appended {rowsAppended} row(s) to '{inputArtifactName}' sheet '{sheetName}' " +
                $"(last row {lastRowAfterAppend}).");
    }

    private static int DetermineAppendStartRow(
        IXLWorksheet ws,
        bool isEmptySheet,
        bool includeHeadersIfEmpty,
        IReadOnlyList<string> sourceColumns)
    {
        if (isEmptySheet)
        {
            if (includeHeadersIfEmpty && sourceColumns.Count > 0)
            {
                for (var c = 0; c < sourceColumns.Count; c++)
                    ws.Cell(1, c + 1).Value = sourceColumns[c];

                return 2;
            }

            return 1;
        }

        return (ws.LastRowUsed()?.RowNumber() ?? 0) + 1;
    }

    private static int AppendMatchByName(
        IXLWorksheet ws,
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<string> sourceColumns,
        bool isEmptySheet,
        bool includeHeadersIfEmpty,
        int writeRow)
    {
        var headerMap = ReadHeaderMap(ws);

        if (isEmptySheet && !includeHeadersIfEmpty)
        {
            headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < sourceColumns.Count; i++)
                headerMap[sourceColumns[i]] = i + 1;
        }
        else if (isEmptySheet && includeHeadersIfEmpty)
        {
            headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < sourceColumns.Count; i++)
                headerMap[sourceColumns[i]] = i + 1;
        }

        foreach (var column in sourceColumns)
        {
            if (!headerMap.ContainsKey(column))
            {
                var nextCol = NextAvailableColumn(ws, headerMap);
                ws.Cell(1, nextCol).Value = column;
                headerMap[column] = nextCol;
            }
        }

        var sheetColumns = headerMap
            .OrderBy(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var row in rows)
        {
            foreach (var sheetColumn in sheetColumns)
            {
                row.TryGetValue(sheetColumn, out var value);
                ws.Cell(writeRow, headerMap[sheetColumn]).Value = value ?? string.Empty;
            }

            writeRow++;
        }

        return sourceColumns.Count;
    }

    private static int AppendBySourceOrder(
        IXLWorksheet ws,
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<string> sourceColumns,
        bool isEmptySheet,
        bool includeHeadersIfEmpty,
        int writeRow)
    {
        if (isEmptySheet && !includeHeadersIfEmpty)
        {
            foreach (var row in rows)
            {
                for (var c = 0; c < sourceColumns.Count; c++)
                {
                    row.TryGetValue(sourceColumns[c], out var value);
                    ws.Cell(writeRow, c + 1).Value = value ?? string.Empty;
                }

                writeRow++;
            }

            return sourceColumns.Count;
        }

        foreach (var row in rows)
        {
            for (var c = 0; c < sourceColumns.Count; c++)
            {
                row.TryGetValue(sourceColumns[c], out var value);
                ws.Cell(writeRow, c + 1).Value = value ?? string.Empty;
            }

            writeRow++;
        }

        return sourceColumns.Count;
    }

    private static Dictionary<string, int> ReadHeaderMap(IXLWorksheet ws)
    {
        var map     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var c = 1; c <= lastCol; c++)
        {
            var name = ws.Cell(1, c).GetString().Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            if (!map.ContainsKey(name))
                map[name] = c;
        }

        return map;
    }

    private static int NextAvailableColumn(
        IXLWorksheet ws,
        IReadOnlyDictionary<string, int> headerMap)
    {
        var maxFromHeaders = headerMap.Count > 0 ? headerMap.Values.Max() : 0;
        var maxFromSheet   = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        return Math.Max(maxFromHeaders, maxFromSheet) + 1;
    }

    private static List<string> BuildSourceColumns(
        IReadOnlyList<string> firstRowColumnOrder,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var columns = new List<string>(firstRowColumnOrder);
        var seen    = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
            {
                if (seen.Add(key))
                    columns.Add(key);
            }
        }

        return columns;
    }

    private static List<Dictionary<string, string>> ParseDataTableRows(
        object raw,
        out List<string> firstRowColumnOrder)
    {
        firstRowColumnOrder = [];

        var json = VariableToString(raw).Trim();
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException("source variable is empty.");

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("source variable is not a JSON array.");

        var rows    = new List<Dictionary<string, string>>();
        var isFirst = true;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("source variable is not a JSON array of objects.");

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in element.EnumerateObject())
            {
                if (isFirst)
                    firstRowColumnOrder.Add(prop.Name);

                row[prop.Name] = JsonElementToString(prop.Value);
            }

            rows.Add(row);
            isFirst = false;
        }

        return rows;
    }

    private static string JsonElementToString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String  => element.GetString() ?? string.Empty,
            JsonValueKind.Number  => element.GetRawText(),
            JsonValueKind.True     => "true",
            JsonValueKind.False    => "false",
            JsonValueKind.Null     => string.Empty,
            _                      => element.GetRawText(),
        };

    private static string NormalizeVarName(string name) => name.Trim().Trim('{', '}');

    private static string GetString(Dictionary<string, object> config, string key, string fallback = "")
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
            return fallback;

        if (raw is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? fallback,
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                _                    => el.GetRawText(),
            };
        }

        return raw.ToString() ?? fallback;
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
                _                    => defaultValue,
            };
        }

        if (raw is bool flag)
            return flag;

        return bool.TryParse(raw.ToString(), out var boolVal) ? boolVal : defaultValue;
    }

    private static string VariableToString(object? val)
    {
        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? string.Empty,
                JsonValueKind.Array   => je.GetRawText(),
                JsonValueKind.Object  => je.GetRawText(),
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => string.Empty,
                _                     => je.GetRawText(),
            };
        }

        return val?.ToString() ?? string.Empty;
    }
}
