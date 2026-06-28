using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Writes a DataTable JSON variable into an Excel worksheet artifact.
/// </summary>
public sealed class ExcelWriteDataTableStepHandler : IStepHandler
{
    private static readonly Regex CellAddressRegex = new(
        @"^[A-Za-z]{1,3}[1-9]\d*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelWriteDataTableStepHandler> _logger;

    public string HandlerType => "excel.write-datatable";

    public ExcelWriteDataTableStepHandler(
        IArtifactStore store,
        ILogger<ExcelWriteDataTableStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var inputArtifactName = context.Interpolate(GetString(config, "inputArtifactName"));
        var sheetName         = context.Interpolate(GetString(config, "sheetName"));
        var sourceVariable    = NormalizeVarName(GetString(config, "sourceVariable"));
        var startCell         = GetString(config, "startCell", "A1").Trim().ToUpperInvariant();
        var includeHeaders    = GetBool(config, "includeHeaders", defaultValue: true);
        var clearExistingData = GetBool(config, "clearExistingData", defaultValue: false);
        var createSheetIfMissing = GetBool(config, "createSheetIfMissing", defaultValue: true);

        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail("excel.write-datatable: 'inputArtifactName' is required.");
        if (string.IsNullOrWhiteSpace(sheetName))
            return StepResult.Fail("excel.write-datatable: 'sheetName' is required.");
        if (string.IsNullOrWhiteSpace(sourceVariable))
            return StepResult.Fail("excel.write-datatable: 'sourceVariable' is required.");
        if (string.IsNullOrWhiteSpace(startCell))
            return StepResult.Fail("excel.write-datatable: 'startCell' is required.");
        if (!IsValidCellAddress(startCell))
            return StepResult.Fail($"excel.write-datatable: invalid startCell '{startCell}'.");

        if (!context.HasArtifact(inputArtifactName))
        {
            return StepResult.Fail(
                $"excel.write-datatable: workbook artifact '{inputArtifactName}' not found in context.");
        }

        if (!context.Variables.TryGetValue(sourceVariable, out var sourceRaw))
        {
            return StepResult.Fail(
                $"excel.write-datatable: source variable '{sourceVariable}' not found in workflow context.");
        }

        List<Dictionary<string, string>> rows;
        List<string> firstRowColumnOrder;
        try
        {
            rows = ParseDataTableRows(sourceRaw, out firstRowColumnOrder);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"excel.write-datatable: {ex.Message}");
        }

        var columns = BuildColumns(firstRowColumnOrder, rows);
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
                    $"excel.write-datatable: sheet '{sheetName}' not found. Available: " +
                    $"[{string.Join(", ", wb.Worksheets.Select(s => s.Name))}]");
            }

            ws = wb.Worksheets.Add(sheetName);
        }

        if (clearExistingData)
        {
            ws.RangeUsed()?.Clear(XLClearOptions.Contents);
        }

        var anchor   = ws.Cell(startCell);
        var startRow = anchor.Address.RowNumber;
        var startCol = anchor.Address.ColumnNumber;
        var writeRow = startRow;

        if (includeHeaders && columns.Count > 0)
        {
            for (var c = 0; c < columns.Count; c++)
                ws.Cell(writeRow, startCol + c).Value = columns[c];
            writeRow++;
        }

        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                row.TryGetValue(columns[c], out var value);
                ws.Cell(writeRow, startCol + c).Value = value ?? string.Empty;
            }

            writeRow++;
        }

        var rowsWritten    = rows.Count;
        var columnsWritten = columns.Count;

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
                ["rowsWritten"]    = rowsWritten.ToString(),
                ["columnsWritten"] = columnsWritten.ToString(),
                ["sheetName"]      = sheetName,
                ["startCell"]      = startCell,
            },
        };

        _logger.LogInformation(
            "excel.write-datatable: artifact='{Artifact}', sheet='{Sheet}', startCell='{StartCell}', " +
            "sourceVariable='{SourceVariable}', rowsWritten={RowsWritten}, columnsWritten={ColumnsWritten}",
            inputArtifactName,
            sheetName,
            startCell,
            sourceVariable,
            rowsWritten,
            columnsWritten);

        return StepResult.Ok(
            output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["outputArtifactName"] = inputArtifactName,
                ["rowsWritten"]        = rowsWritten,
                ["columnsWritten"]     = columnsWritten,
            },
            artifacts: [updatedArtifact],
            outputData:
                $"Wrote {rowsWritten} row(s) and {columnsWritten} column(s) to " +
                $"'{inputArtifactName}' sheet '{sheetName}' starting at {startCell}.");
    }

    private static List<Dictionary<string, string>> ParseDataTableRows(
        object raw,
        out List<string> firstRowColumnOrder)
    {
        firstRowColumnOrder = [];

        var json = VariableToString(raw).Trim();
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException($"source variable is empty.");

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

    private static List<string> BuildColumns(
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

    private static bool IsValidCellAddress(string address) =>
        CellAddressRegex.IsMatch(address.Trim());

    private static string JsonElementToString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String  => element.GetString() ?? string.Empty,
            JsonValueKind.Number  => element.GetRawText(),
            JsonValueKind.True    => "true",
            JsonValueKind.False   => "false",
            JsonValueKind.Null    => string.Empty,
            _                     => element.GetRawText(),
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
