namespace Company.Orchestrator.Application.Capabilities.Browser;

public sealed class BrowserPageInfo
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public int Index { get; init; }
}

public sealed class BrowserDialogResult
{
    public required string DialogType { get; init; }
    public required string Message { get; init; }
    public bool Handled { get; init; }
}
