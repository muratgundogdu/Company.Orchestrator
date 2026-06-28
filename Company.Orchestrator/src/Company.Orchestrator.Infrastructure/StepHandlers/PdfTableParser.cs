using System.Text.RegularExpressions;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal sealed class PdfTableParser
{
    private readonly string _stepType;
    private readonly string _parserMode;
    private readonly string _delimiter;
    private readonly bool _hasHeader;

    public PdfTableParser(string stepType, string parserMode, string delimiter, bool hasHeader)
    {
        _stepType    = stepType;
        _parserMode  = parserMode.Trim().ToLowerInvariant();
        _delimiter   = delimiter;
        _hasHeader   = hasHeader;
    }

    public (IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, string>> Rows) Parse(
        string text,
        int tableIndex)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"{_stepType}: no text found in PDF.");

        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
            throw new InvalidOperationException($"{_stepType}: no text found in PDF.");

        if (_parserMode == "fixedwidth")
        {
            throw new InvalidOperationException(
                $"{_stepType}: parserMode 'fixedWidth' is not supported in this version.");
        }

        var strategy = ResolveStrategy(lines);
        var parsedRows = lines
            .Select(line => strategy.ParseLine(line))
            .Where(cells => cells.Length > 0)
            .ToList();

        if (parsedRows.Count == 0)
            throw new InvalidOperationException($"{_stepType}: no table rows could be parsed.");

        var blocks = DetectTableBlocks(parsedRows);
        if (blocks.Count == 0)
            throw new InvalidOperationException($"{_stepType}: no table found in PDF text.");

        if (tableIndex < 0 || tableIndex >= blocks.Count)
        {
            throw new InvalidOperationException(
                $"{_stepType}: tableIndex {tableIndex} is out of range ({blocks.Count} table block(s) detected).");
        }

        var block = blocks[tableIndex];
        return BuildDataTable(block);
    }

    private ILineParser ResolveStrategy(IReadOnlyList<string> lines)
    {
        return _parserMode switch
        {
            "delimiter" => CreateDelimiterParser(RequireDelimiter()),
            "multispace" => MultiSpaceParser.Instance,
            "auto" => ResolveAutoParser(lines),
            _ => throw new InvalidOperationException(
                $"{_stepType}: parserMode must be 'auto', 'delimiter', 'multiSpace', or 'fixedWidth'."),
        };
    }

    private ILineParser ResolveAutoParser(IReadOnlyList<string> lines)
    {
        if (!string.IsNullOrWhiteSpace(_delimiter))
            return CreateDelimiterParser(UnescapeDelimiter(_delimiter));

        var sample = lines.Take(Math.Min(lines.Count, 25)).ToList();

        foreach (var parser in new ILineParser[] { TabParser.Instance, PipeParser.Instance, MultiSpaceParser.Instance })
        {
            if (TryConsistentMultiColumnParse(sample, parser, out _))
                return parser;
        }

        return SingleColumnParser.Instance;
    }

    private string RequireDelimiter()
    {
        if (string.IsNullOrWhiteSpace(_delimiter))
        {
            throw new InvalidOperationException(
                $"{_stepType}: 'delimiter' is required when parserMode is 'delimiter'.");
        }

        return UnescapeDelimiter(_delimiter);
    }

    private static bool TryConsistentMultiColumnParse(
        IReadOnlyList<string> lines,
        ILineParser parser,
        out int columnCount)
    {
        columnCount = 0;
        if (lines.Count == 0)
            return false;

        var counts = lines.Select(line => parser.ParseLine(line).Length).Distinct().ToList();
        if (counts.Count != 1)
            return false;

        columnCount = counts[0];
        return columnCount >= 2;
    }

    private static DelimiterParser CreateDelimiterParser(string delimiter)
    {
        if (string.IsNullOrEmpty(delimiter))
            throw new InvalidOperationException("delimiter must not be empty.");

        return new DelimiterParser(delimiter[0]);
    }

    private (IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, string>> Rows) BuildDataTable(
        IReadOnlyList<string[]> block)
    {
        if (block.Count == 0)
            throw new InvalidOperationException($"{_stepType}: selected table block is empty.");

        var columnCount = block[0].Length;
        if (columnCount == 0)
            throw new InvalidOperationException($"{_stepType}: selected table has no columns.");

        string[] headers;
        IEnumerable<string[]> dataRows;

        if (_hasHeader)
        {
            if (block.Count < 1)
                throw new InvalidOperationException($"{_stepType}: header row is missing.");

            headers = CsvStepHandlerHelpers.BuildHeaderNames(block[0]);
            if (headers.Length != columnCount)
            {
                throw new InvalidOperationException(
                    $"{_stepType}: header row has inconsistent column count.");
            }

            dataRows = block.Skip(1);
        }
        else
        {
            headers  = Enumerable.Range(1, columnCount).Select(i => $"Column{i}").ToArray();
            dataRows = block;
        }

        var rows = new List<Dictionary<string, string>>();
        foreach (var cells in dataRows)
        {
            if (cells.Length != columnCount)
            {
                throw new InvalidOperationException(
                    $"{_stepType}: inconsistent column count in table row " +
                    $"(expected {columnCount}, got {cells.Length}).");
            }

            var row = new Dictionary<string, string>(columnCount, StringComparer.Ordinal);
            for (var i = 0; i < headers.Length; i++)
                row[headers[i]] = cells[i];
            rows.Add(row);
        }

        return (headers, rows);
    }

    private static List<List<string[]>> DetectTableBlocks(IReadOnlyList<string[]> parsedRows)
    {
        var blocks  = new List<List<string[]>>();
        var current = new List<string[]>();
        int? currentColumnCount = null;

        foreach (var row in parsedRows)
        {
            if (currentColumnCount is null || row.Length != currentColumnCount)
            {
                if (current.Count > 0)
                    blocks.Add(current);

                current           = new List<string[]>();
                currentColumnCount = row.Length;
            }

            current.Add(row);
        }

        if (current.Count > 0)
            blocks.Add(current);

        return blocks.Where(b => b.Count > 0).ToList();
    }

    private static string UnescapeDelimiter(string value) =>
        value switch
        {
            "\\t" => "\t",
            "tab" => "\t",
            _     => value,
        };

    private interface ILineParser
    {
        string[] ParseLine(string line);
    }

    private sealed class DelimiterParser(char delimiter) : ILineParser
    {
        public string[] ParseLine(string line) =>
            line.Split(delimiter, StringSplitOptions.TrimEntries);
    }

    private sealed class TabParser : ILineParser
    {
        public static TabParser Instance { get; } = new();
        public string[] ParseLine(string line) =>
            line.Split('\t', StringSplitOptions.TrimEntries);
    }

    private sealed class PipeParser : ILineParser
    {
        public static PipeParser Instance { get; } = new();
        public string[] ParseLine(string line) =>
            line.Split('|', StringSplitOptions.TrimEntries);
    }

    private sealed class MultiSpaceParser : ILineParser
    {
        public static MultiSpaceParser Instance { get; } = new();
        public string[] ParseLine(string line) =>
            Regex.Split(line.Trim(), @"\s{2,}", RegexOptions.CultureInvariant)
                .Where(cell => cell.Length > 0)
                .ToArray();
    }

    private sealed class SingleColumnParser : ILineParser
    {
        public static SingleColumnParser Instance { get; } = new();
        public string[] ParseLine(string line) => [line.Trim()];
    }
}
