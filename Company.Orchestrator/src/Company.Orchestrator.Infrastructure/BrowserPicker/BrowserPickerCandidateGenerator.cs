using System.Text.Json;
using Company.Orchestrator.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.BrowserPicker;

internal static class BrowserPickerCandidateGenerator
{
    private static readonly Dictionary<string, int> ConfidenceRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["high"]   = 0,
        ["medium"] = 1,
        ["low"]    = 2,
    };

    public static async Task<BrowserPickerSelectedResult> BuildAsync(IPage page, ILogger logger)
    {
        try
        {
            var raw = await page.EvaluateAsync<JsonElement>(BrowserPickerCandidateScript.Evaluate);
            if (raw.ValueKind == JsonValueKind.Null || raw.ValueKind == JsonValueKind.Undefined)
            {
                return EmptyResult();
            }

            BrowserPickerSelectedElement resolvedElement;
            if (raw.TryGetProperty("resolvedClickableElement", out var resolvedProp))
                resolvedElement = ParseSelectedElement(resolvedProp);
            else
                resolvedElement = ParseSelectedElement(raw.GetProperty("selectedElement"));

            BrowserPickerSelectedElement originalElement;
            if (raw.TryGetProperty("originalClickedElement", out var originalProp))
                originalElement = ParseSelectedElement(originalProp);
            else
                originalElement = resolvedElement;

            var validated = new List<BrowserPickerCandidate>();

            foreach (var item in raw.GetProperty("rawCandidates").EnumerateArray())
            {
                var selector   = item.GetProperty("selector").GetString() ?? string.Empty;
                var strategy   = item.GetProperty("strategy").GetString() ?? string.Empty;
                var confidence = item.GetProperty("confidence").GetString() ?? "low";
                var reason     = item.GetProperty("reason").GetString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(selector)) continue;

                var matchCount = await CountMatchesAsync(page, selector);
                if (matchCount < 1) continue;

                validated.Add(new BrowserPickerCandidate
                {
                    Selector   = selector,
                    Strategy   = strategy,
                    Confidence = confidence,
                    MatchCount = matchCount,
                    Reason     = reason,
                });
            }

            validated = validated
                .GroupBy(c => c.Selector, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            var primary = PickPrimary(validated);

            if (primary is not null)
            {
                logger.LogInformation(
                    "BrowserPicker: primary={Selector} strategy={Strategy} matches={Count}",
                    primary.Selector, primary.Strategy, primary.MatchCount);
            }

            return new BrowserPickerSelectedResult
            {
                PrimarySelector           = primary?.Selector ?? string.Empty,
                Candidates                = validated,
                SelectedElement             = resolvedElement,
                OriginalClickedElement      = originalElement,
                ResolvedClickableElement    = resolvedElement,
            };
        }
        finally
        {
            try
            {
                await page.EvaluateAsync("""
                    () => document.querySelectorAll('[data-alterone-pick-target],[data-alterone-pick-original]').forEach(
                      el => {
                        el.removeAttribute('data-alterone-pick-target');
                        el.removeAttribute('data-alterone-pick-original');
                      })
                    """);
            }
            catch
            {
                // page may be closing
            }
        }
    }

    private static async Task<int> CountMatchesAsync(IPage page, string selector)
    {
        try
        {
            return await page.Locator(selector).CountAsync();
        }
        catch
        {
            return 0;
        }
    }

    private static BrowserPickerCandidate? PickPrimary(IReadOnlyList<BrowserPickerCandidate> candidates)
    {
        if (candidates.Count == 0) return null;

        return candidates
            .OrderBy(c => ConfidenceRank.GetValueOrDefault(c.Confidence, 99))
            .ThenBy(c => c.MatchCount == 1 ? 0 : 1)
            .ThenBy(c => c.Selector.Length)
            .First();
    }

    private static BrowserPickerSelectedElement ParseSelectedElement(JsonElement el) =>
        new()
        {
            TagName   = GetStr(el, "tagName"),
            Text      = GetStr(el, "text"),
            Id        = GetStr(el, "id"),
            Name      = GetStr(el, "name"),
            AriaLabel = GetStr(el, "ariaLabel"),
            Href      = GetStr(el, "href"),
        };

    private static string GetStr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    private static BrowserPickerSelectedResult EmptyResult() =>
        new() { SelectedElement = new BrowserPickerSelectedElement() };
}
