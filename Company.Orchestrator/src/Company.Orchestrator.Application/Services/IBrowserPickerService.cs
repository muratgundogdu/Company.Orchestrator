namespace Company.Orchestrator.Application.Services;

public interface IBrowserPickerService
{
    Task<BrowserPickerStartResult> StartAsync(string url, CancellationToken cancellationToken = default);
    Task StopAsync(Guid sessionId, CancellationToken cancellationToken = default);
    BrowserPickerSelectedResult? GetSelected(Guid sessionId);
}

public sealed record BrowserPickerStartResult(Guid SessionId, string Status);

public sealed class BrowserPickerSelectedResult
{
    public string PrimarySelector { get; init; } = string.Empty;

    /// <summary>Alias for <see cref="PrimarySelector"/> — backward compatible.</summary>
    public string Selector => PrimarySelector;

    public IReadOnlyList<BrowserPickerCandidate> Candidates { get; init; } = [];

    /// <summary>Resolved clickable target used for candidate generation (backward compatible).</summary>
    public BrowserPickerSelectedElement SelectedElement { get; init; } = new();

    public BrowserPickerSelectedElement OriginalClickedElement { get; init; } = new();

    public BrowserPickerSelectedElement ResolvedClickableElement { get; init; } = new();

    // Legacy flat fields (mirrors SelectedElement)
    public string Text => SelectedElement.Text;
    public string TagName => SelectedElement.TagName;
    public string Id => SelectedElement.Id;
    public string Name => SelectedElement.Name;
    public string AriaLabel => SelectedElement.AriaLabel;
    public string Href => SelectedElement.Href;
}

public sealed class BrowserPickerCandidate
{
    public string Selector { get; init; } = string.Empty;

    /// <summary>
    /// Selector engine / healing category: css, xpath, dom-path, table-relative, attribute, text.
    /// </summary>
    public string Type { get; init; } = "css";

    public string Strategy { get; init; } = string.Empty;
    public string Confidence { get; init; } = "low";
    public int MatchCount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class BrowserPickerSelectedElement
{
    public string TagName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AriaLabel { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
}
