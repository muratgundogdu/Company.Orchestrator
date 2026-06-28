using Company.Orchestrator.Application.Artifacts;

namespace Company.Orchestrator.Application.Capabilities.Browser;

/// <summary>
/// Capability for browser automation.
/// Implementations back this with Playwright, Selenium, or Puppeteer.
/// The engine and step handlers never import a browser library directly.
/// </summary>
public interface IBrowserCapability : ICapability
{
    Task NavigateAsync(string url, CancellationToken cancellationToken = default);
    Task<ArtifactReference> TakeScreenshotAsync(string? name = null, CancellationToken cancellationToken = default);
    Task<string> GetTextAsync(string cssSelector, CancellationToken cancellationToken = default);
    Task<string> GetAttributeAsync(string cssSelector, string attribute, CancellationToken cancellationToken = default);
    Task<string> GetPageSourceAsync(CancellationToken cancellationToken = default);
    Task ClickAsync(string cssSelector, CancellationToken cancellationToken = default);
    Task TypeAsync(string cssSelector, string text, CancellationToken cancellationToken = default);
    Task ClearInputAsync(string cssSelector, CancellationToken cancellationToken = default);
    Task PressKeyAsync(string key, string? cssSelector = null, CancellationToken cancellationToken = default);
    Task SelectOptionAsync(string cssSelector, string value, CancellationToken cancellationToken = default);
    Task SelectOptionByValueOrLabelAsync(string cssSelector, string? value, string? label, CancellationToken cancellationToken = default);
    Task SelectOptionByIndexAsync(string cssSelector, int index, CancellationToken cancellationToken = default);
    Task ScrollAsync(string? cssSelector, string direction, int amount, CancellationToken cancellationToken = default);
    Task HoverAsync(string cssSelector, CancellationToken cancellationToken = default);
    Task WaitForSelectorAsync(string cssSelector, int timeoutMs = 5000, CancellationToken cancellationToken = default);
    Task WaitForTextAsync(string text, int timeoutMs = 30000, CancellationToken cancellationToken = default);
    Task WaitForTextAsync(string text, string cssSelector, int timeoutMs, CancellationToken cancellationToken = default);
    Task WaitForUrlContainsAsync(string urlContains, int timeoutMs = 30000, CancellationToken cancellationToken = default);
    Task WaitForUrlAsync(string pattern, string matchMode, int timeoutMs, CancellationToken cancellationToken = default);
    Task WaitForNetworkIdleAsync(int timeoutMs, CancellationToken cancellationToken = default);
    Task<bool> ElementExistsAsync(string cssSelector, int timeoutMs, bool visibleOnly, CancellationToken cancellationToken = default);
    Task<string> GetCurrentUrlAsync(CancellationToken cancellationToken = default);
    Task WaitForNavigationAsync(int timeoutMs = 10000, CancellationToken cancellationToken = default);
    Task<string> EvaluateScriptAsync(string script, CancellationToken cancellationToken = default);
    Task<ArtifactReference> DownloadFileAsync(string downloadUrl, string name, CancellationToken cancellationToken = default);
    Task<ArtifactReference> DownloadByClickAsync(string cssSelector, string name, CancellationToken cancellationToken = default);
    Task<ArtifactReference> DownloadByClickAsync(string cssSelector, string name, int timeoutMs, CancellationToken cancellationToken = default);
    Task UploadFileAsync(string cssSelector, string filePath, int timeoutMs, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrowserPageInfo>> GetPagesAsync(CancellationToken cancellationToken = default);
    Task<BrowserPageInfo> SwitchPageAsync(string mode, string? urlContains, string? titleContains, int timeoutMs, CancellationToken cancellationToken = default);
    Task<BrowserPageInfo> ClosePageAsync(string mode, CancellationToken cancellationToken = default);
    Task<BrowserDialogResult> WaitForDialogAsync(string action, string? promptText, int timeoutMs, CancellationToken cancellationToken = default);
    Task<BrowserDialogResult> ClickAndHandleDialogAsync(string clickSelector, string action, string? promptText, int timeoutMs, CancellationToken cancellationToken = default);
    Task<BrowserPageInfo> ClickAndWaitForPopupAsync(string clickSelector, int timeoutMs, bool switchToPopup, CancellationToken cancellationToken = default);
    Task<BrowserTableExtractResult> ExtractTableAsync(BrowserTableExtractOptions options, CancellationToken cancellationToken = default);
    Task OpenAsync(BrowserOptions? options = null, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}

public sealed class BrowserOptions
{
    public bool Headless { get; set; } = true;
    public string? UserAgent { get; set; }
    public int ViewportWidth { get; set; } = 1280;
    public int ViewportHeight { get; set; } = 720;
    public int DefaultTimeoutMs { get; set; } = 30_000;
    public Dictionary<string, string> ExtraHeaders { get; set; } = new();
}
