using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Extracts values or rows from HTML or plain-text tables in a workflow variable (typically mail body).
/// </summary>
public sealed class MailExtractTableStepHandler : IStepHandler
{
    private readonly ILogger<MailExtractTableStepHandler> _logger;

    public string HandlerType => "mail.extract-table";

    public MailExtractTableStepHandler(ILogger<MailExtractTableStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var sourceVar = config.GetValueOrDefault("sourceVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(sourceVar))
            return Task.FromResult(StepResult.Fail("mail.extract-table: 'sourceVariable' is required."));

        sourceVar = MailVariableHelper.NormalizeVariableName(context.Interpolate(sourceVar));

        var outputVar = config.GetValueOrDefault("outputVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(outputVar))
            return Task.FromResult(StepResult.Fail("mail.extract-table: 'outputVariable' is required."));

        var modeRaw = config.GetValueOrDefault("mode")?.ToString()?.Trim() ?? "cell";
        var mode    = modeRaw.ToLowerInvariant() switch
        {
            "cell"        => "cell",
            "headercell"  => "headercell",
            "lookup"      => "lookup",
            "tablejson"   => "tablejson",
            _             => string.Empty,
        };

        if (string.IsNullOrEmpty(mode))
        {
            return Task.FromResult(StepResult.Fail(
                "mail.extract-table: 'mode' must be 'cell', 'headerCell', 'lookup', or 'tableJson'."));
        }

        var tableIndex   = ParseInt(config.GetValueOrDefault("tableIndex"), 0);
        var rowIndex     = ParseInt(config.GetValueOrDefault("rowIndex"), 0);
        var columnIndex  = ParseInt(config.GetValueOrDefault("columnIndex"), 0);
        var ignoreCase   = ParseBool(config.GetValueOrDefault("ignoreCase"), defaultValue: true);

        var lookupColumn = config.GetValueOrDefault("lookupColumn")?.ToString()?.Trim() ?? string.Empty;
        var lookupValue  = config.GetValueOrDefault("lookupValue")?.ToString()?.Trim() ?? string.Empty;
        var returnColumn = config.GetValueOrDefault("returnColumn")?.ToString()?.Trim() ?? string.Empty;

        if (mode == "lookup" && !string.IsNullOrEmpty(lookupValue))
            lookupValue = context.Interpolate(lookupValue);

        if (!MailVariableHelper.TryGetVariableString(context.Variables, sourceVar, out var content, out var varError))
            return Task.FromResult(StepResult.Fail($"mail.extract-table: {varError}"));

        var tables = MailTableExtractor.ParseTables(content);
        if (tables.Count == 0)
        {
            return Task.FromResult(StepResult.Fail(
                $"mail.extract-table: no table found in variable '{sourceVar}'."));
        }

        if (tableIndex < 0 || tableIndex >= tables.Count)
        {
            return Task.FromResult(StepResult.Fail(
                $"mail.extract-table: table index {tableIndex} not found (found {tables.Count} table(s))."));
        }

        var table = tables[tableIndex];
        if (table.Columns.Count == 0)
        {
            return Task.FromResult(StepResult.Fail(
                $"mail.extract-table: table {tableIndex} has no columns."));
        }

        _logger.LogInformation(
            "mail.extract-table: parsed table {Index} — columns=[{Columns}], dataRows={Rows}, mode={Mode}",
            tableIndex,
            string.Join(", ", table.Columns),
            table.Rows.Count,
            mode);

        string extracted;
        var output = new Dictionary<string, object>
        {
            [$"{outputVar}_columns"] = MailTableExtractor.SerializeColumns(table),
        };

        switch (mode)
        {
            case "cell":
            {
                if (!TryGetMatrixRow(table, rowIndex, out var matrixRow, out var rowError))
                    return Task.FromResult(StepResult.Fail($"mail.extract-table: {rowError}"));

                if (columnIndex < 0 || columnIndex >= matrixRow.Count)
                {
                    return Task.FromResult(StepResult.Fail(
                        $"mail.extract-table: column index {columnIndex} out of range (table has {matrixRow.Count} column(s))."));
                }

                extracted = matrixRow[columnIndex];
                break;
            }

            case "headercell":
            {
                if (string.IsNullOrWhiteSpace(returnColumn))
                {
                    return Task.FromResult(StepResult.Fail(
                        "mail.extract-table: 'returnColumn' is required for headerCell mode."));
                }

                var colIdx = MailTableExtractor.FindColumnIndex(table.Columns, returnColumn, ignoreCase);
                if (colIdx < 0)
                {
                    return Task.FromResult(StepResult.Fail(
                        $"mail.extract-table: header '{returnColumn}' not found in table columns [{string.Join(", ", table.Columns)}]."));
                }

                if (!TryGetMatrixRow(table, rowIndex, out var matrixRow, out var rowError))
                    return Task.FromResult(StepResult.Fail($"mail.extract-table: {rowError}"));

                extracted = matrixRow[colIdx];
                break;
            }

            case "lookup":
            {
                if (string.IsNullOrWhiteSpace(lookupColumn))
                    return Task.FromResult(StepResult.Fail("mail.extract-table: 'lookupColumn' is required for lookup mode."));
                if (string.IsNullOrWhiteSpace(lookupValue))
                    return Task.FromResult(StepResult.Fail("mail.extract-table: 'lookupValue' is required for lookup mode."));
                if (string.IsNullOrWhiteSpace(returnColumn))
                    return Task.FromResult(StepResult.Fail("mail.extract-table: 'returnColumn' is required for lookup mode."));

                var lookupColIdx = MailTableExtractor.FindColumnIndex(table.Columns, lookupColumn, ignoreCase);
                if (lookupColIdx < 0)
                {
                    return Task.FromResult(StepResult.Fail(
                        $"mail.extract-table: lookup column '{lookupColumn}' not found in table columns [{string.Join(", ", table.Columns)}]."));
                }

                var returnColIdx = MailTableExtractor.FindColumnIndex(table.Columns, returnColumn, ignoreCase);
                if (returnColIdx < 0)
                {
                    return Task.FromResult(StepResult.Fail(
                        $"mail.extract-table: return column '{returnColumn}' not found in table columns [{string.Join(", ", table.Columns)}]."));
                }

                var normalizedLookup = MailTableExtractor.NormalizeCell(lookupValue);
                var comparison       = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                List<string>? matchedRow = null;
                foreach (var row in table.Rows)
                {
                    if (lookupColIdx >= row.Count)
                        continue;

                    if (string.Equals(
                            MailTableExtractor.NormalizeCell(row[lookupColIdx]),
                            normalizedLookup,
                            comparison))
                    {
                        matchedRow = row;
                        break;
                    }
                }

                if (matchedRow is null)
                {
                    return Task.FromResult(StepResult.Fail(
                        $"mail.extract-table: lookup value '{lookupValue}' not found in column '{lookupColumn}'."));
                }

                extracted = returnColIdx < matchedRow.Count ? matchedRow[returnColIdx] : string.Empty;
                break;
            }

            case "tablejson":
            {
                extracted = MailTableExtractor.SerializeTableJson(table);
                output[$"{outputVar}_count"] = table.Rows.Count;
                break;
            }

            default:
                return Task.FromResult(StepResult.Fail($"mail.extract-table: unsupported mode '{mode}'."));
        }

        output[outputVar] = extracted;

        _logger.LogInformation(
            "mail.extract-table: extracted via {Mode} from '{SourceVar}' table {TableIndex} → '{OutputVar}'",
            mode, sourceVar, tableIndex, outputVar);

        return Task.FromResult(StepResult.Ok(
            output: output,
            outputData: $"Extracted table value from '{sourceVar}' ({mode})"));
    }

    /// <summary>
    /// Maps a 0-based row index over header + data rows (row 0 = header).
    /// </summary>
    private static bool TryGetMatrixRow(
        ParsedMailTable table,
        int rowIndex,
        out List<string> row,
        out string error)
    {
        row   = [];
        error = string.Empty;

        if (rowIndex < 0)
        {
            error = $"row index {rowIndex} out of range.";
            return false;
        }

        if (rowIndex == 0)
        {
            row = table.Columns.ToList();
            return true;
        }

        var dataIndex = rowIndex - 1;
        if (dataIndex >= table.Rows.Count)
        {
            error = $"row index {rowIndex} out of range (table has header + {table.Rows.Count} data row(s)).";
            return false;
        }

        row = table.Rows[dataIndex];
        return true;
    }

    private static int ParseInt(object? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
    }

    private static bool ParseBool(object? value, bool defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is bool b) return b;
        if (value is string s && bool.TryParse(s, out var parsed)) return parsed;
        return defaultValue;
    }
}
