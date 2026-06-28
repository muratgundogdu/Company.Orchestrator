using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Models;
using Xceed.Words.NET;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class WordStepHandlerHelpers
{
    private static readonly Regex PlaceholderRegex = new(
        @"\{\{([^}]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const string DocxMime =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static IReadOnlyList<string> ExtractPlaceholders(string text) =>
        PlaceholderRegex.Matches(text)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public static (bool Found, string Value) ResolvePlaceholder(WorkflowContext context, string path)
    {
        path = path.Trim();
        if (string.IsNullOrEmpty(path))
            return (true, string.Empty);

        if (context.Variables.TryGetValue(path, out var direct))
            return (true, VariableToString(direct));

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return (true, string.Empty);

        if (!context.Variables.TryGetValue(parts[0], out var current))
            return (false, string.Empty);

        if (parts.Length == 1)
            return (true, VariableToString(current));

        for (var i = 1; i < parts.Length; i++)
        {
            current = ResolveProperty(current, parts[i]);
            if (current is null)
                return (false, string.Empty);
        }

        return (true, VariableToString(current));
    }

    public static IReadOnlyList<string> CollectDocumentPlaceholders(DocX document)
    {
        var placeholders = new HashSet<string>(StringComparer.Ordinal);
        AddPlaceholdersFromText(document.Text, placeholders);

        foreach (var paragraph in document.Paragraphs)
            AddPlaceholdersFromText(paragraph.Text, placeholders);

        foreach (var table in document.Tables)
        {
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    foreach (var paragraph in cell.Paragraphs)
                        AddPlaceholdersFromText(paragraph.Text, placeholders);
                }
            }
        }

        foreach (var headerPart in new[] { document.Headers?.Odd, document.Headers?.Even, document.Headers?.First })
        {
            if (headerPart is null)
                continue;
            foreach (var paragraph in headerPart.Paragraphs)
                AddPlaceholdersFromText(paragraph.Text, placeholders);
        }

        foreach (var footerPart in new[] { document.Footers?.Odd, document.Footers?.Even, document.Footers?.First })
        {
            if (footerPart is null)
                continue;
            foreach (var paragraph in footerPart.Paragraphs)
                AddPlaceholdersFromText(paragraph.Text, placeholders);
        }

        return placeholders.OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    private static void AddPlaceholdersFromText(string? text, ISet<string> placeholders)
    {
        if (string.IsNullOrEmpty(text))
            return;

        foreach (var placeholder in ExtractPlaceholders(text))
            placeholders.Add(placeholder);
    }

    public static void ReplacePlaceholder(DocX document, string placeholder, string value)
    {
        var token = $"{{{{{placeholder}}}}}";
#pragma warning disable CS0618
        document.ReplaceText(token, value ?? string.Empty, trackChanges: false);
#pragma warning restore CS0618
    }

    private static object? ResolveProperty(object? value, string propertyName)
    {
        if (value is null)
            return null;

        if (value is JsonElement je)
            return ResolveJsonProperty(je, propertyName);

        if (value is Dictionary<string, object> dict)
        {
            foreach (var (key, val) in dict)
            {
                if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
                    return val;
            }

            return null;
        }

        if (value is string text)
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    return ResolveJsonProperty(doc.RootElement, propertyName);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static object? ResolveJsonProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty(propertyName, out var direct))
            return direct;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }

        return null;
    }

    private static string VariableToString(object? val)
    {
        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? string.Empty,
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
