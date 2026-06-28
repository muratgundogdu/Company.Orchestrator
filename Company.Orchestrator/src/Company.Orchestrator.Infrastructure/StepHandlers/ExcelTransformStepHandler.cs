using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Applies a sequence of structural and data transformations to an Excel workbook artifact
/// and produces a new output artifact — the source is never mutated.
///
/// Config keys:
///   inputArtifactName  (required) — context artifact name; supports {{variable}} interpolation
///   outputName         (required) — name for the new output artifact (.xlsx appended if absent)
///   operations         (required) — JSON array of operation objects (see below)
///
/// Supported operation types:
///   addSheet           sheetName
///   deleteSheet        sheetName
///   renameSheet        sheetName, newName
///   deleteRow          sheetName, rowNumber (1-based)
///   deleteColumn       sheetName, column (letter or 1-based number)
///   addColumn          sheetName, header, [column], [defaultValue]
///   copyColumn         [sourceSheet], sourceColumn, [targetSheet], targetColumn, [targetHeader]
///                      or sheetName for in-sheet copy
///   multiplyColumn     sheetName + column (in-place)  OR
///                      sourceSheet + sourceColumn + targetSheet + targetColumn + [targetHeader] (cross-sheet)
///                      + factor (number)
///   divideColumn       same shape as multiplyColumn; factor (divisor)
///   addNumberToColumn  same shape; value (number to add)
///   filterRows         sheetName, column (letter/number/header), [startRow:"2"], [mode:"keep"|"remove"],
///                      [conditionJoin:"and"|"or"], conditions (array of {operator, value?, values?})
///                      Legacy: operator, value, [invert:"true"] (invert=true ≡ mode=remove)
///   sortRows           sheetName, [startRow:"2"], sorts (array of {column, direction, dataType})
///   removeDuplicates   sheetName, [startRow:"2"], columns (array), [keep:"first"|"last"],
///                      [ignoreCase:"true"], [trimValues:"true"]
///   copyRows           sourceSheet, targetSheet, column, operator, value
///   sortByColumn       sheetName, column, [direction:"asc"|"desc"]
///   setCellValue       sheetName, cell (e.g. "A1"), [value]
///   replaceText        sheetName, oldText, newText, [column]
///   removeEmptyRows    sheetName, [startRow:"2"]
///   insertColumn       sheetName, afterColumn, newColumn, [header]
///   convertColumnToNumber  sheetName, column, [startRow:"2"], [numberFormat:"#,##0.00"]
///   setFormula         sheetName, cell, formula
///   fillFormulaDown    sheetName, column, startRow, [endRow], formulaTemplate ({row} placeholder)
///   setColumnFormat    sheetName, column, format
///   setHeader          sheetName, column, header
///   autoFitColumns     sheetName
///   createSheetFromColumns  sourceSheet, targetSheet, columns (array or "A,B,C")
///   setCellStyle       sheetName, range, [bold:"true"], [backgroundColor:"#RRGGBB"]
///   importTextToSheet  textArtifactName, targetSheet, [delimiter:","|comma|semicolon|tab|pipe|custom],
///                      [customDelimiter], [encoding:"UTF-8"], [hasHeader:"true"], [startCell:"A1"],
///                      [quoteChar:"\""], [trimValues:"true"], [parseNumbers:"false"], [overwrite:"true"]
///
/// filterRows / copyRows operators:
///   equals, notEquals, contains, notContains, startsWith, endsWith,
///   greaterThan, lessThan, greaterOrEqual, lessOrEqual, isEmpty, isNotEmpty, in, notIn
///
/// Output variables:
///   transformedArtifactName          — artifact name for downstream steps
///   transformedArtifact_sheetNames   — comma-separated sheet names
///   transformedArtifact_rowCount     — row count of first sheet
///   transformedArtifact_colCount     — column count of first sheet
/// </summary>
public sealed class ExcelTransformStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelTransformStepHandler> _logger;

    public string HandlerType => "excel.transform";

    public ExcelTransformStepHandler(IArtifactStore store, ILogger<ExcelTransformStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Entry point
    // ══════════════════════════════════════════════════════════════════════

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        // ── input artifact ──────────────────────────────────────────────
        if (!config.TryGetValue("inputArtifactName", out var inputRaw) || inputRaw is null)
            return StepResult.Fail("excel.transform: 'inputArtifactName' is required.");

        var inputRawStr       = inputRaw.ToString()!;
        var inputArtifactName = context.Interpolate(inputRawStr);
        if (!string.Equals(inputRawStr, inputArtifactName, StringComparison.Ordinal))
            _logger.LogInformation(
                "excel.transform: resolved inputArtifactName '{Raw}' -> '{Resolved}'",
                inputRawStr, inputArtifactName);

        if (!context.HasArtifact(inputArtifactName))
            return StepResult.Fail(
                $"excel.transform: input artifact '{inputArtifactName}' not found in context.");

        var inputArtifact = context.GetArtifact(inputArtifactName);

        // ── output name ─────────────────────────────────────────────────
        var outputNameRaw = config.GetValueOrDefault("outputName")?.ToString() ?? "transformed-excel";
        var outputName    = context.Interpolate(outputNameRaw);
        if (!outputName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            outputName += ".xlsx";

        // ── parse operations ────────────────────────────────────────────
        if (!config.TryGetValue("operations", out var opsRaw) || opsRaw is null)
            return StepResult.Fail("excel.transform: 'operations' array is required.");

        List<Dictionary<string, string?>> operations;
        try { operations = ParseOperations(opsRaw); }
        catch (Exception ex)
        {
            return StepResult.Fail($"excel.transform: failed to parse 'operations': {ex.Message}");
        }

        _logger.LogInformation(
            "excel.transform: starting — input='{Input}', output='{Output}', operations={Count}",
            inputArtifactName, outputName, operations.Count);

        // ── load workbook into memory ───────────────────────────────────
        var bytes = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);
        using var wb = new XLWorkbook(new MemoryStream(bytes));

        // ── apply each operation ────────────────────────────────────────
        for (var i = 0; i < operations.Count; i++)
        {
            var op     = operations[i];
            var opType = op.GetValueOrDefault("type") ?? "(unknown)";
            _logger.LogInformation(
                "excel.transform: [{Index}/{Total}] executing '{Type}'",
                i + 1, operations.Count, opType);
            try
            {
                var opTypeLower = (op.GetValueOrDefault("type") ?? "").ToLowerInvariant();
                if (opTypeLower == "importtexttosheet")
                    await ApplyImportTextToSheetAsync(wb, op, context, cancellationToken);
                else
                    ApplyOperation(wb, op);
                _logger.LogInformation(
                    "excel.transform: [{Index}/{Total}] '{Type}' completed",
                    i + 1, operations.Count, opType);
            }
            catch (Exception ex)
            {
                return StepResult.Fail(
                    $"excel.transform: operation [{i + 1}/{operations.Count}] '{opType}' failed: {ex.Message}");
            }
        }

        // ── collect metadata ────────────────────────────────────────────
        var sheetNames = wb.Worksheets.Select(ws => ws.Name).ToList();
        var firstWs    = wb.Worksheets.FirstOrDefault();
        var rowCount   = firstWs?.LastRowUsed()?.RowNumber() ?? 0;
        var colCount   = firstWs?.LastColumnUsed()?.ColumnNumber() ?? 0;

        // ── persist new artifact ────────────────────────────────────────
        var artifactId = Guid.NewGuid();
        using var outStream = new MemoryStream();
        wb.SaveAs(outStream);
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
                ["sourceArtifactId"] = inputArtifact.Id.ToString(),
                ["operationCount"]   = operations.Count.ToString(),
                ["sheetNames"]       = string.Join(", ", sheetNames),
                ["rowCount"]         = rowCount.ToString(),
                ["columnCount"]      = colCount.ToString()
            }
        };

        context.Artifacts[outputName] = artifact;

        _logger.LogInformation(
            "excel.transform: completed — output artifact '{Name}' ({Size:N0} bytes), " +
            "sheets=[{Sheets}], rows={Rows}, cols={Cols}",
            outputName, sizeBytes, string.Join(", ", sheetNames), rowCount, colCount);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["transformedArtifactName"]        = outputName,
                ["transformedArtifact_sheetNames"] = string.Join(", ", sheetNames),
                ["transformedArtifact_rowCount"]   = rowCount,
                ["transformedArtifact_colCount"]   = colCount
            },
            artifacts: new List<ArtifactReference> { artifact },
            outputData:
                $"Transformed '{inputArtifactName}' → '{outputName}' " +
                $"({operations.Count} operations, {sizeBytes:N0} bytes)");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Dispatch
    // ══════════════════════════════════════════════════════════════════════

    private void ApplyOperation(XLWorkbook wb, Dictionary<string, string?> op)
    {
        switch ((op.GetValueOrDefault("type") ?? "").ToLowerInvariant())
        {
            case "addsheet":           ApplyAddSheet(wb, op);           break;
            case "deletesheet":        ApplyDeleteSheet(wb, op);        break;
            case "renamesheet":        ApplyRenameSheet(wb, op);        break;
            case "deleterow":          ApplyDeleteRow(wb, op);          break;
            case "deletecolumn":       ApplyDeleteColumn(wb, op);       break;
            case "addcolumn":          ApplyAddColumn(wb, op);          break;
            case "copycolumn":         ApplyCopyColumn(wb, op);         break;
            case "multiplycolumn":     ApplyArithmeticColumn(wb, op, ArithOp.Multiply); break;
            case "dividecolumn":       ApplyArithmeticColumn(wb, op, ArithOp.Divide);   break;
            case "addnumbertocolumn":  ApplyArithmeticColumn(wb, op, ArithOp.Add);      break;
            case "filterrows":         ApplyFilterRows(wb, op);         break;
            case "sortrows":           ApplySortRows(wb, op);           break;
            case "removeduplicates":   ApplyRemoveDuplicates(wb, op);   break;
            case "copyrows":           ApplyCopyRows(wb, op);           break;
            case "sortbycolumn":       ApplySortByColumn(wb, op);       break;
            case "setcellvalue":       ApplySetCellValue(wb, op);       break;
            case "replacetext":        ApplyReplaceText(wb, op);        break;
            case "removeemptyrows":    ApplyRemoveEmptyRows(wb, op);    break;
            case "insertcolumn":       ApplyInsertColumn(wb, op);       break;
            case "convertcolumntonumber": ApplyConvertColumnToNumber(wb, op); break;
            case "setformula":         ApplySetFormula(wb, op);         break;
            case "fillformuladown":    ApplyFillFormulaDown(wb, op);    break;
            case "setcolumnformat":    ApplySetColumnFormat(wb, op);    break;
            case "setheader":          ApplySetHeader(wb, op);          break;
            case "autofitcolumns":     ApplyAutoFitColumns(wb, op);     break;
            case "createsheetfromcolumns": ApplyCreateSheetFromColumns(wb, op); break;
            case "setcellstyle":       ApplySetCellStyle(wb, op);       break;
            case "transformcolumn":    ApplyTransformColumn(wb, op);    break;
            case "copycolumnvalues":   ApplyCopyColumnValues(wb, op);   break;
            case "replacecolumnvalues": ApplyReplaceColumnValues(wb, op); break;
            case "calculateformulavalues": ApplyCalculateFormulaValues(wb, op); break;
            case "lookupcolumn":         ApplyLookupColumn(wb, op);         break;
            case "multicolumnlookup":    ApplyMultiColumnLookup(wb, op);    break;
            case "compositelookup":      ApplyCompositeLookup(wb, op);      break;
            case "replacewithlookupresult": ApplyReplaceWithLookupResult(wb, op); break;
            default:
                throw new InvalidOperationException(
                    $"Unknown operation type '{op.GetValueOrDefault("type")}'. " +
                    "Supported: addSheet, deleteSheet, renameSheet, deleteRow, deleteColumn, " +
                    "addColumn, copyColumn, multiplyColumn, divideColumn, addNumberToColumn, " +
                    "filterRows, sortRows, removeDuplicates, copyRows, sortByColumn, setCellValue, replaceText, removeEmptyRows, " +
                    "insertColumn, convertColumnToNumber, setFormula, fillFormulaDown, setColumnFormat, " +
                    "setHeader, autoFitColumns, createSheetFromColumns, setCellStyle, importTextToSheet, " +
                    "transformColumn, copyColumnValues, replaceColumnValues, calculateFormulaValues, " +
                    "lookupColumn, multiColumnLookup, compositeLookup, replaceWithLookupResult.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Sheet-level operations
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyAddSheet(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var name = Require(op, "sheetName");
        if (!wb.Worksheets.Any(ws => ws.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            wb.Worksheets.Add(name);
    }

    private static void ApplyDeleteSheet(XLWorkbook wb, Dictionary<string, string?> op)
        => FindSheet(wb, Require(op, "sheetName")).Delete();

    private static void ApplyRenameSheet(XLWorkbook wb, Dictionary<string, string?> op)
        => FindSheet(wb, Require(op, "sheetName")).Name = Require(op, "newName");

    // ══════════════════════════════════════════════════════════════════════
    // Row / column structural operations
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyDeleteRow(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws  = FindSheet(wb, Require(op, "sheetName"));
        var row = int.Parse(Require(op, "rowNumber"));
        ws.Row(row).Delete();
    }

    private static void ApplyDeleteColumn(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws  = FindSheet(wb, Require(op, "sheetName"));
        var col = ParseColRef(Require(op, "column"));
        ws.Column(col).Delete();
    }

    private static void ApplyAddColumn(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws      = FindSheet(wb, Require(op, "sheetName"));
        var header  = op.GetValueOrDefault("header") ?? "";
        var defVal  = op.GetValueOrDefault("defaultValue");
        var colRef  = op.GetValueOrDefault("column");

        var colNum  = colRef is not null
            ? ParseColRef(colRef)
            : (ws.LastColumnUsed()?.ColumnNumber() ?? 0) + 1;

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        if (!string.IsNullOrEmpty(header))
            ws.Cell(1, colNum).Value = header;

        if (defVal is not null)
            for (var r = 2; r <= lastRow; r++)
                ws.Cell(r, colNum).Value = defVal;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Column copy
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyCopyColumn(XLWorkbook wb, Dictionary<string, string?> op)
    {
        // Resolve source
        var srcSheetName = op.GetValueOrDefault("sourceSheet")
            ?? op.GetValueOrDefault("sheetName")
            ?? Require(op, "sourceSheet");
        var srcWs  = FindSheet(wb, srcSheetName);
        var srcCol = ParseColRef(Require(op, "sourceColumn"));

        // Resolve target
        var tgtSheetName = op.GetValueOrDefault("targetSheet") ?? srcSheetName;
        var tgtWs        = FindSheet(wb, tgtSheetName);
        var tgtCol       = ParseColRef(Require(op, "targetColumn"));

        var header  = op.GetValueOrDefault("targetHeader");
        var lastRow = srcWs.LastRowUsed()?.RowNumber() ?? 0;

        // When targetHeader is supplied: write it at row 1 then copy data rows (src row 2..N)
        // When absent: copy all rows as-is (src row 1..N → tgt row 1..N)
        if (header is not null)
        {
            tgtWs.Cell(1, tgtCol).Value = header;
            for (var r = 2; r <= lastRow; r++)
                tgtWs.Cell(r, tgtCol).Value = srcWs.Cell(r, srcCol).Value;
        }
        else
        {
            for (var r = 1; r <= lastRow; r++)
                tgtWs.Cell(r, tgtCol).Value = srcWs.Cell(r, srcCol).Value;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Arithmetic column operations (multiply / divide / add-number)
    // ══════════════════════════════════════════════════════════════════════

    private enum ArithOp { Multiply, Divide, Add }

    private static void ApplyArithmeticColumn(XLWorkbook wb, Dictionary<string, string?> op, ArithOp arith)
    {
        var factorStr = arith == ArithOp.Add
            ? Require(op, "value")
            : Require(op, "factor");

        if (!double.TryParse(factorStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var factor))
            throw new InvalidOperationException($"'{(arith == ArithOp.Add ? "value" : "factor")}' must be numeric, got '{factorStr}'.");

        if (arith == ArithOp.Divide && factor == 0)
            throw new InvalidOperationException("Division by zero: 'factor' must not be 0.");

        static double Compute(ArithOp a, double v, double f) => a switch
        {
            ArithOp.Multiply => v * f,
            ArithOp.Divide   => v / f,
            _                => v + f
        };

        bool crossSheet = op.ContainsKey("sourceSheet") || op.ContainsKey("targetSheet");

        if (crossSheet)
        {
            var srcWs   = FindSheet(wb, Require(op, "sourceSheet"));
            var tgtWs   = FindSheet(wb, Require(op, "targetSheet"));
            var srcCol  = ParseColRef(Require(op, "sourceColumn"));
            var tgtCol  = ParseColRef(Require(op, "targetColumn"));
            var header  = op.GetValueOrDefault("targetHeader");
            var lastRow = srcWs.LastRowUsed()?.RowNumber() ?? 1;

            var tgtRow = 1;
            if (header is not null)
            {
                tgtWs.Cell(1, tgtCol).Value = header;
                tgtRow = 2;
            }

            for (var r = 2; r <= lastRow; r++) // row 1 = source header, skip it
            {
                var cell = srcWs.Cell(r, srcCol);
                tgtWs.Cell(tgtRow++, tgtCol).Value = cell.Value.IsNumber
                    ? (XLCellValue)Compute(arith, cell.Value.GetNumber(), factor)
                    : cell.Value;
            }
        }
        else
        {
            var ws      = FindSheet(wb, Require(op, "sheetName"));
            var col     = ParseColRef(op.GetValueOrDefault("column") ?? Require(op, "sourceColumn"));
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (var r = 2; r <= lastRow; r++) // row 1 = header, skip it
            {
                var cell = ws.Cell(r, col);
                if (cell.Value.IsNumber)
                    cell.Value = Compute(arith, cell.Value.GetNumber(), factor);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Row filtering
    // ══════════════════════════════════════════════════════════════════════

    private sealed class FilterCondition
    {
        public string Operator { get; set; } = "";
        public string? Value { get; set; }
        public List<string>? Values { get; set; }
    }

    private void ApplyFilterRows(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var col      = ResolveColumnRef(ws, Require(op, "column"));
        var startRow = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        if (startRow < 1)
            throw new InvalidOperationException("'startRow' must be >= 1.");

        var conditions = ParseFilterConditions(op);
        if (conditions.Count == 0)
        {
            throw new InvalidOperationException(
                "filterRows requires at least one condition in 'conditions', or legacy 'operator'.");
        }

        ValidateFilterConditions(conditions);

        var mode = (op.GetValueOrDefault("mode") ?? "keep").Trim().ToLowerInvariant();
        if (IsTrue(op.GetValueOrDefault("invert")))
            mode = "remove";

        if (mode is not ("keep" or "remove"))
            throw new InvalidOperationException("'mode' must be 'keep' or 'remove'.");

        var joinAnd = !string.Equals(
            op.GetValueOrDefault("conditionJoin"), "or", StringComparison.OrdinalIgnoreCase);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var checkedCount = 0;
        var deleted      = 0;

        for (var r = lastRow; r >= startRow; r--)
        {
            checkedCount++;
            var cellValue = NormalizeFilterValue(GetCellValueAsString(ws.Cell(r, col)));
            var matches   = EvaluateFilterConditions(cellValue, conditions, joinAnd);
            var deleteRow = mode == "keep" ? !matches : matches;

            if (deleteRow)
            {
                ws.Row(r).Delete();
                deleted++;
            }
        }

        var kept = checkedCount - deleted;
        _logger.LogInformation(
            "filterRows: sheet='{Sheet}', column='{Column}' (#{ColNum}), rows checked={Checked}, rows deleted={Deleted}, rows kept={Kept}, mode={Mode}, join={Join}",
            ws.Name,
            op.GetValueOrDefault("column"),
            col,
            checkedCount,
            deleted,
            kept,
            mode,
            joinAnd ? "and" : "or");
    }

    private static List<FilterCondition> ParseFilterConditions(Dictionary<string, string?> op)
    {
        if (op.TryGetValue("conditions", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                return JsonSerializer.Deserialize<List<FilterCondition>>(
                           raw.Trim(),
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? [];
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse 'conditions': {ex.Message}");
            }
        }

        if (op.TryGetValue("operator", out var opName) && !string.IsNullOrWhiteSpace(opName))
        {
            return
            [
                new FilterCondition
                {
                    Operator = opName,
                    Value    = op.GetValueOrDefault("value"),
                }
            ];
        }

        return [];
    }

    private static void ValidateFilterConditions(IReadOnlyList<FilterCondition> conditions)
    {
        var valueRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "equals", "notequals", "contains", "notcontains", "startswith", "endswith",
            "greaterthan", "lessthan", "greaterorequal", "lessorequal",
        };

        var valuesRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "in", "notin",
        };

        for (var i = 0; i < conditions.Count; i++)
        {
            var c = conditions[i];
            if (string.IsNullOrWhiteSpace(c.Operator))
                throw new InvalidOperationException($"Condition [{i + 1}]: 'operator' is required.");

            var op = c.Operator.Trim().ToLowerInvariant();
            if (valueRequired.Contains(op) && string.IsNullOrWhiteSpace(c.Value))
                throw new InvalidOperationException($"Condition [{i + 1}] ('{c.Operator}'): 'value' is required.");

            if (valuesRequired.Contains(op) && (c.Values is null || c.Values.Count == 0))
                throw new InvalidOperationException($"Condition [{i + 1}] ('{c.Operator}'): 'values' is required.");
        }
    }

    private static bool EvaluateFilterConditions(
        string cellValue,
        IReadOnlyList<FilterCondition> conditions,
        bool joinAnd)
    {
        if (conditions.Count == 0)
            return false;

        if (joinAnd)
        {
            foreach (var condition in conditions)
            {
                if (!EvaluateFilterCondition(cellValue, condition))
                    return false;
            }

            return true;
        }

        foreach (var condition in conditions)
        {
            if (EvaluateFilterCondition(cellValue, condition))
                return true;
        }

        return false;
    }

    private static bool EvaluateFilterCondition(string cellValue, FilterCondition condition)
    {
        var op = condition.Operator.Trim().ToLowerInvariant();
        var compareValue = NormalizeFilterValue(condition.Value ?? string.Empty);

        switch (op)
        {
            case "equals":
                return string.Equals(cellValue, compareValue, StringComparison.OrdinalIgnoreCase);
            case "notequals":
                return !string.Equals(cellValue, compareValue, StringComparison.OrdinalIgnoreCase);
            case "contains":
                return cellValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase);
            case "notcontains":
                return !cellValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase);
            case "startswith":
                return cellValue.StartsWith(compareValue, StringComparison.OrdinalIgnoreCase);
            case "endswith":
                return cellValue.EndsWith(compareValue, StringComparison.OrdinalIgnoreCase);
            case "isempty": case "isnull":
                return IsFilterEmpty(cellValue);
            case "isnotempty": case "isnotnull":
                return !IsFilterEmpty(cellValue);
            case "greaterthan":
                return TryParseFilterDecimal(cellValue, out var gtCell) &&
                       TryParseFilterDecimal(compareValue, out var gtVal) &&
                       gtCell > gtVal;
            case "lessthan":
                return TryParseFilterDecimal(cellValue, out var ltCell) &&
                       TryParseFilterDecimal(compareValue, out var ltVal) &&
                       ltCell < ltVal;
            case "greaterorequal":
                return TryParseFilterDecimal(cellValue, out var geCell) &&
                       TryParseFilterDecimal(compareValue, out var geVal) &&
                       geCell >= geVal;
            case "lessorequal":
                return TryParseFilterDecimal(cellValue, out var leCell) &&
                       TryParseFilterDecimal(compareValue, out var leVal) &&
                       leCell <= leVal;
            case "in":
                return condition.Values!.Any(v =>
                    string.Equals(cellValue, NormalizeFilterValue(v), StringComparison.OrdinalIgnoreCase));
            case "notin":
                return !condition.Values!.Any(v =>
                    string.Equals(cellValue, NormalizeFilterValue(v), StringComparison.OrdinalIgnoreCase));
            default:
                return false;
        }
    }

    private static bool IsFilterEmpty(string cellValue)
    {
        if (string.IsNullOrEmpty(cellValue))
            return true;

        return cellValue.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFilterValue(string cellValue)
        => Regex.Replace(cellValue.Trim(), @"\s+", " ");

    private static bool TryParseFilterDecimal(string value, out decimal result)
    {
        result = 0;
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        return decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
               || decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
    }

    /// <summary>
    /// Resolves a column by 1-based number, Excel letter (1–3 chars), or header name in row 1.
    /// </summary>
    private static int ResolveColumnRef(IXLWorksheet ws, string colRef)
    {
        var trimmed = colRef.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new InvalidOperationException("Column reference must not be empty.");

        if (int.TryParse(trimmed, out var colNum) && colNum >= 1)
            return colNum;

        if (Regex.IsMatch(trimmed, @"^[A-Za-z]{1,3}$"))
        {
            try { return ParseColRef(trimmed); }
            catch { /* fall through to header lookup */ }
        }

        var firstCol = ws.FirstColumnUsed()?.ColumnNumber() ?? 1;
        var lastCol  = ws.LastColumnUsed()?.ColumnNumber() ?? 1;

        for (var c = firstCol; c <= lastCol; c++)
        {
            var header = NormalizeFilterValue(GetCellValueAsString(ws.Cell(1, c)));
            if (string.Equals(header, trimmed, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        throw new InvalidOperationException(
            $"Column '{colRef}' not found as letter, number, or header name in row 1.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Row copy (cross-sheet, with optional filter condition)
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyCopyRows(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var srcWs     = FindSheet(wb, Require(op, "sourceSheet"));
        var tgtWs     = FindSheet(wb, Require(op, "targetSheet"));
        var col       = ParseColRef(Require(op, "column"));
        var @operator = op.GetValueOrDefault("operator") ?? "equals";
        var value     = op.GetValueOrDefault("value") ?? "";

        var srcFirst = srcWs.FirstRowUsed();
        if (srcFirst is null) return;

        var srcFirstRow = srcFirst.RowNumber();
        var srcLastRow  = srcWs.LastRowUsed()!.RowNumber();
        var srcFirstCol = srcWs.FirstColumnUsed()?.ColumnNumber() ?? 1;
        var srcLastCol  = srcWs.LastColumnUsed()?.ColumnNumber() ?? 1;

        // Append below whatever already exists in target
        var appendRow = (tgtWs.LastRowUsed()?.RowNumber() ?? 0) + 1;

        // If target is empty, copy source header row first
        if (appendRow == 1)
        {
            for (var c = srcFirstCol; c <= srcLastCol; c++)
                tgtWs.Cell(appendRow, c - srcFirstCol + 1).Value = srcWs.Cell(srcFirstRow, c).Value;
            appendRow++;
        }

        // Copy data rows that satisfy the condition
        for (var r = srcFirstRow + 1; r <= srcLastRow; r++)
        {
            var cellVal = srcWs.Cell(r, col).GetString();
            if (!EvaluateCondition(cellVal, @operator, value)) continue;

            for (var c = srcFirstCol; c <= srcLastCol; c++)
                tgtWs.Cell(appendRow, c - srcFirstCol + 1).Value = srcWs.Cell(r, c).Value;
            appendRow++;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Sort
    // ══════════════════════════════════════════════════════════════════════

    private sealed class SortKeySpec
    {
        public string Column { get; set; } = "";
        public string Direction { get; set; } = "asc";
        public string DataType { get; set; } = "auto";
    }

    private sealed class ResolvedSortKey
    {
        public required int ColumnIndex { get; init; }
        public required string ColumnLabel { get; init; }
        public required bool Descending { get; init; }
        public required string DataType { get; init; }
    }

    private sealed class SortRowSnapshot
    {
        public required XLCellValue[] Values { get; init; }
        public required IXLStyle[] Styles { get; init; }
    }

    private enum SortValueKind { Empty, Number, Date, Text }

    private readonly struct SortComparable : IComparable<SortComparable>
    {
        public SortValueKind Kind { get; }
        public decimal Number { get; }
        public DateTime Date { get; }
        public string Text { get; }

        private SortComparable(SortValueKind kind, decimal number, DateTime date, string text)
        {
            Kind   = kind;
            Number = number;
            Date   = date;
            Text   = text;
        }

        public static SortComparable Empty { get; } = new(SortValueKind.Empty, 0, default, string.Empty);

        public static SortComparable FromNumber(decimal n) => new(SortValueKind.Number, n, default, string.Empty);
        public static SortComparable FromDate(DateTime d)  => new(SortValueKind.Date, 0, d, string.Empty);
        public static SortComparable FromText(string t)    => new(SortValueKind.Text, 0, default, t);

        public int CompareTo(SortComparable other)
        {
            if (Kind == SortValueKind.Empty && other.Kind != SortValueKind.Empty) return 1;
            if (Kind != SortValueKind.Empty && other.Kind == SortValueKind.Empty) return -1;
            if (Kind == SortValueKind.Empty && other.Kind == SortValueKind.Empty) return 0;

            return Kind switch
            {
                SortValueKind.Number => Number.CompareTo(other.Number),
                SortValueKind.Date   => Date.CompareTo(other.Date),
                _                    => string.Compare(Text, other.Text, StringComparison.OrdinalIgnoreCase),
            };
        }
    }

    private void ApplySortRows(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var startRow = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        if (startRow < 1)
            throw new InvalidOperationException("'startRow' must be >= 1.");

        var sorts = ParseSortKeys(op);
        if (sorts.Count == 0)
            throw new InvalidOperationException("sortRows requires at least one item in 'sorts'.");

        ValidateSortKeys(sorts);

        var lastUsed = ws.LastRowUsed();
        if (lastUsed is null || startRow > lastUsed.RowNumber())
        {
            _logger.LogInformation(
                "sortRows: sheet='{Sheet}' — no data rows to sort from startRow={StartRow}",
                ws.Name, startRow);
            return;
        }

        var dataEnd  = lastUsed.RowNumber();
        var firstCol = ws.FirstColumnUsed()?.ColumnNumber() ?? 1;
        var lastCol  = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
        var width    = lastCol - firstCol + 1;

        var resolvedKeys = sorts.Select(s =>
        {
            var colNum = ResolveColumnRef(ws, s.Column);
            if (colNum < firstCol || colNum > lastCol)
            {
                throw new InvalidOperationException(
                    $"Sort column '{s.Column}' (#{colNum}) is outside used range [{firstCol}..{lastCol}].");
            }

            var direction = (s.Direction ?? "asc").Trim().ToLowerInvariant();
            var dataType  = (s.DataType ?? "auto").Trim().ToLowerInvariant();
            return new ResolvedSortKey
            {
                ColumnIndex = colNum - firstCol,
                ColumnLabel = s.Column,
                Descending  = direction == "desc",
                DataType    = dataType,
            };
        }).ToList();

        var rows = new List<SortRowSnapshot>();
        for (var r = startRow; r <= dataEnd; r++)
            rows.Add(CaptureSortRow(ws, r, firstCol, lastCol));

        rows.Sort((a, b) =>
        {
            foreach (var key in resolvedKeys)
            {
                var av = CreateSortComparable(a.Values[key.ColumnIndex], key.DataType);
                var bv = CreateSortComparable(b.Values[key.ColumnIndex], key.DataType);
                var cmp = av.CompareTo(bv);
                if (cmp != 0)
                    return key.Descending ? -cmp : cmp;
            }

            return 0;
        });

        for (var i = 0; i < rows.Count; i++)
            WriteSortRow(ws, startRow + i, firstCol, rows[i]);

        var sortKeySummary = string.Join(", ",
            resolvedKeys.Select(k => $"{k.ColumnLabel} {(k.Descending ? "desc" : "asc")} ({k.DataType})"));

        _logger.LogInformation(
            "sortRows: sheet='{Sheet}', sort keys=[{SortKeys}], rows sorted={Count}",
            ws.Name, sortKeySummary, rows.Count);
    }

    private void ApplyRemoveDuplicates(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws         = FindSheet(wb, Require(op, "sheetName"));
        var startRow   = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        if (startRow < 1)
            throw new InvalidOperationException("'startRow' must be >= 1.");

        var columns = GetColumnList(op, "columns");
        if (columns.Count == 0)
            throw new InvalidOperationException("removeDuplicates requires at least one column in 'columns'.");

        var keep = (op.GetValueOrDefault("keep") ?? "first").Trim().ToLowerInvariant();
        if (keep is not ("first" or "last"))
            throw new InvalidOperationException("'keep' must be 'first' or 'last'.");

        var ignoreCase = op.GetValueOrDefault("ignoreCase") is null || IsTrue(op.GetValueOrDefault("ignoreCase"));
        var trimValues = op.GetValueOrDefault("trimValues") is null || IsTrue(op.GetValueOrDefault("trimValues"));

        var colNums = columns
            .Select(c => ResolveColumnRef(ws, c))
            .ToList();

        var lastUsed = ws.LastRowUsed();
        if (lastUsed is null || startRow > lastUsed.RowNumber())
        {
            _logger.LogInformation(
                "removeDuplicates: sheet='{Sheet}' — no data rows from startRow={StartRow}",
                ws.Name, startRow);
            return;
        }

        var dataEnd     = lastUsed.RowNumber();
        var rowsChecked = dataEnd - startRow + 1;
        var rowsToKeep  = new HashSet<int>();

        if (keep == "first")
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var r = startRow; r <= dataEnd; r++)
            {
                var key = BuildDuplicateKey(ws, r, colNums, ignoreCase, trimValues);
                if (seen.Add(key))
                    rowsToKeep.Add(r);
            }
        }
        else
        {
            var keyToRow = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var r = startRow; r <= dataEnd; r++)
            {
                var key = BuildDuplicateKey(ws, r, colNums, ignoreCase, trimValues);
                keyToRow[key] = r;
            }

            foreach (var row in keyToRow.Values)
                rowsToKeep.Add(row);
        }

        var removed = 0;
        for (var r = dataEnd; r >= startRow; r--)
        {
            if (!rowsToKeep.Contains(r))
            {
                ws.Row(r).Delete();
                removed++;
            }
        }

        var remaining = rowsChecked - removed;
        _logger.LogInformation(
            "removeDuplicates: sheet='{Sheet}', columns=[{Columns}], rows checked={Checked}, duplicates removed={Removed}, rows remaining={Remaining}, keep={Keep}",
            ws.Name,
            string.Join(", ", columns),
            rowsChecked,
            removed,
            remaining,
            keep);
    }

    private static string BuildDuplicateKey(
        IXLWorksheet ws,
        int row,
        IReadOnlyList<int> colNums,
        bool ignoreCase,
        bool trimValues)
    {
        var parts = new string[colNums.Count];
        for (var i = 0; i < colNums.Count; i++)
        {
            var val = GetCellValueAsString(ws.Cell(row, colNums[i]));
            if (trimValues)
                val = val.Trim();
            if (ignoreCase)
                val = val.ToUpperInvariant();
            parts[i] = val;
        }

        return string.Join('\u001F', parts);
    }

    private static List<SortKeySpec> ParseSortKeys(Dictionary<string, string?> op)
    {
        if (!op.TryGetValue("sorts", out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SortKeySpec>>(
                       raw.Trim(),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? [];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse 'sorts': {ex.Message}");
        }
    }

    private static void ValidateSortKeys(IReadOnlyList<SortKeySpec> sorts)
    {
        for (var i = 0; i < sorts.Count; i++)
        {
            var s = sorts[i];
            if (string.IsNullOrWhiteSpace(s.Column))
                throw new InvalidOperationException($"Sort [{i + 1}]: 'column' is required.");

            var direction = (s.Direction ?? "asc").Trim().ToLowerInvariant();
            if (direction is not ("asc" or "desc"))
                throw new InvalidOperationException($"Sort [{i + 1}]: 'direction' must be 'asc' or 'desc'.");

            var dataType = (s.DataType ?? "auto").Trim().ToLowerInvariant();
            if (dataType is not ("text" or "number" or "date" or "auto"))
            {
                throw new InvalidOperationException(
                    $"Sort [{i + 1}]: 'dataType' must be 'text', 'number', 'date', or 'auto'.");
            }
        }
    }

    private static SortRowSnapshot CaptureSortRow(IXLWorksheet ws, int row, int firstCol, int lastCol)
    {
        var width  = lastCol - firstCol + 1;
        var values = new XLCellValue[width];
        var styles = new IXLStyle[width];

        for (var c = firstCol; c <= lastCol; c++)
        {
            var cell = ws.Cell(row, c);
            values[c - firstCol] = cell.Value;
            styles[c - firstCol] = cell.Style;
        }

        return new SortRowSnapshot { Values = values, Styles = styles };
    }

    private static void WriteSortRow(IXLWorksheet ws, int row, int firstCol, SortRowSnapshot snapshot)
    {
        for (var i = 0; i < snapshot.Values.Length; i++)
        {
            var cell = ws.Cell(row, firstCol + i);
            cell.Value = snapshot.Values[i];
            cell.Style   = snapshot.Styles[i];
        }
    }

    private static SortComparable CreateSortComparable(XLCellValue value, string dataType)
    {
        if (IsSortEmpty(value))
            return SortComparable.Empty;

        return dataType switch
        {
            "number" => TryGetSortNumber(value, out var n)
                ? SortComparable.FromNumber(n)
                : SortComparable.Empty,
            "date" => TryGetSortDate(value, out var d)
                ? SortComparable.FromDate(d)
                : SortComparable.Empty,
            "text" => SortComparable.FromText(NormalizeFilterValue(CellValueToString(value))),
            _ => CreateAutoSortComparable(value),
        };
    }

    private static SortComparable CreateAutoSortComparable(XLCellValue value)
    {
        if (TryGetSortNumber(value, out var n))
            return SortComparable.FromNumber(n);
        if (TryGetSortDate(value, out var d))
            return SortComparable.FromDate(d);
        return SortComparable.FromText(NormalizeFilterValue(CellValueToString(value)));
    }

    private static bool IsSortEmpty(XLCellValue value)
    {
        if (value.IsBlank)
            return true;

        var text = NormalizeFilterValue(CellValueToString(value));
        if (string.IsNullOrEmpty(text))
            return true;

        return text.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSortNumber(XLCellValue value, out decimal result)
    {
        result = 0;
        if (value.IsNumber)
        {
            result = (decimal)value.GetNumber();
            return true;
        }

        var text = CellValueToString(value).Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
               || decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryGetSortDate(XLCellValue value, out DateTime result)
    {
        result = default;
        if (value.IsDateTime)
        {
            result = value.GetDateTime();
            return true;
        }

        var text = CellValueToString(value).Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
               || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
    }

    private static void ApplySortByColumn(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws   = FindSheet(wb, Require(op, "sheetName"));
        var col  = ParseColRef(Require(op, "column"));
        var desc = string.Equals(op.GetValueOrDefault("direction"), "desc",
            StringComparison.OrdinalIgnoreCase);

        var firstUsed = ws.FirstRowUsed();
        var lastUsed  = ws.LastRowUsed();
        if (firstUsed is null || lastUsed is null) return;

        var dataStart = firstUsed.RowNumber() + 1; // row 1 = header
        var dataEnd   = lastUsed.RowNumber();
        if (dataStart > dataEnd) return;

        var firstCol = ws.FirstColumnUsed()?.ColumnNumber() ?? 1;
        var lastCol  = ws.LastColumnUsed()?.ColumnNumber() ?? 1;

        // Snapshot data rows
        var rows = new List<XLCellValue[]>();
        for (var r = dataStart; r <= dataEnd; r++)
        {
            var snap = new XLCellValue[lastCol - firstCol + 1];
            for (var c = firstCol; c <= lastCol; c++)
                snap[c - firstCol] = ws.Cell(r, c).Value;
            rows.Add(snap);
        }

        // Sort by key column (0-based index within snapshot)
        var keyIdx = col - firstCol;
        if (keyIdx < 0 || keyIdx >= rows[0].Length)
            throw new InvalidOperationException(
                $"Sort column {col} is outside the used range [{firstCol}..{lastCol}].");

        rows.Sort((a, b) =>
        {
            var av = a[keyIdx];
            var bv = b[keyIdx];
            int cmp;
            if (av.IsNumber && bv.IsNumber)
                cmp = av.GetNumber().CompareTo(bv.GetNumber());
            else if (av.IsDateTime && bv.IsDateTime)
                cmp = av.GetDateTime().CompareTo(bv.GetDateTime());
            else
                cmp = string.Compare(CellValueToString(av), CellValueToString(bv),
                    StringComparison.OrdinalIgnoreCase);
            return desc ? -cmp : cmp;
        });

        // Write sorted data back
        for (var r = 0; r < rows.Count; r++)
        {
            var snap = rows[r];
            for (var c = 0; c < snap.Length; c++)
                ws.Cell(dataStart + r, firstCol + c).Value = snap[c];
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Cell value
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplySetCellValue(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws    = FindSheet(wb, Require(op, "sheetName"));
        var addr  = Require(op, "cell");
        var value = op.GetValueOrDefault("value");
        var cell  = ws.Cell(addr);

        if (value is null)
        {
            cell.Clear();
        }
        else if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                     System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            cell.Value = d;
        }
        else
        {
            cell.Value = value;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Text replacement
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyReplaceText(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws      = FindSheet(wb, Require(op, "sheetName"));
        var oldText = Require(op, "oldText");
        var newText = op.GetValueOrDefault("newText") ?? "";
        var colRef  = op.GetValueOrDefault("column");

        var firstRow = ws.FirstRowUsed()?.RowNumber() ?? 1;
        var lastRow  = ws.LastRowUsed()?.RowNumber() ?? 1;

        if (colRef is not null)
        {
            var col = ParseColRef(colRef);
            for (var r = firstRow; r <= lastRow; r++)
            {
                var cell = ws.Cell(r, col);
                if (cell.Value.IsText)
                    cell.Value = cell.Value.GetText().Replace(oldText, newText);
            }
        }
        else
        {
            var firstCol = ws.FirstColumnUsed()?.ColumnNumber() ?? 1;
            var lastCol  = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
            for (var r = firstRow; r <= lastRow; r++)
                for (var c = firstCol; c <= lastCol; c++)
                {
                    var cell = ws.Cell(r, c);
                    if (cell.Value.IsText)
                        cell.Value = cell.Value.GetText().Replace(oldText, newText);
                }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Remove empty rows
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyRemoveEmptyRows(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var startRow = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;

        var firstCol = ws.FirstColumnUsed()?.ColumnNumber() ?? 1;
        var lastCol  = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
        var lastRow  = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = lastRow; r >= startRow; r--)
        {
            var allBlank = true;
            for (var c = firstCol; c <= lastCol; c++)
            {
                if (!ws.Cell(r, c).Value.IsBlank) { allBlank = false; break; }
            }
            if (allBlank) ws.Row(r).Delete();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 18 — advanced column / formula / format operations
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyInsertColumn(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var afterCol = ParseColRef(Require(op, "afterColumn"));
        var newCol   = ParseColRef(Require(op, "newColumn"));
        var header   = op.GetValueOrDefault("header") ?? "";

        if (afterCol + 1 != newCol)
            throw new InvalidOperationException(
                $"'newColumn' must be the column immediately after 'afterColumn' " +
                $"(after {afterCol} expected column {afterCol + 1}, got {newCol}).");

        ws.Column(newCol).InsertColumnsBefore(1);

        if (!string.IsNullOrEmpty(header))
            ws.Cell(1, newCol).Value = header;
    }

    private static void ApplyConvertColumnToNumber(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var col      = ParseColRef(Require(op, "column"));
        var startRow = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var format   = op.GetValueOrDefault("numberFormat") ?? "#,##0.00";
        var lastRow  = ws.LastRowUsed()?.RowNumber() ?? startRow;

        for (var r = startRow; r <= lastRow; r++)
        {
            var cell = ws.Cell(r, col);
            if (cell.Value.IsText)
            {
                var text = cell.GetString().Trim();
                if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var num) ||
                    double.TryParse(text, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.CurrentCulture, out num))
                {
                    cell.Value = num;
                }
            }

            cell.Style.NumberFormat.Format = format;
        }
    }

    private static void ApplySetFormula(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws      = FindSheet(wb, Require(op, "sheetName"));
        var formula = Require(op, "formula").Trim();
        if (!formula.StartsWith('='))
            formula = "=" + formula;
        ws.Cell(Require(op, "cell")).FormulaA1 = formula;
    }

    private static void ApplyFillFormulaDown(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var col      = ParseColRef(Require(op, "column"));
        var startRow = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var endRow   = int.TryParse(op.GetValueOrDefault("endRow"), out var er)
            ? er
            : ws.LastRowUsed()?.RowNumber() ?? startRow;
        var template = Require(op, "formulaTemplate");

        if (startRow > endRow)
            throw new InvalidOperationException(
                $"'startRow' ({startRow}) must not be greater than 'endRow' ({endRow}).");

        for (var r = startRow; r <= endRow; r++)
        {
            var formula = template.Replace("{row}", r.ToString(), StringComparison.OrdinalIgnoreCase);
            if (!formula.StartsWith('='))
                formula = "=" + formula;
            ws.Cell(r, col).FormulaA1 = formula;
        }
    }

    private static void ApplySetColumnFormat(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws     = FindSheet(wb, Require(op, "sheetName"));
        var col    = ParseColRef(Require(op, "column"));
        var format = Require(op, "format");
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 1; r <= lastRow; r++)
            ws.Cell(r, col).Style.NumberFormat.Format = format;
    }

    private static void ApplySetHeader(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws     = FindSheet(wb, Require(op, "sheetName"));
        var col    = ParseColRef(Require(op, "column"));
        var header = Require(op, "header");
        ws.Cell(1, col).Value = header;
    }

    private static void ApplyAutoFitColumns(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws = FindSheet(wb, Require(op, "sheetName"));
        ws.Columns().AdjustToContents();
    }

    private static void ApplyCreateSheetFromColumns(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var srcWs   = FindSheet(wb, Require(op, "sourceSheet"));
        var tgtName = Require(op, "targetSheet");
        var columns = GetColumnList(op, "columns");
        if (columns.Count == 0)
            throw new InvalidOperationException("'columns' must list at least one column.");

        var existing = wb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(tgtName, StringComparison.OrdinalIgnoreCase));
        existing?.Delete();

        var tgtWs     = wb.Worksheets.Add(tgtName);
        var srcLastRow = srcWs.LastRowUsed()?.RowNumber() ?? 1;

        for (var i = 0; i < columns.Count; i++)
        {
            var srcCol = ParseColRef(columns[i]);
            var tgtCol = i + 1;
            for (var r = 1; r <= srcLastRow; r++)
                tgtWs.Cell(r, tgtCol).Value = srcWs.Cell(r, srcCol).Value;
        }
    }

    private static void ApplySetCellStyle(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws    = FindSheet(wb, Require(op, "sheetName"));
        var range = ws.Range(Require(op, "range"));

        if (IsTrue(op.GetValueOrDefault("bold")))
            range.Style.Font.Bold = true;

        var bg = op.GetValueOrDefault("backgroundColor");
        if (!string.IsNullOrWhiteSpace(bg))
            range.Style.Fill.BackgroundColor = XLColor.FromHtml(bg);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 20 — column transform / expression operations
    // ══════════════════════════════════════════════════════════════════════

    private static void ApplyTransformColumn(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws         = FindSheet(wb, Require(op, "sheetName"));
        var srcCol     = ParseColRef(Require(op, "sourceColumn"));
        var tgtCol     = ParseColRef(Require(op, "targetColumn"));
        var startRow   = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var expression = Require(op, "expression");
        var header     = op.GetValueOrDefault("targetHeader");
        var format     = op.GetValueOrDefault("numberFormat");
        var lastRow    = ws.LastRowUsed()?.RowNumber() ?? startRow;

        if (!string.IsNullOrEmpty(header))
            ws.Cell(1, tgtCol).Value = header;

        for (var r = startRow; r <= lastRow; r++)
        {
            var input  = GetCellValueAsString(ws.Cell(r, srcCol));
            var result = ColumnExpressionEvaluator.Evaluate(expression, input);
            WriteExpressionResult(ws.Cell(r, tgtCol), result, format);
        }
    }

    private static void ApplyCopyColumnValues(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var srcWs      = FindSheet(wb, Require(op, "sourceSheet"));
        var tgtWs      = FindSheet(wb, Require(op, "targetSheet"));
        var srcCol     = ParseColRef(Require(op, "sourceColumn"));
        var tgtCol     = ParseColRef(Require(op, "targetColumn"));
        var startRow   = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var includeHeader = IsTrue(op.GetValueOrDefault("includeHeader"));
        var firstRow   = includeHeader ? 1 : startRow;
        var lastRow    = srcWs.LastRowUsed()?.RowNumber() ?? firstRow;

        for (var r = firstRow; r <= lastRow; r++)
            CopyCellValue(srcWs.Cell(r, srcCol), tgtWs.Cell(r, tgtCol));
    }

    private static void ApplyReplaceColumnValues(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var srcCol   = ParseColRef(Require(op, "sourceColumn"));
        var tgtCol   = ParseColRef(Require(op, "targetColumn"));
        var startRow = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var lastRow  = ws.LastRowUsed()?.RowNumber() ?? startRow;

        for (var r = startRow; r <= lastRow; r++)
            CopyCellValue(ws.Cell(r, srcCol), ws.Cell(r, tgtCol));
    }

    private void ApplyCalculateFormulaValues(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var ws       = FindSheet(wb, Require(op, "sheetName"));
        var srcCol   = ParseColRef(Require(op, "sourceFormulaColumn"));
        var tgtCol   = ParseColRef(Require(op, "targetColumn"));
        var startRow = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var format   = op.GetValueOrDefault("numberFormat");
        var lastRow  = ws.LastRowUsed()?.RowNumber() ?? startRow;

        for (var r = startRow; r <= lastRow; r++)
        {
            var srcCell = ws.Cell(r, srcCol);
            var tgtCell = ws.Cell(r, tgtCol);
            var value   = GetFormulaCachedValue(srcCell, r);

            if (value.IsNumber)
                tgtCell.Value = value.GetNumber();
            else if (value.IsText)
                tgtCell.Value = value.GetText();
            else if (value.IsDateTime)
                tgtCell.Value = value.GetDateTime();
            else if (value.IsBoolean)
                tgtCell.Value = value.GetBoolean();
            else if (!value.IsBlank)
                tgtCell.Value = value;

            if (!string.IsNullOrEmpty(format))
                tgtCell.Style.NumberFormat.Format = format;
        }
    }

    private XLCellValue GetFormulaCachedValue(IXLCell cell, int row)
    {
        if (!cell.HasFormula)
            return cell.Value;

        if (!cell.Value.IsBlank)
            return cell.Value;

        _logger.LogWarning(
            "excel.transform: row {Row} formula has no cached value and cannot be evaluated: {Formula}",
            row, cell.FormulaA1);
        return cell.Value;
    }

    private static void CopyCellValue(IXLCell src, IXLCell tgt)
    {
        if (src.HasFormula)
        {
            var cached = src.Value;
            if (cached.IsNumber)      { tgt.Value = cached.GetNumber(); return; }
            if (cached.IsText)        { tgt.Value = cached.GetText(); return; }
            if (cached.IsDateTime)    { tgt.Value = cached.GetDateTime(); return; }
            if (cached.IsBoolean)     { tgt.Value = cached.GetBoolean(); return; }
        }

        tgt.Value = src.Value;
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

    private static void WriteExpressionResult(IXLCell cell, object result, string? numberFormat)
    {
        switch (result)
        {
            case double d:
                cell.Value = d;
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case decimal m:
                cell.Value = (double)m;
                break;
            default:
                cell.Value = result.ToString() ?? string.Empty;
                break;
        }

        if (!string.IsNullOrEmpty(numberFormat))
            cell.Style.NumberFormat.Format = numberFormat;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 21 — lookup / VLookup operations
    // ══════════════════════════════════════════════════════════════════════

    private sealed class LookupMappingEntry
    {
        public string ReferenceColumn { get; set; } = "";
        public string TargetColumn { get; set; } = "";
        public string? TargetHeader { get; set; }
    }

    private void ApplyLookupColumn(XLWorkbook wb, Dictionary<string, string?> op)
        => RunSingleColumnLookup(wb, op, replaceInPlace: false);

    private void ApplyReplaceWithLookupResult(XLWorkbook wb, Dictionary<string, string?> op)
        => RunSingleColumnLookup(wb, op, replaceInPlace: true);

    private void RunSingleColumnLookup(XLWorkbook wb, Dictionary<string, string?> op, bool replaceInPlace)
    {
        var srcWs        = FindSheet(wb, Require(op, "sourceSheet"));
        var refWs        = FindSheet(wb, Require(op, "referenceSheet"));
        var lookupCol    = ParseColRef(Require(op, "lookupColumn"));
        var refKeyCol    = ParseColRef(Require(op, "referenceKeyColumn"));
        var refReturnCol = ParseColRef(Require(op, "referenceReturnColumn"));
        var tgtCol       = ParseColRef(
            replaceInPlace
                ? (op.GetValueOrDefault("targetColumn") ?? Require(op, "lookupColumn"))
                : Require(op, "targetColumn"));
        var startRow     = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var ignoreCase   = op.GetValueOrDefault("ignoreCase") is null || IsTrue(op.GetValueOrDefault("ignoreCase"));
        var notFound     = op.GetValueOrDefault("notFoundValue") ?? "";
        var header       = replaceInPlace ? null : op.GetValueOrDefault("targetHeader");
        var lastRow      = srcWs.LastRowUsed()?.RowNumber() ?? startRow;

        var map = BuildLookupDictionary(refWs, refKeyCol, refReturnCol, ignoreCase);
        _logger.LogInformation(
            "lookupColumn: dictionary size={DictSize}, ignoreCase={IgnoreCase}",
            map.Count, ignoreCase);

        if (!string.IsNullOrEmpty(header))
            srcWs.Cell(1, tgtCol).Value = header;

        var matches = 0;
        var missing = 0;
        var processed = 0;

        for (var r = startRow; r <= lastRow; r++)
        {
            processed++;
            var key = NormalizeLookupKey(GetCellValueAsString(srcWs.Cell(r, lookupCol)), ignoreCase);
            if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var found))
            {
                srcWs.Cell(r, tgtCol).Value = found;
                matches++;
            }
            else
            {
                srcWs.Cell(r, tgtCol).Value = notFound;
                missing++;
            }
        }

        _logger.LogInformation(
            "lookupColumn: rows processed={Processed}, matches found={Matches}, matches missing={Missing}",
            processed, matches, missing);
    }

    private void ApplyMultiColumnLookup(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var srcWs      = FindSheet(wb, Require(op, "sourceSheet"));
        var refWs      = FindSheet(wb, Require(op, "referenceSheet"));
        var lookupCol  = ParseColRef(Require(op, "lookupColumn"));
        var refKeyCol  = ParseColRef(Require(op, "referenceKeyColumn"));
        var startRow   = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var ignoreCase = op.GetValueOrDefault("ignoreCase") is null || IsTrue(op.GetValueOrDefault("ignoreCase"));
        var mappings   = GetMappingList(op);
        if (mappings.Count == 0)
            throw new InvalidOperationException("'mappings' must contain at least one entry.");

        var refReturnCols = mappings
            .Select(m => ParseColRef(m.ReferenceColumn))
            .Distinct()
            .ToList();

        var map = BuildMultiColumnLookupDictionary(refWs, refKeyCol, refReturnCols, ignoreCase);
        _logger.LogInformation(
            "multiColumnLookup: dictionary size={DictSize}, mappings={MappingCount}, ignoreCase={IgnoreCase}",
            map.Count, mappings.Count, ignoreCase);

        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.TargetHeader))
                srcWs.Cell(1, ParseColRef(mapping.TargetColumn)).Value = mapping.TargetHeader;
        }

        var lastRow   = srcWs.LastRowUsed()?.RowNumber() ?? startRow;
        var matches   = 0;
        var missing   = 0;
        var processed = 0;

        for (var r = startRow; r <= lastRow; r++)
        {
            processed++;
            var key = NormalizeLookupKey(GetCellValueAsString(srcWs.Cell(r, lookupCol)), ignoreCase);
            if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var rowValues))
            {
                matches++;
                foreach (var mapping in mappings)
                {
                    var refCol = ParseColRef(mapping.ReferenceColumn);
                    var tgtCol = ParseColRef(mapping.TargetColumn);
                    rowValues.TryGetValue(refCol, out var val);
                    srcWs.Cell(r, tgtCol).Value = val ?? string.Empty;
                }
            }
            else
            {
                missing++;
                foreach (var mapping in mappings)
                    srcWs.Cell(r, ParseColRef(mapping.TargetColumn)).Value = string.Empty;
            }
        }

        _logger.LogInformation(
            "multiColumnLookup: rows processed={Processed}, matches found={Matches}, matches missing={Missing}",
            processed, matches, missing);
    }

    private void ApplyCompositeLookup(XLWorkbook wb, Dictionary<string, string?> op)
    {
        var srcWs           = FindSheet(wb, Require(op, "sourceSheet"));
        var refWs           = FindSheet(wb, Require(op, "referenceSheet"));
        var lookupCols      = GetColumnList(op, "lookupColumns");
        var refKeyCols      = GetColumnList(op, "referenceKeyColumns");
        var refReturnCol    = ParseColRef(Require(op, "referenceReturnColumn"));
        var tgtCol          = ParseColRef(Require(op, "targetColumn"));
        var startRow        = int.TryParse(op.GetValueOrDefault("startRow"), out var sr) ? sr : 2;
        var separator       = op.GetValueOrDefault("separator") ?? "|";
        var ignoreCase      = op.GetValueOrDefault("ignoreCase") is null || IsTrue(op.GetValueOrDefault("ignoreCase"));

        if (lookupCols.Count == 0)
            throw new InvalidOperationException("'lookupColumns' must list at least one column.");
        if (refKeyCols.Count == 0)
            throw new InvalidOperationException("'referenceKeyColumns' must list at least one column.");
        if (lookupCols.Count != refKeyCols.Count)
            throw new InvalidOperationException(
                $"'lookupColumns' count ({lookupCols.Count}) must match 'referenceKeyColumns' count ({refKeyCols.Count}).");

        var srcKeyCols = lookupCols.Select(ParseColRef).ToList();
        var refKeyColNums = refKeyCols.Select(ParseColRef).ToList();

        var map = BuildCompositeLookupDictionary(
            refWs, refKeyColNums, refReturnCol, separator, ignoreCase);
        _logger.LogInformation(
            "compositeLookup: dictionary size={DictSize}, keyParts={KeyParts}, separator='{Separator}'",
            map.Count, lookupCols.Count, separator);

        var header = op.GetValueOrDefault("targetHeader");
        if (!string.IsNullOrEmpty(header))
            srcWs.Cell(1, tgtCol).Value = header;

        var lastRow   = srcWs.LastRowUsed()?.RowNumber() ?? startRow;
        var matches   = 0;
        var missing   = 0;
        var processed = 0;

        for (var r = startRow; r <= lastRow; r++)
        {
            processed++;
            var key = BuildCompositeKey(srcWs, r, srcKeyCols, separator, ignoreCase);
            if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var found))
            {
                srcWs.Cell(r, tgtCol).Value = found;
                matches++;
            }
            else
            {
                srcWs.Cell(r, tgtCol).Value = op.GetValueOrDefault("notFoundValue") ?? string.Empty;
                missing++;
            }
        }

        _logger.LogInformation(
            "compositeLookup: rows processed={Processed}, matches found={Matches}, matches missing={Missing}",
            processed, matches, missing);
    }

    private static Dictionary<string, string> BuildLookupDictionary(
        IXLWorksheet refWs, int keyCol, int returnCol, bool ignoreCase, int refStartRow = 2)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var map      = new Dictionary<string, string>(comparer);
        var lastRow  = refWs.LastRowUsed()?.RowNumber() ?? refStartRow;

        for (var r = refStartRow; r <= lastRow; r++)
        {
            var key = NormalizeLookupKey(GetCellValueAsString(refWs.Cell(r, keyCol)), ignoreCase);
            if (string.IsNullOrEmpty(key))
                continue;
            map.TryAdd(key, GetCellValueAsString(refWs.Cell(r, returnCol)));
        }

        return map;
    }

    private static Dictionary<string, Dictionary<int, string>> BuildMultiColumnLookupDictionary(
        IXLWorksheet refWs, int keyCol, IReadOnlyList<int> returnCols, bool ignoreCase, int refStartRow = 2)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var map      = new Dictionary<string, Dictionary<int, string>>(comparer);
        var lastRow  = refWs.LastRowUsed()?.RowNumber() ?? refStartRow;

        for (var r = refStartRow; r <= lastRow; r++)
        {
            var key = NormalizeLookupKey(GetCellValueAsString(refWs.Cell(r, keyCol)), ignoreCase);
            if (string.IsNullOrEmpty(key))
                continue;

            if (!map.TryGetValue(key, out var rowValues))
            {
                rowValues = new Dictionary<int, string>();
                map[key] = rowValues;
            }

            foreach (var col in returnCols)
                rowValues[col] = GetCellValueAsString(refWs.Cell(r, col));
        }

        return map;
    }

    private static Dictionary<string, string> BuildCompositeLookupDictionary(
        IXLWorksheet refWs,
        IReadOnlyList<int> keyCols,
        int returnCol,
        string separator,
        bool ignoreCase,
        int refStartRow = 2)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var map      = new Dictionary<string, string>(comparer);
        var lastRow  = refWs.LastRowUsed()?.RowNumber() ?? refStartRow;

        for (var r = refStartRow; r <= lastRow; r++)
        {
            var key = BuildCompositeKey(refWs, r, keyCols, separator, ignoreCase);
            if (string.IsNullOrEmpty(key))
                continue;
            map.TryAdd(key, GetCellValueAsString(refWs.Cell(r, returnCol)));
        }

        return map;
    }

    private static string BuildCompositeKey(
        IXLWorksheet ws, int row, IReadOnlyList<int> cols, string separator, bool ignoreCase)
    {
        var parts = cols
            .Select(c => NormalizeLookupKey(GetCellValueAsString(ws.Cell(row, c)), ignoreCase))
            .ToArray();
        return string.Join(separator, parts);
    }

    private static string NormalizeLookupKey(string value, bool ignoreCase)
    {
        var trimmed = value.Trim();
        return ignoreCase ? trimmed.ToUpperInvariant() : trimmed;
    }

    private static List<LookupMappingEntry> GetMappingList(Dictionary<string, string?> op, string key = "mappings")
    {
        if (!op.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<LookupMappingEntry>>(
                       raw.Trim(),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? [];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse '{key}': {ex.Message}");
        }
    }

    private static class ColumnExpressionEvaluator
    {
        private static readonly Regex FunctionCallRegex =
            new(@"(\w+)\(([^()]*)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static object Evaluate(string expression, string rawValue)
        {
            var expr = expression.Trim();
            if (string.IsNullOrEmpty(expr))
                throw new InvalidOperationException("Expression must not be empty.");

            while (true)
            {
                Match? innermost = null;
                foreach (Match match in FunctionCallRegex.Matches(expr))
                {
                    if (innermost is null || match.Groups[2].Length < innermost.Groups[2].Length)
                        innermost = match;
                }

                if (innermost is null)
                    break;

                var fnName = innermost.Groups[1].Value;
                var args   = innermost.Groups[2].Value;
                var result = InvokeFunction(fnName, args, rawValue);
                expr = expr[..innermost.Index]
                     + FormatIntermediate(result)
                     + expr[(innermost.Index + innermost.Length)..];
            }

            return EvaluateFinalExpression(expr.Trim(), rawValue);
        }

        private static object InvokeFunction(string name, string argsRaw, string rawValue)
        {
            var args = SplitFunctionArgs(argsRaw);
            return name.ToLowerInvariant() switch
            {
                "tonumber" => ToNumber(ResolveArg(args, 0, rawValue)),
                "totext"   => ToText(ResolveArg(args, 0, rawValue)),
                "trim"     => Trim(ResolveArg(args, 0, rawValue)),
                "removeleadingzeros" => RemoveLeadingZeros(ResolveArg(args, 0, rawValue)),
                "divide"   => ToNumber(ResolveArg(args, 0, rawValue))
                              / ParseNumericArg(ResolveArg(args, 1, rawValue), "divide"),
                "multiply" => ToNumber(ResolveArg(args, 0, rawValue))
                              * ParseNumericArg(ResolveArg(args, 1, rawValue), "multiply"),
                "add"      => ToNumber(ResolveArg(args, 0, rawValue))
                              + ParseNumericArg(ResolveArg(args, 1, rawValue), "add"),
                "subtract" => ToNumber(ResolveArg(args, 0, rawValue))
                              - ParseNumericArg(ResolveArg(args, 1, rawValue), "subtract"),
                _ => throw new InvalidOperationException($"Unsupported expression function '{name}'.")
            };
        }

        private static string ResolveArg(string[] args, int index, string rawValue)
        {
            if (args.Length <= index)
                throw new InvalidOperationException($"Function expected argument {index + 1}.");

            var arg = args[index].Trim();
            if (string.Equals(arg, "value", StringComparison.OrdinalIgnoreCase))
                return rawValue;
            return arg;
        }

        private static double ParseNumericArg(string arg, string fn)
        {
            if (double.TryParse(arg, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                return n;
            if (double.TryParse(arg, NumberStyles.Any, CultureInfo.CurrentCulture, out n))
                return n;
            throw new InvalidOperationException($"'{fn}' second argument must be numeric, got '{arg}'.");
        }

        private static string[] SplitFunctionArgs(string args)
        {
            var parts   = new List<string>();
            var current = new StringBuilder();
            var depth   = 0;

            foreach (var ch in args)
            {
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                else if (ch == ',' && depth == 0)
                {
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            parts.Add(current.ToString().Trim());
            return parts.ToArray();
        }

        private static double ToNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return 0;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                return n;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out n))
                return n;

            var trimmed = s.Trim();
            if (trimmed.Length > 0 && trimmed.All(char.IsDigit))
                return double.Parse(trimmed, CultureInfo.InvariantCulture);

            throw new InvalidOperationException($"toNumber could not parse '{s}'.");
        }

        private static string ToText(string s) => s;

        private static string Trim(string s) => s.Trim();

        private static string RemoveLeadingZeros(string s)
        {
            var trimmed = s.Trim();
            if (trimmed.Length == 0) return trimmed;

            var negative = trimmed.StartsWith('-');
            if (negative) trimmed = trimmed[1..];

            var i = 0;
            while (i < trimmed.Length - 1 && trimmed[i] == '0') i++;
            var result = trimmed[i..];
            return negative ? "-" + result : result;
        }

        private static string FormatIntermediate(object result) => result switch
        {
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f  => f.ToString(CultureInfo.InvariantCulture),
            int i    => i.ToString(CultureInfo.InvariantCulture),
            long l   => l.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            _        => result.ToString() ?? string.Empty
        };

        private static object EvaluateFinalExpression(string expr, string rawValue)
        {
            if (string.IsNullOrEmpty(expr))
                return rawValue;

            if (string.Equals(expr, "value", StringComparison.OrdinalIgnoreCase))
                return rawValue;

            if (double.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out var direct))
                return direct;

            if (expr.IndexOfAny(['+', '-', '*', '/']) >= 0)
            {
                try
                {
                    var computed = new DataTable().Compute(expr, null);
                    if (computed is null)
                        throw new InvalidOperationException($"Expression '{expr}' did not produce a value.");
                    return computed switch
                    {
                        double d  => d,
                        decimal m => (double)m,
                        int i     => (double)i,
                        long l    => (double)l,
                        float f   => (double)f,
                        _         => Convert.ToDouble(computed, CultureInfo.InvariantCulture)
                    };
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to evaluate expression '{expr}': {ex.Message}");
                }
            }

            return expr;
        }
    }

    private async Task ApplyImportTextToSheetAsync(
        XLWorkbook wb,
        Dictionary<string, string?> op,
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        var textArtifactRaw = Require(op, "textArtifactName");
        var textArtifactName = context.Interpolate(textArtifactRaw);
        if (!context.HasArtifact(textArtifactName))
            throw new InvalidOperationException(
                $"Text artifact '{textArtifactName}' not found in context.");

        var targetSheet = context.Interpolate(Require(op, "targetSheet"));
        var delimiter   = ResolveDelimiter(op);
        var quoteChar   = ResolveQuoteChar(op);
        var encoding    = GetTextEncoding(op.GetValueOrDefault("encoding") ?? "UTF-8");
        var trimValues  = op.GetValueOrDefault("trimValues") is null
            || IsTrue(op.GetValueOrDefault("trimValues"));
        var overwrite   = op.GetValueOrDefault("overwrite") is null
            || IsTrue(op.GetValueOrDefault("overwrite"));
        var parseNumbers = IsTrue(op.GetValueOrDefault("parseNumbers"));
        var startCell    = op.GetValueOrDefault("startCell") ?? "A1";

        var textArtifact = context.GetArtifact(textArtifactName);
        var bytes        = await _store.ReadAllBytesAsync(textArtifact.StoragePath, cancellationToken);
        var text         = encoding.GetString(bytes);

        var rows = ParseDelimitedText(text, delimiter, quoteChar, trimValues);
        if (rows.Count == 0)
            return;

        var ws = wb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(targetSheet, StringComparison.OrdinalIgnoreCase));
        if (ws is null)
            ws = wb.Worksheets.Add(targetSheet);
        else if (overwrite)
            ws.Clear();

        var anchor = ws.Cell(startCell);
        var startRow = anchor.Address.RowNumber;
        var startCol = anchor.Address.ColumnNumber;

        for (var r = 0; r < rows.Count; r++)
        {
            var cells = rows[r];
            for (var c = 0; c < cells.Count; c++)
                WriteImportedCell(ws.Cell(startRow + r, startCol + c), cells[c], parseNumbers);
        }
    }

    private static void WriteImportedCell(IXLCell cell, string value, bool parseNumbers)
    {
        if (parseNumbers &&
            double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            cell.Value = num;
            return;
        }

        if (parseNumbers &&
            double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.CurrentCulture, out num))
        {
            cell.Value = num;
            return;
        }

        cell.Style.NumberFormat.Format = "@";
        cell.Value = value;
    }

    private static char ResolveDelimiter(Dictionary<string, string?> op)
    {
        var raw = op.GetValueOrDefault("delimiter") ?? ",";
        return raw.ToLowerInvariant() switch
        {
            "comma"     => ',',
            "semicolon" => ';',
            "tab"       => '\t',
            "pipe"      => '|',
            "custom"    => ResolveCustomDelimiter(op.GetValueOrDefault("customDelimiter")),
            _           => raw.Length == 1 ? raw[0]
                          : raw.Length == 2 && raw[1] == 't' && raw[0] == '\\' ? '\t'
                          : ','
        };
    }

    private static char ResolveCustomDelimiter(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            throw new InvalidOperationException(
                "'customDelimiter' is required when delimiter is 'custom'.");
        return raw == "\\t" ? '\t' : raw[0];
    }

    private static char ResolveQuoteChar(Dictionary<string, string?> op)
    {
        var raw = op.GetValueOrDefault("quoteChar");
        if (string.IsNullOrEmpty(raw) || raw.Equals("none", StringComparison.OrdinalIgnoreCase))
            return '\0';
        return raw[0];
    }

    private static Encoding GetTextEncoding(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("utf8", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8;

        try { return Encoding.GetEncoding(name); }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unsupported encoding '{name}': {ex.Message}");
        }
    }

    private static List<List<string>> ParseDelimitedText(
        string text, char delimiter, char quoteChar, bool trimValues)
    {
        var rows   = new List<List<string>>();
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;

        void FinishField()
        {
            var val = trimValues ? current.ToString().Trim() : current.ToString();
            fields.Add(val);
            current.Clear();
        }

        void FinishRow()
        {
            FinishField();
            if (fields.Count > 0 || rows.Count > 0)
                rows.Add(new List<string>(fields));
            fields.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (quoteChar != '\0' && inQuote)
            {
                if (ch == quoteChar)
                {
                    if (i + 1 < text.Length && text[i + 1] == quoteChar)
                    {
                        current.Append(quoteChar);
                        i++;
                    }
                    else inQuote = false;
                }
                else current.Append(ch);
                continue;
            }

            if (quoteChar != '\0' && ch == quoteChar)
            {
                inQuote = true;
                continue;
            }

            if (ch == delimiter)
            {
                FinishField();
                continue;
            }

            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                FinishRow();
                continue;
            }

            if (ch == '\n')
            {
                FinishRow();
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0 || fields.Count > 0)
            FinishRow();

        return rows;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Operations parsing
    // ══════════════════════════════════════════════════════════════════════

    private static List<Dictionary<string, string?>> ParseOperations(object raw)
    {
        var result = new List<Dictionary<string, string?>>();

        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in item.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String  => prop.Value.GetString(),
                        JsonValueKind.Number  => prop.Value.ToString(),
                        JsonValueKind.True    => "true",
                        JsonValueKind.False   => "false",
                        JsonValueKind.Null    => null,
                        JsonValueKind.Array   => prop.Value.GetRawText(),
                        _                     => prop.Value.ToString()
                    };
                }
                result.Add(dict);
            }
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════

    private static IXLWorksheet FindSheet(XLWorkbook wb, string name)
    {
        var ws = wb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return ws ?? throw new InvalidOperationException(
            $"Sheet '{name}' not found. Available: " +
            $"[{string.Join(", ", wb.Worksheets.Select(s => s.Name))}]");
    }

    /// <summary>Converts a column reference (letter "C" or number "3") to a 1-based column number.</summary>
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

    private static string Require(Dictionary<string, string?> op, string key)
    {
        if (!op.TryGetValue(key, out var val) || val is null)
            throw new InvalidOperationException(
                $"Operation '{op.GetValueOrDefault("type")}' requires '{key}'.");
        return val;
    }

    private static bool IsTrue(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static List<string> GetColumnList(Dictionary<string, string?> op, string key)
    {
        if (!op.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(trimmed) ?? [];
            }
            catch
            {
                throw new InvalidOperationException($"'{key}' must be a JSON array of column letters.");
            }
        }

        return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool EvaluateCondition(string cellValue, string @operator, string compareValue)
    {
        switch (@operator.ToLowerInvariant())
        {
            case "equals":
                return string.Equals(cellValue, compareValue, StringComparison.OrdinalIgnoreCase);
            case "notequals":
                return !string.Equals(cellValue, compareValue, StringComparison.OrdinalIgnoreCase);
            case "contains":
                return cellValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase);
            case "notcontains":
                return !cellValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase);
            case "startswith":
                return cellValue.StartsWith(compareValue, StringComparison.OrdinalIgnoreCase);
            case "endswith":
                return cellValue.EndsWith(compareValue, StringComparison.OrdinalIgnoreCase);
            case "isempty": case "isnull":
                return string.IsNullOrEmpty(cellValue);
            case "isnotempty": case "isnotnull":
                return !string.IsNullOrEmpty(cellValue);
            case "greaterthan":
                return double.TryParse(cellValue, out var lgt) &&
                       double.TryParse(compareValue, out var rgt) && lgt > rgt;
            case "lessthan":
                return double.TryParse(cellValue, out var llt) &&
                       double.TryParse(compareValue, out var rlt) && llt < rlt;
            case "greaterorequal":
                return double.TryParse(cellValue, out var lge) &&
                       double.TryParse(compareValue, out var rge) && lge >= rge;
            case "lessorequal":
                return double.TryParse(cellValue, out var lle) &&
                       double.TryParse(compareValue, out var rle) && lle <= rle;
            default:
                return false;
        }
    }

    private static string CellValueToString(XLCellValue v)
    {
        if (v.IsBlank)    return string.Empty;
        if (v.IsText)     return v.GetText();
        if (v.IsNumber)   return v.GetNumber().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (v.IsDateTime) return v.GetDateTime().ToString("O");
        if (v.IsBoolean)  return v.GetBoolean().ToString();
        if (v.IsTimeSpan) return v.GetTimeSpan().ToString(@"hh\:mm\:ss");
        return string.Empty;
    }
}
