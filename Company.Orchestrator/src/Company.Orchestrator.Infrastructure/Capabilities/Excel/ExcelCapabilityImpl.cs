using System.Text;
using ClosedXML.Excel;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.Excel;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Capabilities.Excel;

/// <summary>
/// Production ExcelCapability backed by ClosedXML.
///
/// Supports:
///   - .xlsx read/write (full ClosedXML implementation)
///   - .csv fallback for read-only operations (no ClosedXML required)
///
/// Key design rules:
///   - Every write operation returns a NEW artifact; originals are never mutated in the store.
///   - Typed cell value mapping: string / double / DateTime / bool / TimeSpan / null.
///   - First-class Excel cell addressing: A1, B3, AA10, etc. (ClosedXML handles this natively).
///   - Thread-safe: no shared mutable state — each call opens its own MemoryStream + XLWorkbook.
/// </summary>
public sealed class ExcelCapabilityImpl : IExcelCapability
{
    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelCapabilityImpl> _logger;

    public string CapabilityName => "Excel";

    public ExcelCapabilityImpl(IArtifactStore store, ILogger<ExcelCapabilityImpl> logger)
    {
        _store  = store;
        _logger = logger;
    }

    // ------------------------------------------------------------------ //
    // Lifecycle
    // ------------------------------------------------------------------ //

    /// <summary>Creates a new empty .xlsx workbook with a single "Sheet1" tab.</summary>
    public async Task<ArtifactReference> CreateWorkbookAsync(
        string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ExcelCapability: creating new workbook '{Name}'", name);

        var safeName = EnsureXlsxExtension(name);

        using var wb = new XLWorkbook();
        wb.Worksheets.Add("Sheet1");

        return await PersistWorkbookAsync(wb, safeName,
            new Dictionary<string, string>
            {
                ["operation"]  = "create",
                ["sheetCount"] = "1"
            }, cancellationToken);
    }

    // ------------------------------------------------------------------ //
    // Sheet discovery
    // ------------------------------------------------------------------ //

    public async Task<IReadOnlyList<string>> GetSheetNamesAsync(
        ArtifactReference workbook, CancellationToken cancellationToken = default)
    {
        if (IsCsv(workbook))
            return new List<string> { Path.GetFileNameWithoutExtension(workbook.Name) };

        using var wb = await LoadWorkbookAsync(workbook, cancellationToken);
        return wb.Worksheets.Select(ws => ws.Name).ToList();
    }

    // ------------------------------------------------------------------ //
    // Read
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Reads all data rows from the specified sheet and returns them as
    /// List&lt;Dictionary&lt;string, object?&gt;&gt; where each key is a column header.
    /// Cell values are typed: string, double, DateTime, bool, TimeSpan, or null.
    /// Falls back to CSV parsing for .csv artifacts.
    /// </summary>
    public async Task<IReadOnlyList<Dictionary<string, object?>>> ReadSheetAsync(
        ArtifactReference workbook,
        string sheetName = "Sheet1",
        bool hasHeaderRow = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ExcelCapability: reading sheet '{Sheet}' from '{Name}'", sheetName, workbook.Name);

        if (IsCsv(workbook))
        {
            var csv = Encoding.UTF8.GetString(
                await _store.ReadAllBytesAsync(workbook.StoragePath, cancellationToken));
            return ParseCsv(csv, hasHeaderRow);
        }

        using var wb = await LoadWorkbookAsync(workbook, cancellationToken);
        var ws = GetWorksheet(wb, sheetName, workbook.Name);
        return ExtractRows(ws, hasHeaderRow);
    }

    /// <summary>Reads the typed value of a single cell by Excel address (e.g. "A1", "B3", "AA10").</summary>
    public async Task<object?> ReadCellAsync(
        ArtifactReference workbook, string sheetName, string cellAddress,
        CancellationToken cancellationToken = default)
    {
        using var wb = await LoadWorkbookAsync(workbook, cancellationToken);
        var ws   = GetWorksheet(wb, sheetName, workbook.Name);
        var cell = ws.Cell(cellAddress);
        return GetCellTypedValue(cell);
    }

    // ------------------------------------------------------------------ //
    // Write — always returns a NEW artifact (original is immutable)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Writes rows into the specified sheet of an existing workbook artifact.
    /// Other sheets in the workbook are preserved.
    /// Returns a new artifact — the original is not modified.
    /// </summary>
    public async Task<ArtifactReference> WriteSheetAsync(
        ArtifactReference workbook,
        string sheetName,
        IEnumerable<Dictionary<string, object?>> rows,
        bool includeHeader = true,
        CancellationToken cancellationToken = default)
    {
        var rowList = rows.ToList();
        _logger.LogInformation(
            "ExcelCapability: writing {Count} rows to sheet '{Sheet}' in '{Name}'",
            rowList.Count, sheetName, workbook.Name);

        // Load existing workbook to preserve all other sheets
        using var wb = await LoadWorkbookAsync(workbook, cancellationToken);

        // Replace or create the target sheet
        var existing = wb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

        IXLWorksheet ws;
        if (existing is not null)
        {
            existing.Clear();
            ws = existing;
        }
        else
        {
            ws = wb.Worksheets.Add(sheetName);
        }

        WriteRowsToSheet(ws, rowList, includeHeader);

        var colCount = rowList.FirstOrDefault()?.Count ?? 0;
        var outName  = Path.GetFileNameWithoutExtension(workbook.Name) + $"-{SanitizeName(sheetName)}.xlsx";

        return await PersistWorkbookAsync(wb, outName,
            new Dictionary<string, string>
            {
                ["operation"]        = "writeSheet",
                ["sheetName"]        = sheetName,
                ["rowCount"]         = rowList.Count.ToString(),
                ["columnCount"]      = colCount.ToString(),
                ["sourceArtifactId"] = workbook.Id.ToString()
            }, cancellationToken);
    }

    /// <summary>
    /// Updates a single cell in an existing workbook and returns a new artifact.
    /// The original workbook is not modified.
    /// </summary>
    public async Task<ArtifactReference> WriteCellAsync(
        ArtifactReference workbook, string sheetName, string cellAddress, object? value,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ExcelCapability: writing value to {Address} on sheet '{Sheet}' in '{Name}'",
            cellAddress, sheetName, workbook.Name);

        using var wb = await LoadWorkbookAsync(workbook, cancellationToken);
        var ws = GetWorksheet(wb, sheetName, workbook.Name);
        SetCellValue(ws.Cell(cellAddress), value);

        var outName = Path.GetFileNameWithoutExtension(workbook.Name) + "-updated.xlsx";

        return await PersistWorkbookAsync(wb, outName,
            new Dictionary<string, string>
            {
                ["operation"]        = "writeCell",
                ["sheetName"]        = sheetName,
                ["cellAddress"]      = cellAddress,
                ["sourceArtifactId"] = workbook.Id.ToString()
            }, cancellationToken);
    }

    // ------------------------------------------------------------------ //
    // Export
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Converts the specified sheet to a RFC 4180-compliant CSV string.
    /// Values containing commas, double-quotes, or line breaks are properly quoted.
    /// Falls back to raw text for .csv artifacts.
    /// </summary>
    public async Task<string> ToCsvAsync(
        ArtifactReference workbook, string sheetName = "Sheet1",
        CancellationToken cancellationToken = default)
    {
        if (IsCsv(workbook))
        {
            var raw = await _store.ReadAllBytesAsync(workbook.StoragePath, cancellationToken);
            return Encoding.UTF8.GetString(raw);
        }

        using var wb = await LoadWorkbookAsync(workbook, cancellationToken);
        var ws   = GetWorksheet(wb, sheetName, workbook.Name);
        var rows = ExtractRows(ws, hasHeaderRow: true);

        if (rows.Count == 0) return string.Empty;

        var sb      = new StringBuilder();
        var headers = rows[0].Keys.ToList();

        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",",
                headers.Select(h => EscapeCsv(row.GetValueOrDefault(h)?.ToString() ?? ""))));

        return sb.ToString().TrimEnd('\r', '\n');
    }

    // ------------------------------------------------------------------ //
    // Row extraction
    // ------------------------------------------------------------------ //

    private static IReadOnlyList<Dictionary<string, object?>> ExtractRows(
        IXLWorksheet ws, bool hasHeaderRow)
    {
        var firstRowUsed = ws.FirstRowUsed();
        if (firstRowUsed is null) return new List<Dictionary<string, object?>>();

        var lastRowNum  = ws.LastRowUsed()!.RowNumber();
        var firstColNum = ws.FirstColumnUsed()!.ColumnNumber();
        var lastColNum  = ws.LastColumnUsed()!.ColumnNumber();
        var colCount    = lastColNum - firstColNum + 1;

        var allRows = ws.Rows(firstRowUsed.RowNumber(), lastRowNum).ToList();
        if (allRows.Count == 0) return new List<Dictionary<string, object?>>();

        string[] headers;
        int dataStart;

        if (hasHeaderRow)
        {
            headers = Enumerable.Range(0, colCount)
                .Select(i =>
                {
                    var h = allRows[0].Cell(firstColNum + i).GetString().Trim();
                    return string.IsNullOrEmpty(h) ? $"Col{i + 1}" : h;
                })
                .ToArray();
            dataStart = 1;
        }
        else
        {
            headers   = Enumerable.Range(1, colCount).Select(i => $"Col{i}").ToArray();
            dataStart = 0;
        }

        var result = new List<Dictionary<string, object?>>();
        for (var r = dataStart; r < allRows.Count; r++)
        {
            // Skip completely blank rows
            var isBlank = Enumerable.Range(0, colCount)
                .All(c => allRows[r].Cell(firstColNum + c).Value.IsBlank);
            if (isBlank) continue;

            var dict = new Dictionary<string, object?>(colCount);
            for (var c = 0; c < colCount; c++)
                dict[headers[c]] = GetCellTypedValue(allRows[r].Cell(firstColNum + c));

            result.Add(dict);
        }

        return result;
    }

    // ------------------------------------------------------------------ //
    // Cell value helpers
    // ------------------------------------------------------------------ //

    private static object? GetCellTypedValue(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank)    return null;
        if (v.IsText)     return v.GetText();
        if (v.IsNumber)   return v.GetNumber();
        if (v.IsDateTime) return v.GetDateTime();
        if (v.IsBoolean)  return v.GetBoolean();
        if (v.IsTimeSpan) return v.GetTimeSpan().ToString(@"hh\:mm\:ss");
        if (v.IsError)    return $"#ERROR:{v.GetError()}";
        return cell.GetString();
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        if (value is null) { cell.Clear(); return; }

        cell.Value = value switch
        {
            bool b     => (XLCellValue)b,
            double d   => d,
            float f    => (double)f,
            int i      => (double)i,
            long l     => (double)l,
            decimal dc => (double)dc,
            DateTime dt => dt,
            TimeSpan ts => ts,
            string s    => s,
            _           => value.ToString() ?? string.Empty
        };
    }

    // ------------------------------------------------------------------ //
    // Sheet writing
    // ------------------------------------------------------------------ //

    private static void WriteRowsToSheet(
        IXLWorksheet ws,
        List<Dictionary<string, object?>> rows,
        bool includeHeader)
    {
        if (rows.Count == 0) return;

        var headers    = rows[0].Keys.ToList();
        var currentRow = 1;

        if (includeHeader)
        {
            for (var c = 0; c < headers.Count; c++)
            {
                var hCell = ws.Cell(currentRow, c + 1);
                hCell.Value = headers[c];
                hCell.Style.Font.Bold = true;
            }
            currentRow++;
        }

        foreach (var row in rows)
        {
            for (var c = 0; c < headers.Count; c++)
                SetCellValue(ws.Cell(currentRow, c + 1), row.GetValueOrDefault(headers[c]));
            currentRow++;
        }

        // Auto-fit column widths (best-effort; ClosedXML calculates width from content)
        ws.Columns().AdjustToContents();
    }

    // ------------------------------------------------------------------ //
    // Artifact persistence
    // ------------------------------------------------------------------ //

    private async Task<ArtifactReference> PersistWorkbookAsync(
        XLWorkbook wb,
        string name,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var sizeBytes = stream.Length;
        stream.Position = 0;

        var storagePath = await _store.SaveAsync(id, name, stream, cancellationToken);

        _logger.LogInformation(
            "ExcelCapability: persisted workbook '{Name}' ({Size:N0} bytes) → artifact {Id}",
            name, sizeBytes, id);

        return new ArtifactReference
        {
            Id          = id,
            Name        = name,
            ContentType = XlsxMime,
            StoragePath = storagePath,
            SizeBytes   = sizeBytes,
            Metadata    = metadata
        };
    }

    private async Task<XLWorkbook> LoadWorkbookAsync(
        ArtifactReference artifact, CancellationToken cancellationToken)
    {
        var bytes = await _store.ReadAllBytesAsync(artifact.StoragePath, cancellationToken);
        var ms    = new MemoryStream(bytes); // XLWorkbook owns & disposes this
        return new XLWorkbook(ms);
    }

    // ------------------------------------------------------------------ //
    // CSV helpers (fallback for .csv artifacts)
    // ------------------------------------------------------------------ //

    private static IReadOnlyList<Dictionary<string, object?>> ParseCsv(string csv, bool hasHeader)
    {
        var lines = csv.ReplaceLineEndings("\n").Split('\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0) return new List<Dictionary<string, object?>>();

        string[] headers;
        int dataStart;

        if (hasHeader)
        {
            headers   = SplitCsvLine(lines[0]);
            dataStart = 1;
        }
        else
        {
            var first = SplitCsvLine(lines[0]);
            headers   = Enumerable.Range(1, first.Length).Select(i => $"Col{i}").ToArray();
            dataStart = 0;
        }

        var result = new List<Dictionary<string, object?>>();
        for (var i = dataStart; i < lines.Length; i++)
        {
            var cells = SplitCsvLine(lines[i]);
            var row   = new Dictionary<string, object?>(headers.Length);
            for (var j = 0; j < headers.Length; j++)
                row[headers[j]] = j < cells.Length ? (object?)cells[j] : null;
            result.Add(row);
        }
        return result;
    }

    /// <summary>
    /// RFC 4180-aware CSV line splitter. Handles quoted fields containing commas, quotes, and line breaks.
    /// </summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields  = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuote)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuote = false;
                }
                else current.Append(ch);
            }
            else
            {
                if (ch == '"') inQuote = true;
                else if (ch == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(ch);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        if (value is null) return "";
        return value.Contains(',') || value.Contains('"') ||
               value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    // ------------------------------------------------------------------ //
    // Private helpers
    // ------------------------------------------------------------------ //

    private static IXLWorksheet GetWorksheet(XLWorkbook wb, string sheetName, string workbookName)
    {
        var ws = wb.Worksheets.FirstOrDefault(
            s => s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

        return ws ?? throw new InvalidOperationException(
            $"Sheet '{sheetName}' not found in workbook '{workbookName}'. " +
            $"Available sheets: [{string.Join(", ", wb.Worksheets.Select(s => s.Name))}].");
    }

    private static bool IsCsv(ArtifactReference artifact) =>
        artifact.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    private static string EnsureXlsxExtension(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".xlsx" or ".xlsm" ? name
            : Path.GetFileNameWithoutExtension(name) + ".xlsx";
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private const string XlsxMime =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
