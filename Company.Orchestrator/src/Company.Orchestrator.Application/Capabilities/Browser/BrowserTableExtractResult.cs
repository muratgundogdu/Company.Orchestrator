namespace Company.Orchestrator.Application.Capabilities.Browser;

/// <summary>Structured table data extracted from a browser page.</summary>
public sealed class BrowserTableExtractResult
{
    public required IReadOnlyList<string> Columns { get; init; }
    public required IReadOnlyList<Dictionary<string, string>> Rows { get; init; }
}
