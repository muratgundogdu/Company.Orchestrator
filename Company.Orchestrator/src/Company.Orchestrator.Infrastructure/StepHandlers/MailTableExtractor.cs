using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal sealed class ParsedMailTable
{
    public List<string> Columns { get; init; } = [];
    public List<List<string>> Rows { get; init; } = [];
}

internal static class MailTableExtractor
{
    internal static List<ParsedMailTable> ParseTables(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        return content.Contains("<table", StringComparison.OrdinalIgnoreCase)
            ? ParseHtmlTables(content)
            : ParsePlainTextTables(content);
    }

    internal static string NormalizeCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    internal static int FindColumnIndex(IReadOnlyList<string> columns, string name, bool ignoreCase)
    {
        var normalized = NormalizeCell(name);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(NormalizeCell(columns[i]), normalized, comparison))
                return i;
        }

        return -1;
    }

    internal static string SerializeTableJson(ParsedMailTable table)
    {
        var rows = new List<Dictionary<string, string>>();
        foreach (var row in table.Rows)
        {
            var obj = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var key = table.Columns[i];
                if (string.IsNullOrEmpty(key))
                    key = $"Column{i}";

                obj[key] = i < row.Count ? row[i] : string.Empty;
            }

            rows.Add(obj);
        }

        return JsonSerializer.Serialize(rows);
    }

    internal static string SerializeColumns(ParsedMailTable table)
        => JsonSerializer.Serialize(table.Columns);

    private static List<ParsedMailTable> ParseHtmlTables(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tableNodes = doc.DocumentNode.SelectNodes("//table");
        if (tableNodes is null || tableNodes.Count == 0)
            return [];

        var tables = new List<ParsedMailTable>();
        foreach (var tableNode in tableNodes)
        {
            var parsed = ParseHtmlTable(tableNode);
            if (parsed.Columns.Count > 0 || parsed.Rows.Count > 0)
                tables.Add(parsed);
        }

        return tables;
    }

    private static ParsedMailTable ParseHtmlTable(HtmlNode tableNode)
    {
        var allRows = tableNode.SelectNodes(".//tr");
        if (allRows is null || allRows.Count == 0)
            return new ParsedMailTable();

        var matrix     = new List<List<string>>();
        var headerRow  = -1;

        foreach (var rowNode in allRows)
        {
            var thCells = rowNode.SelectNodes("th");
            if (thCells is not null && thCells.Count > 0)
            {
                matrix.Add(thCells.Select(c => NormalizeCell(c.InnerText)).ToList());
                headerRow = matrix.Count - 1;
                continue;
            }

            var tdCells = rowNode.SelectNodes("td");
            if (tdCells is null || tdCells.Count == 0)
                continue;

            matrix.Add(tdCells.Select(c => NormalizeCell(c.InnerText)).ToList());
        }

        if (matrix.Count == 0)
            return new ParsedMailTable();

        if (headerRow < 0)
        {
            headerRow = 0;
            var columns = PadRow(matrix[0], matrix.Max(r => r.Count));
            var dataRows = matrix.Skip(1)
                .Select(r => PadRow(r, columns.Count))
                .Where(r => r.Any(c => !string.IsNullOrEmpty(c)))
                .ToList();

            return new ParsedMailTable { Columns = columns, Rows = dataRows };
        }

        var headerCells = PadRow(matrix[headerRow], matrix.Max(r => r.Count));
        var bodyRows = matrix
            .Where((_, idx) => idx != headerRow)
            .Select(r => PadRow(r, headerCells.Count))
            .Where(r => r.Any(c => !string.IsNullOrEmpty(c)))
            .ToList();

        return new ParsedMailTable { Columns = headerCells, Rows = bodyRows };
    }

    private static List<ParsedMailTable> ParsePlainTextTables(string content)
    {
        var lines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeCell)
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        if (lines.Count == 0)
            return [];

        var delimiter = DetectDelimiter(lines[0]);
        var header    = SplitLine(lines[0], delimiter);
        var rows      = new List<List<string>>();

        for (var i = 1; i < lines.Count; i++)
        {
            var cells = SplitLine(lines[i], delimiter);
            if (cells.All(string.IsNullOrEmpty))
                continue;

            rows.Add(PadRow(cells, header.Count));
        }

        return
        [
            new ParsedMailTable
            {
                Columns = header,
                Rows    = rows,
            }
        ];
    }

    private static char DetectDelimiter(string line)
    {
        if (line.Contains('|'))
            return '|';

        if (line.Contains('\t'))
            return '\t';

        return '\0';
    }

    private static List<string> SplitLine(string line, char delimiter)
    {
        if (delimiter == '|')
            return line.Split('|', StringSplitOptions.TrimEntries)
                       .Select(NormalizeCell)
                       .ToList();

        if (delimiter == '\t')
            return line.Split('\t', StringSplitOptions.TrimEntries)
                       .Select(NormalizeCell)
                       .ToList();

        return Regex.Split(line, @"\s{2,}")
                    .Select(NormalizeCell)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
    }

    private static List<string> PadRow(List<string> row, int width)
    {
        var padded = row.Select(NormalizeCell).ToList();
        while (padded.Count < width)
            padded.Add(string.Empty);
        return padded;
    }
}
