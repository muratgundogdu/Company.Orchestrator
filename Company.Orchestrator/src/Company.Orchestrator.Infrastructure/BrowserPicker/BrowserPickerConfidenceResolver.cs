namespace Company.Orchestrator.Infrastructure.BrowserPicker;

/// <summary>
/// Final confidence is derived from Playwright match count after validation.
/// A selector matching more than one element cannot be HIGH.
/// </summary>
internal static class BrowserPickerConfidenceResolver
{
    public static string FromMatchCount(int matchCount) => matchCount switch
    {
        1       => "high",
        2 or 3  => "medium",
        _       => "low",
    };
}
