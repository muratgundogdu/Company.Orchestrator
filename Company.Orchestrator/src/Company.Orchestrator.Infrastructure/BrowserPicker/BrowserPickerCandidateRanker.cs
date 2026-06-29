using Company.Orchestrator.Application.Services;

namespace Company.Orchestrator.Infrastructure.BrowserPicker;

/// <summary>
/// Selector Healing V1 — ranks validated candidates for primary recommendation.
/// </summary>
internal static class BrowserPickerCandidateRanker
{
    private static readonly Dictionary<string, int> ConfidenceRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["high"]   = 0,
        ["medium"] = 1,
        ["low"]    = 2,
    };

    /// <summary>Lower is better. Table-relative healing strategies outrank fragile DOM paths.</summary>
    private static readonly Dictionary<string, int> StrategyRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["table-row-xpath"]      = 0,
        ["table-row-css"]        = 1,
        ["table-row-text-xpath"] = 2,
        ["data"]                 = 3,
        ["id"]                   = 4,
        ["name"]                 = 5,
        ["text-xpath"]           = 6,
        ["aria"]                 = 7,
        ["role"]                 = 8,
        ["href"]                 = 9,
        ["text"]                 = 10,
        ["parent-text"]          = 11,
        ["class"]                = 12,
        ["path"]                 = 100,
    };

    public static BrowserPickerCandidate? PickPrimary(IReadOnlyList<BrowserPickerCandidate> candidates)
    {
        if (candidates.Count == 0) return null;

        return candidates
            .OrderBy(c => ConfidenceRank.GetValueOrDefault(c.Confidence, 99))
            .ThenBy(c => StrategyRank.GetValueOrDefault(c.Strategy, 50))
            .ThenBy(c => c.MatchCount == 1 ? 0 : 1)
            .ThenBy(c => c.Selector.Length)
            .First();
    }
}
