using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Models;
using UglyToad.PdfPig;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class PdfStepHandlerHelpers
{
    public static IReadOnlyList<int> ParsePageRange(string pageRange, int totalPages, string stepType)
    {
        if (totalPages <= 0)
            throw new InvalidOperationException($"{stepType}: PDF has no pages.");

        var trimmed = pageRange.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return Enumerable.Range(1, totalPages).ToList();

        var pages = new SortedSet<int>();

        foreach (var segment in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            var dashIndex = segment.IndexOf('-', StringComparison.Ordinal);
            if (dashIndex >= 0)
            {
                var startPart = segment[..dashIndex].Trim();
                var endPart   = segment[(dashIndex + 1)..].Trim();

                if (!int.TryParse(startPart, out var start) || !int.TryParse(endPart, out var end))
                {
                    throw new InvalidOperationException(
                        $"{stepType}: invalid page range segment '{segment}'.");
                }

                if (start <= 0 || end <= 0 || start > end)
                {
                    throw new InvalidOperationException(
                        $"{stepType}: invalid page range '{segment}'.");
                }

                for (var page = start; page <= end; page++)
                    pages.Add(page);
            }
            else
            {
                if (!int.TryParse(segment, out var page) || page <= 0)
                {
                    throw new InvalidOperationException(
                        $"{stepType}: invalid page number '{segment}'.");
                }

                pages.Add(page);
            }
        }

        if (pages.Count == 0)
            throw new InvalidOperationException($"{stepType}: pageRange did not specify any pages.");

        foreach (var page in pages)
        {
            if (page > totalPages)
            {
                throw new InvalidOperationException(
                    $"{stepType}: page {page} is out of range (PDF has {totalPages} page(s)).");
            }
        }

        return pages.ToList();
    }

    public static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text.Replace('\t', ' ');
        normalized = Regex.Replace(normalized, @"[ \f\v]+", " ");
        normalized = Regex.Replace(normalized, @"\r\n?|\n", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    public static string TakePrefix(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];

    public static (string Text, int PageCount) ExtractTextFromPdf(
        byte[] bytes,
        string pageRange,
        bool normalizeWhitespace,
        string stepType)
    {
        using var stream = new MemoryStream(bytes);
        using var document = PdfDocument.Open(stream);

        var totalPages = document.NumberOfPages;
        var pages      = ParsePageRange(pageRange, totalPages, stepType);

        var builder = new StringBuilder();
        foreach (var pageNumber in pages)
        {
            var pageText = document.GetPage(pageNumber).Text;
            if (!string.IsNullOrEmpty(pageText))
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(pageText);
            }
        }

        var text = builder.ToString();
        if (normalizeWhitespace)
            text = NormalizeWhitespace(text);

        return (text, pages.Count);
    }

    public static StepResult BuildDataTableOutput(
        string outputVar,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var json        = JsonSerializer.Serialize(rows);
        var columnsJson = JsonSerializer.Serialize(columns);
        var firstJson   = rows.Count > 0 ? JsonSerializer.Serialize(rows[0]) : "{}";

        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]              = json,
            [$"{outputVar}_count"]   = rows.Count,
            [$"{outputVar}_columns"] = columnsJson,
            [$"{outputVar}_first"]   = firstJson,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonSerializer.Serialize(rows[i]);

        return StepResult.Ok(
            output: output,
            outputData:
                $"Extracted {rows.Count} row(s) and {columns.Count} column(s) into '{outputVar}'.");
    }
}
