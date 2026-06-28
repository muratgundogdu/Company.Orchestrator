namespace Company.Orchestrator.Application.Capabilities.Browser;

/// <summary>Options for extracting tabular data from the active browser page.</summary>
public sealed class BrowserTableExtractOptions
{
    public string Mode { get; init; } = "htmlTable";
    public string Selector { get; init; } = "table";
    public int TableIndex { get; init; }
    public string? RowSelector { get; init; }
    public string? CellSelector { get; init; }
    public bool IncludeHeaders { get; init; } = true;
    public bool SkipEmptyRows { get; init; } = true;
    public bool NormalizeWhitespace { get; init; } = true;
    public int TimeoutMs { get; init; } = 30_000;
}
