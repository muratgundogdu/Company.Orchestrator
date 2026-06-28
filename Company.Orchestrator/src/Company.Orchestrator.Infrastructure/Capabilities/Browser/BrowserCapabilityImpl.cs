using System.Text.Json;
using System.Text.RegularExpressions;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.Capabilities.Browser;

/// <summary>
/// Production BrowserCapability backed by Microsoft.Playwright + Chromium.
///
/// FIRST-TIME SETUP — run once after publish/build:
///   dotnet tool install --global Microsoft.Playwright.CLI
///   playwright install chromium
/// Or use the bundled installer:
///   var exitCode = Program.Main(["install", "chromium"]);
///
/// LIFECYCLE:
///   The capability is registered as a singleton. Browser sessions are serialized
///   via an internal semaphore — one active session at a time. Each step handler
///   is responsible for calling OpenAsync() before use and CloseAsync() afterward.
///
/// THREAD SAFETY:
///   OpenAsync() acquires an exclusive lock. CloseAsync() releases it.
///   Concurrent OpenAsync() calls block until the current session closes.
/// </summary>
public sealed class BrowserCapabilityImpl : IBrowserCapability, IAsyncDisposable
{
    private readonly IArtifactStore _store;
    private readonly ILogger<BrowserCapabilityImpl> _logger;

    // ---- Session state (protected by _semaphore) ----
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _sessionOpen;

    // Ensures one browser session at a time when capability is used as a singleton.
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public string CapabilityName => "Browser";

    public BrowserCapabilityImpl(IArtifactStore store, ILogger<BrowserCapabilityImpl> logger)
    {
        _store = store;
        _logger = logger;
    }

    // ------------------------------------------------------------------ //
    // Lifecycle
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Launches a Chromium browser and creates a new page.
    /// Blocks if another session is already open (serialises concurrent callers).
    /// </summary>
    public async Task OpenAsync(BrowserOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_sessionOpen)
        {
            _logger.LogInformation("BrowserCapability: session already open — reusing existing session");
            return;
        }

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            options ??= new BrowserOptions();

            _playwright = await Playwright.CreateAsync();

            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = options.Headless,
                // Required on Linux (CI / Docker) where there is no sandbox user namespace.
                Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
            });

            var contextOptions = new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width  = options.ViewportWidth,
                    Height = options.ViewportHeight
                },
                // Enable downloads so DownloadFileAsync works.
                AcceptDownloads = true
            };

            if (!string.IsNullOrEmpty(options.UserAgent))
                contextOptions.UserAgent = options.UserAgent;

            _context = await _browser.NewContextAsync(contextOptions);
            _page    = await _context.NewPageAsync();

            _context.Page += (_, page) =>
            {
                page.SetDefaultTimeout(options.DefaultTimeoutMs);
            };

            // Apply default timeout to every action on this page.
            _page.SetDefaultTimeout(options.DefaultTimeoutMs);

            if (options.ExtraHeaders.Count > 0)
                await _page.SetExtraHTTPHeadersAsync(options.ExtraHeaders);

            _sessionOpen = true;

            _logger.LogInformation(
                "BrowserCapability: Chromium launched (headless={H}, viewport={W}×{Ht}, timeout={T}ms)",
                options.Headless, options.ViewportWidth, options.ViewportHeight, options.DefaultTimeoutMs);
        }
        catch (Exception ex)
        {
            // Teardown whatever was partially created, release the lock, re-throw.
            await TeardownAsync();
            _semaphore.Release();
            _logger.LogError(ex, "BrowserCapability: failed to launch Chromium");
            throw;
        }
    }

    /// <summary>Closes the page, browser context, browser, and Playwright instance.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_sessionOpen)
        {
            _logger.LogWarning("BrowserCapability.CloseAsync called with no active session — ignored");
            return;
        }

        try
        {
            await TeardownAsync();
            _logger.LogInformation("BrowserCapability: Chromium closed");
        }
        finally
        {
            _sessionOpen = false;
            _semaphore.Release();
        }
    }

    // ------------------------------------------------------------------ //
    // Navigation
    // ------------------------------------------------------------------ //

    /// <summary>Navigates to the given URL and waits until network is idle.</summary>
    public async Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogInformation("BrowserCapability: navigating → {Url}", url);

        await _page!.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });
    }

    /// <summary>
    /// Waits for the page URL to change and for the network to settle.
    /// Call this BEFORE the action that triggers navigation (e.g. before ClickAsync).
    /// Uses WaitForURLAsync("**") which matches any URL — the Playwright 1.60+ replacement
    /// for the now-obsolete WaitForNavigationAsync.
    /// </summary>
    public async Task WaitForNavigationAsync(int timeoutMs = 10_000, CancellationToken cancellationToken = default)
    {
        EnsureSession();

        await _page!.WaitForURLAsync("**", new PageWaitForURLOptions
        {
            Timeout   = timeoutMs,
            WaitUntil = WaitUntilState.NetworkIdle
        });
    }

    // ------------------------------------------------------------------ //
    // DOM interaction
    // ------------------------------------------------------------------ //

    public async Task ClickAsync(string cssSelector, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: click '{Selector}'", cssSelector);
        await _page!.ClickAsync(cssSelector);
    }

    /// <summary>Clears the target input and types the given text.</summary>
    public async Task TypeAsync(string cssSelector, string text, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: fill '{Selector}'", cssSelector);
        await _page!.FillAsync(cssSelector, text);
    }

    /// <summary>Clears the target input using fill with an empty string.</summary>
    public async Task ClearInputAsync(string cssSelector, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: clear '{Selector}'", cssSelector);
        await _page!.FillAsync(cssSelector, string.Empty);
    }

    /// <summary>
    /// Presses a keyboard key globally or on a specific element when <paramref name="cssSelector"/> is set.
    /// </summary>
    public async Task PressKeyAsync(
        string key,
        string? cssSelector = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        var mapped = MapPlaywrightKey(key);
        _logger.LogDebug(
            "BrowserCapability: press key '{Key}' → '{Mapped}'{Target}",
            key,
            mapped,
            string.IsNullOrWhiteSpace(cssSelector) ? string.Empty : $" on '{cssSelector}'");

        if (string.IsNullOrWhiteSpace(cssSelector))
        {
            await _page!.Keyboard.PressAsync(mapped);
            return;
        }

        await _page!.Locator(cssSelector).PressAsync(mapped);
    }

    public async Task SelectOptionAsync(string cssSelector, string value, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: select option '{Value}' on '{Selector}'", value, cssSelector);
        await _page!.SelectOptionAsync(cssSelector, new SelectOptionValue { Value = value });
    }

    public async Task SelectOptionByValueOrLabelAsync(
        string cssSelector, string? value, string? label, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        if (!string.IsNullOrWhiteSpace(label))
        {
            _logger.LogDebug("BrowserCapability: select label '{Label}' on '{Selector}'", label, cssSelector);
            await _page!.SelectOptionAsync(cssSelector, new SelectOptionValue { Label = label });
            return;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            _logger.LogDebug("BrowserCapability: select value '{Value}' on '{Selector}'", value, cssSelector);
            await _page!.SelectOptionAsync(cssSelector, new SelectOptionValue { Value = value });
            return;
        }

        throw new InvalidOperationException("SelectOption requires either 'value' or 'label'.");
    }

    public async Task SelectOptionByIndexAsync(
        string cssSelector, int index, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: select index {Index} on '{Selector}'", index, cssSelector);
        await _page!.SelectOptionAsync(cssSelector, new SelectOptionValue { Index = index });
    }

    public async Task ScrollAsync(
        string? cssSelector, string direction, int amount, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        var (deltaX, deltaY) = ResolveScrollDelta(direction, amount);

        if (string.IsNullOrWhiteSpace(cssSelector))
        {
            _logger.LogDebug(
                "BrowserCapability: page scroll {Direction} by {Amount}px",
                direction,
                amount);
            await _page!.EvaluateAsync(
                @"({ dx, dy }) => window.scrollBy(dx, dy)",
                new { dx = deltaX, dy = deltaY });
            return;
        }

        _logger.LogDebug(
            "BrowserCapability: element scroll '{Selector}' {Direction} by {Amount}px",
            cssSelector,
            direction,
            amount);
        await _page!.Locator(cssSelector).EvaluateAsync(
            @"(el, args) => {
                if (args.dx !== 0) el.scrollLeft += args.dx;
                if (args.dy !== 0) el.scrollTop += args.dy;
            }",
            new { dx = deltaX, dy = deltaY });
    }

    public async Task HoverAsync(string cssSelector, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: hover '{Selector}'", cssSelector);
        await _page!.Locator(cssSelector).HoverAsync();
    }

    public async Task UploadFileAsync(
        string cssSelector,
        string filePath,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();

        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException($"Upload file not found: '{Path.GetFileName(filePath)}'.");

        _logger.LogDebug(
            "BrowserCapability: upload '{FileName}' via '{Selector}' (timeout={TimeoutMs}ms)",
            Path.GetFileName(filePath),
            cssSelector,
            timeoutMs);

        var locator = _page!.Locator(cssSelector);
        await locator.WaitForAsync(new LocatorWaitForOptions
        {
            State   = WaitForSelectorState.Attached,
            Timeout = timeoutMs,
        });

        var inputType = await locator.GetAttributeAsync("type");
        if (!string.IsNullOrEmpty(inputType)
            && !inputType.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Element '{cssSelector}' is not a file input (type='{inputType}').");
        }

        await locator.SetInputFilesAsync(filePath);
    }

    public Task<IReadOnlyList<BrowserPageInfo>> GetPagesAsync(CancellationToken cancellationToken = default)
    {
        EnsureSession();
        return BuildAllPageInfosAsync();
    }

    public async Task<BrowserPageInfo> SwitchPageAsync(
        string mode,
        string? urlContains,
        string? titleContains,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        var normalizedMode = mode.Trim().ToLowerInvariant();
        var deadline         = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pages = _context!.Pages.ToList();
            if (pages.Count == 0)
                throw new InvalidOperationException("BrowserCapability: no pages available in the session.");

            IPage? target = normalizedMode switch
            {
                "last"  => pages[^1],
                "first" => pages[0],
                "byurl" => pages.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(urlContains)
                    && p.Url.Contains(urlContains, StringComparison.OrdinalIgnoreCase)),
                "bytitle" => await FindPageByTitleAsync(pages, titleContains),
                _ => throw new InvalidOperationException(
                    $"Invalid switch mode '{mode}'. Use last, first, byUrl, or byTitle."),
            };

            if (target is not null)
            {
                _page = target;
                _logger.LogInformation(
                    "BrowserCapability: switched to page index {Index} ({Url})",
                    pages.IndexOf(target),
                    target.Url);
                return await BuildPageInfoAsync(target);
            }

            if (normalizedMode is "last" or "first")
            {
                _page = pages[normalizedMode == "first" ? 0 : ^1];
                return await BuildPageInfoAsync(_page);
            }

            await Task.Delay(200, cancellationToken);
        }

        var currentUrl = _page?.Url ?? string.Empty;
        throw new TimeoutException(
            $"Timed out after {timeoutMs}ms switching tab (mode={mode}, currentUrl={currentUrl}).");
    }

    public async Task<BrowserPageInfo> ClosePageAsync(
        string mode,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        var normalizedMode = mode.Trim().ToLowerInvariant();
        var pages          = _context!.Pages.ToList();

        if (pages.Count == 0)
            throw new InvalidOperationException("BrowserCapability: no pages available to close.");

        if (pages.Count == 1 && normalizedMode == "current")
        {
            throw new InvalidOperationException(
                "Cannot close the only remaining tab. At least one page must stay open in the session.");
        }

        var toClose = normalizedMode switch
        {
            "current" => _page ?? pages[^1],
            "last"    => pages[^1],
            "first"   => pages[0],
            _         => throw new InvalidOperationException(
                $"Invalid close mode '{mode}'. Use current, last, or first."),
        };

        _logger.LogInformation("BrowserCapability: closing page {Url}", toClose.Url);
        await toClose.CloseAsync();

        var remaining = _context.Pages.ToList();
        if (remaining.Count == 0)
        {
            throw new InvalidOperationException(
                "BrowserCapability: no pages remain after closing tab.");
        }

        _page = remaining[^1];
        return await BuildPageInfoAsync(_page);
    }

    public async Task<BrowserDialogResult> WaitForDialogAsync(
        string action,
        string? promptText,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: waiting for dialog (timeout={TimeoutMs}ms)", timeoutMs);

        var dialog = await WaitForNextDialogAsync(_page!, timeoutMs, cancellationToken);
        return await HandleDialogAsync(dialog, action, promptText);
    }

    public async Task<BrowserDialogResult> ClickAndHandleDialogAsync(
        string clickSelector,
        string action,
        string? promptText,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug(
            "BrowserCapability: click '{Selector}' and handle dialog (timeout={TimeoutMs}ms)",
            clickSelector,
            timeoutMs);

        var page       = _page!;
        var dialogTask = WaitForNextDialogAsync(page, timeoutMs, cancellationToken);
        await page.ClickAsync(clickSelector);
        var dialog = await dialogTask;
        return await HandleDialogAsync(dialog, action, promptText);
    }

    public async Task<BrowserPageInfo> ClickAndWaitForPopupAsync(
        string clickSelector,
        int timeoutMs,
        bool switchToPopup,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug(
            "BrowserCapability: click '{Selector}' and wait for popup (timeout={TimeoutMs}ms)",
            clickSelector,
            timeoutMs);

        var popup = await _page!.RunAndWaitForPopupAsync(
            async () => await _page.ClickAsync(clickSelector),
            new PageRunAndWaitForPopupOptions { Timeout = timeoutMs });

        if (switchToPopup)
            _page = popup;

        return await BuildPageInfoAsync(popup);
    }

    public async Task WaitForSelectorAsync(string cssSelector, int timeoutMs = 5_000, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: waiting for '{Selector}' (timeout={T}ms)", cssSelector, timeoutMs);

        await _page!.WaitForSelectorAsync(cssSelector, new PageWaitForSelectorOptions
        {
            State   = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    public async Task WaitForTextAsync(string text, int timeoutMs = 30_000, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: waiting for text '{Text}' (timeout={T}ms)", text, timeoutMs);

        await _page!.WaitForFunctionAsync(
            "(expected) => document.body && document.body.innerText.includes(expected)",
            text,
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    public async Task WaitForTextAsync(
        string text,
        string cssSelector,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug(
            "BrowserCapability: waiting for text '{Text}' in '{Selector}' (timeout={T}ms)",
            text,
            cssSelector,
            timeoutMs);

        await _page!.WaitForFunctionAsync(
            @"([selector, expected]) => {
                const el = document.querySelector(selector);
                return el && el.innerText.includes(expected);
            }",
            new object[] { cssSelector, text },
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    public async Task WaitForUrlContainsAsync(string urlContains, int timeoutMs = 30_000, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        await WaitForUrlAsync(urlContains, "contains", timeoutMs, cancellationToken);
    }

    public async Task WaitForUrlAsync(
        string pattern,
        string matchMode,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        var normalizedMode = matchMode.Trim().ToLowerInvariant();
        _logger.LogDebug(
            "BrowserCapability: waiting for URL {Mode} '{Pattern}' (timeout={T}ms)",
            normalizedMode,
            pattern,
            timeoutMs);

        await _page!.WaitForURLAsync(
            url => UrlMatches(url, pattern, normalizedMode),
            new PageWaitForURLOptions { Timeout = timeoutMs });
    }

    public async Task WaitForNetworkIdleAsync(int timeoutMs, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: waiting for network idle (timeout={T}ms)", timeoutMs);
        await _page!.WaitForLoadStateAsync(
            LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = timeoutMs });
    }

    public async Task<bool> ElementExistsAsync(
        string cssSelector,
        int timeoutMs,
        bool visibleOnly,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        var state = visibleOnly ? WaitForSelectorState.Visible : WaitForSelectorState.Attached;

        try
        {
            await _page!.Locator(cssSelector).First.WaitForAsync(new LocatorWaitForOptions
            {
                State   = state,
                Timeout = timeoutMs,
            });
            return true;
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }

    public Task<string> GetCurrentUrlAsync(CancellationToken cancellationToken = default)
    {
        EnsureSession();
        return Task.FromResult(_page!.Url);
    }

    // ------------------------------------------------------------------ //
    // Data extraction
    // ------------------------------------------------------------------ //

    /// <summary>Returns the visible inner text of the first matching element.</summary>
    public async Task<string> GetTextAsync(string cssSelector, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        return await _page!.InnerTextAsync(cssSelector) ?? string.Empty;
    }

    /// <summary>Returns the specified HTML attribute value of the first matching element.</summary>
    public async Task<string> GetAttributeAsync(string cssSelector, string attribute, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        return await _page!.GetAttributeAsync(cssSelector, attribute) ?? string.Empty;
    }

    /// <summary>Returns the full HTML source of the current page.</summary>
    public async Task<string> GetPageSourceAsync(CancellationToken cancellationToken = default)
    {
        EnsureSession();
        return await _page!.ContentAsync();
    }

    /// <summary>
    /// Evaluates a JavaScript expression in the page context and returns the result as a string.
    /// Primitive return values are coerced to string. Objects/arrays are JSON-serialised.
    /// </summary>
    public async Task<string> EvaluateScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogDebug("BrowserCapability: evaluating script ({Len} chars)", script.Length);

        var result = await _page!.EvaluateAsync(script);

        if (!result.HasValue)
            return string.Empty;

        return result.Value.ValueKind switch
        {
            JsonValueKind.String  => result.Value.GetString() ?? string.Empty,
            JsonValueKind.Null    => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            // Numbers, booleans, objects, arrays → serialise to JSON string
            _ => result.Value.ToString()
        };
    }

    /// <summary>
    /// Waits for table/grid elements and extracts visible cell text into structured rows.
    /// Supports standard HTML tables and CSS grid layouts.
    /// </summary>
    public async Task<BrowserTableExtractResult> ExtractTableAsync(
        BrowserTableExtractOptions options,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();

        var mode = options.Mode?.Trim().ToLowerInvariant() ?? "htmltable";
        if (mode is not ("htmltable" or "cssgrid"))
            throw new InvalidOperationException($"Invalid extract mode '{options.Mode}'. Use 'htmlTable' or 'cssGrid'.");

        var waitSelector = mode == "htmltable"
            ? options.Selector
            : !string.IsNullOrWhiteSpace(options.RowSelector)
                ? options.RowSelector!
                : options.Selector;

        if (string.IsNullOrWhiteSpace(waitSelector))
            throw new InvalidOperationException("A CSS selector is required to wait for table/grid elements.");

        if (mode == "cssgrid")
        {
            if (string.IsNullOrWhiteSpace(options.RowSelector))
                throw new InvalidOperationException("cssGrid mode requires 'rowSelector'.");
            if (string.IsNullOrWhiteSpace(options.CellSelector))
                throw new InvalidOperationException("cssGrid mode requires 'cellSelector'.");
        }

        if (options.TableIndex < 0)
            throw new InvalidOperationException("'tableIndex' must be >= 0.");

        _logger.LogInformation(
            "BrowserCapability: extract-table mode={Mode}, selector='{Selector}', tableIndex={TableIndex}, timeout={TimeoutMs}ms",
            mode,
            waitSelector,
            options.TableIndex,
            options.TimeoutMs);

        try
        {
            await _page!.WaitForSelectorAsync(
                waitSelector,
                new PageWaitForSelectorOptions { Timeout = options.TimeoutMs, State = WaitForSelectorState.Visible });
        }
        catch (TimeoutException ex)
        {
            throw new InvalidOperationException(
                $"Selector '{waitSelector}' not found within {options.TimeoutMs}ms.", ex);
        }

        var payload = new
        {
            mode,
            selector            = options.Selector,
            tableIndex          = options.TableIndex,
            rowSelector         = options.RowSelector ?? string.Empty,
            cellSelector        = options.CellSelector ?? string.Empty,
            includeHeaders      = options.IncludeHeaders,
            skipEmptyRows       = options.SkipEmptyRows,
            normalizeWhitespace = options.NormalizeWhitespace,
        };

        JsonElement raw;
        try
        {
            raw = await _page.EvaluateAsync<JsonElement>(TableExtractScript, payload);
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException($"Failed to extract table data: {ex.Message}", ex);
        }

        if (raw.TryGetProperty("error", out var errProp))
            throw new InvalidOperationException(errProp.GetString() ?? "Table extraction failed.");

        var columns = ParseStringArray(raw, "columns");
        var rowArrays = ParseRowArrays(raw, "rows");

        if (rowArrays.Count == 0)
            throw new InvalidOperationException("No data rows found in the matched table or grid.");

        var colCount = columns.Count > 0
            ? columns.Count
            : rowArrays.Max(r => r.Count);

        if (columns.Count == 0)
        {
            columns = Enumerable.Range(1, colCount).Select(i => $"Column{i}").ToList();
        }
        else if (columns.Count < colCount)
        {
            for (var i = columns.Count; i < colCount; i++)
                columns.Add($"Column{i + 1}");
        }

        var rows = new List<Dictionary<string, string>>(rowArrays.Count);
        foreach (var cells in rowArrays)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < colCount; i++)
                row[columns[i]] = i < cells.Count ? cells[i] : string.Empty;
            rows.Add(row);
        }

        _logger.LogInformation(
            "BrowserCapability: extract-table complete — {RowCount} row(s), {ColumnCount} column(s)",
            rows.Count,
            columns.Count);

        return new BrowserTableExtractResult
        {
            Columns = columns,
            Rows    = rows,
        };
    }

    private static List<string> ParseStringArray(JsonElement root, string property)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
            list.Add(item.GetString() ?? string.Empty);

        return list;
    }

    private static List<List<string>> ParseRowArrays(JsonElement root, string property)
    {
        var rows = new List<List<string>>();
        if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return rows;

        foreach (var rowEl in arr.EnumerateArray())
        {
            var cells = new List<string>();
            if (rowEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var cell in rowEl.EnumerateArray())
                    cells.Add(cell.GetString() ?? string.Empty);
            }
            rows.Add(cells);
        }

        return rows;
    }

    private const string TableExtractScript = """
        (opts) => {
          const normalize = (text) => {
            const t = text ?? '';
            return opts.normalizeWhitespace ? t.replace(/\s+/g, ' ').trim() : t.trim();
          };
          const isEmptyRow = (cells) => cells.every(c => !c || c.length === 0);

          if (opts.mode === 'htmltable') {
            const tables = Array.from(document.querySelectorAll(opts.selector));
            if (opts.tableIndex >= tables.length) {
              return { error: `table index ${opts.tableIndex} out of range (found ${tables.length} table(s))` };
            }
            const table = tables[opts.tableIndex];
            let headers = [];
            const theadRow = table.querySelector('thead tr');
            if (theadRow) {
              headers = Array.from(theadRow.querySelectorAll('th, td')).map(el => normalize(el.innerText));
            }

            let rowEls = Array.from(table.querySelectorAll('tbody tr'));
            if (rowEls.length === 0) {
              rowEls = Array.from(table.querySelectorAll('tr')).filter(tr => !tr.closest('thead'));
            }

            let dataStart = 0;
            if (headers.length === 0 && opts.includeHeaders && rowEls.length > 0) {
              headers = Array.from(rowEls[0].querySelectorAll('th, td')).map(el => normalize(el.innerText));
              dataStart = 1;
            }

            const rows = [];
            for (let i = dataStart; i < rowEls.length; i++) {
              const cells = Array.from(rowEls[i].querySelectorAll('th, td')).map(el => normalize(el.innerText));
              if (opts.skipEmptyRows && isEmptyRow(cells)) continue;
              rows.push(cells);
            }

            const colCount = Math.max(headers.length, ...rows.map(r => r.length), 0);
            if (headers.length === 0 && colCount > 0) {
              headers = Array.from({ length: colCount }, (_, i) => `Column${i + 1}`);
            } else if (headers.length < colCount) {
              for (let i = headers.length; i < colCount; i++) headers.push(`Column${i + 1}`);
            }

            return { columns: headers, rows };
          }

          const scope = opts.selector
            ? document.querySelector(opts.selector)
            : document;
          if (opts.selector && !scope) {
            return { error: `scope selector '${opts.selector}' not found` };
          }

          const rowEls = Array.from(scope.querySelectorAll(opts.rowSelector));
          if (rowEls.length === 0) {
            return { error: `no rows matched rowSelector '${opts.rowSelector}'` };
          }

          let headers = [];
          let dataStart = 0;
          const allRows = rowEls.map(row =>
            Array.from(row.querySelectorAll(opts.cellSelector)).map(el => normalize(el.innerText))
          );

          if (opts.includeHeaders && allRows.length > 0) {
            headers = allRows[0];
            dataStart = 1;
          }

          const rows = [];
          for (let i = dataStart; i < allRows.length; i++) {
            const cells = allRows[i];
            if (opts.skipEmptyRows && isEmptyRow(cells)) continue;
            rows.push(cells);
          }

          const colCount = Math.max(headers.length, ...rows.map(r => r.length), 0);
          if (headers.length === 0 && colCount > 0) {
            headers = Array.from({ length: colCount }, (_, i) => `Column${i + 1}`);
          } else if (headers.length < colCount) {
            for (let i = headers.length; i < colCount; i++) headers.push(`Column${i + 1}`);
          }

          return { columns: headers, rows };
        }
        """;

    // ------------------------------------------------------------------ //
    // Artifact-producing operations
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Takes a full-page PNG screenshot of the current page and stores it as an artifact.
    /// Returns a valid ArtifactReference pointing to the real screenshot content.
    /// </summary>
    public async Task<ArtifactReference> TakeScreenshotAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        EnsureSession();

        var artifactName = string.IsNullOrWhiteSpace(name)
            ? $"screenshot_{DateTime.UtcNow:yyyyMMddHHmmss}.png"
            : name;

        _logger.LogInformation("BrowserCapability: capturing screenshot → {Name}", artifactName);

        var pngBytes = await _page!.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = true,
            Type     = ScreenshotType.Png
        });

        var id = Guid.NewGuid();
        using var ms = new MemoryStream(pngBytes);
        var storagePath = await _store.SaveAsync(id, artifactName, ms, cancellationToken);

        _logger.LogInformation(
            "BrowserCapability: screenshot saved ({Bytes:N0} bytes) → artifact {Id}",
            pngBytes.Length, id);

        return new ArtifactReference
        {
            Id          = id,
            Name        = artifactName,
            ContentType = "image/png",
            StoragePath = storagePath,
            SizeBytes   = pngBytes.Length,
            Metadata    = new Dictionary<string, string>
            {
                ["pageUrl"]  = _page.Url,
                ["fullPage"] = "true"
            }
        };
    }

    /// <summary>
    /// Clicks an element and waits for the browser download event, then stores the file as an artifact.
    /// </summary>
    public Task<ArtifactReference> DownloadByClickAsync(
        string cssSelector, string name, CancellationToken cancellationToken = default)
        => DownloadByClickAsync(cssSelector, name, 60_000, cancellationToken);

    public async Task<ArtifactReference> DownloadByClickAsync(
        string cssSelector, string name, int timeoutMs, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogInformation(
            "BrowserCapability: download via click '{Selector}' → '{Name}' (timeout={TimeoutMs}ms)",
            cssSelector,
            name,
            timeoutMs);

        var download = await _page!.RunAndWaitForDownloadAsync(
            async () => await _page.ClickAsync(cssSelector),
            new PageRunAndWaitForDownloadOptions { Timeout = timeoutMs });

        return await SaveDownloadAsync(download, name, cssSelector, cancellationToken);
    }

    private static string ResolveDownloadArtifactName(string requestedName, string suggestedFilename)
    {
        if (Path.HasExtension(requestedName))
            return requestedName;

        var ext = Path.GetExtension(suggestedFilename);
        return string.IsNullOrEmpty(ext) ? requestedName : requestedName + ext;
    }

    /// <summary>
    /// Navigates to the download URL, waits for the browser to initiate the download,
    /// streams the result to the artifact store, and returns an ArtifactReference.
    /// </summary>
    public async Task<ArtifactReference> DownloadFileAsync(
        string downloadUrl, string name, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        _logger.LogInformation("BrowserCapability: downloading '{Url}' → '{Name}'", downloadUrl, name);

        var download = await _page!.RunAndWaitForDownloadAsync(async () =>
        {
            await _page.GotoAsync(downloadUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Commit
            });
        });

        return await SaveDownloadAsync(download, name, downloadUrl, cancellationToken);
    }

    private async Task<ArtifactReference> SaveDownloadAsync(
        IDownload download,
        string name,
        string source,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            await download.SaveAsAsync(tempPath);

            if (!System.IO.File.Exists(tempPath))
                throw new InvalidOperationException(
                    $"Playwright download from '{source}' did not produce a file.");

            var id   = Guid.NewGuid();
            var info = new System.IO.FileInfo(tempPath);
            var sizeBytes = info.Length;
            var artifactName = ResolveDownloadArtifactName(name, download.SuggestedFilename);

            string storagePath;
            await using (var fs = System.IO.File.OpenRead(tempPath))
            {
                storagePath = await _store.SaveAsync(id, artifactName, fs, cancellationToken);
            }

            _logger.LogInformation(
                "BrowserCapability: download complete ({Bytes:N0} bytes) → artifact {Id} as '{Name}'",
                sizeBytes, id, artifactName);

            return new ArtifactReference
            {
                Id          = id,
                Name        = artifactName,
                ContentType = ResolveMimeType(artifactName),
                StoragePath = storagePath,
                SizeBytes   = sizeBytes,
                Metadata    = new Dictionary<string, string>
                {
                    ["downloadSource"]    = source,
                    ["suggestedFilename"] = download.SuggestedFilename
                }
            };
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    // ------------------------------------------------------------------ //
    // IAsyncDisposable — called on application shutdown
    // ------------------------------------------------------------------ //

    public async ValueTask DisposeAsync()
    {
        if (_sessionOpen)
            await CloseAsync();

        _semaphore.Dispose();
    }

    // ------------------------------------------------------------------ //
    // Private helpers
    // ------------------------------------------------------------------ //

    private async Task<BrowserPageInfo> BuildPageInfoAsync(IPage page)
    {
        var pages = _context!.Pages.ToList();
        var index = pages.IndexOf(page);
        return new BrowserPageInfo
        {
            Url   = page.Url,
            Title = await page.TitleAsync(),
            Index = index >= 0 ? index : Math.Max(pages.Count - 1, 0),
        };
    }

    private async Task<IReadOnlyList<BrowserPageInfo>> BuildAllPageInfosAsync()
    {
        var results = new List<BrowserPageInfo>();
        foreach (var page in _context!.Pages)
            results.Add(await BuildPageInfoAsync(page));
        return results;
    }

    private static async Task<IPage?> FindPageByTitleAsync(
        IReadOnlyList<IPage> pages,
        string? titleContains)
    {
        if (string.IsNullOrWhiteSpace(titleContains))
            return null;

        foreach (var page in pages)
        {
            var title = await page.TitleAsync();
            if (title.Contains(titleContains, StringComparison.OrdinalIgnoreCase))
                return page;
        }

        return null;
    }

    private static async Task<IDialog> WaitForNextDialogAsync(
        IPage page,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<IDialog>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<IDialog>? handler = null;
        handler = (_, dialog) =>
        {
            if (handler is not null)
                page.Dialog -= handler;
            tcs.TrySetResult(dialog);
        };

        page.Dialog += handler;
        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        }
        catch
        {
            if (handler is not null)
                page.Dialog -= handler;
            throw;
        }
    }

    private static async Task<BrowserDialogResult> HandleDialogAsync(
        IDialog dialog,
        string action,
        string? promptText)
    {
        var dialogType = dialog.Type.ToString().ToLowerInvariant();
        var message    = dialog.Message;

        if (action.Equals("accept", StringComparison.OrdinalIgnoreCase))
        {
            if (dialog.Type == DialogType.Prompt && !string.IsNullOrWhiteSpace(promptText))
                await dialog.AcceptAsync(promptText);
            else
                await dialog.AcceptAsync();
        }
        else if (action.Equals("dismiss", StringComparison.OrdinalIgnoreCase))
        {
            await dialog.DismissAsync();
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid dialog action '{action}'. Use accept or dismiss.");
        }

        return new BrowserDialogResult
        {
            DialogType = dialogType,
            Message    = message,
            Handled    = true,
        };
    }

    private static bool UrlMatches(string url, string pattern, string matchMode) =>
        matchMode switch
        {
            "contains"   => url.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            "equals"     => string.Equals(url, pattern, StringComparison.OrdinalIgnoreCase),
            "startswith" => url.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
            "regex"      => Regex.IsMatch(url, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
            _            => throw new InvalidOperationException(
                $"Invalid URL match mode '{matchMode}'. Use contains, equals, startsWith, or regex."),
        };

    private static string MapPlaywrightKey(string key)
    {
        var trimmed = key.Trim();
        return trimmed switch
        {
            "Enter"      => "Enter",
            "Tab"        => "Tab",
            "Escape"     => "Escape",
            "ArrowDown"  => "ArrowDown",
            "ArrowUp"    => "ArrowUp",
            "ArrowLeft"  => "ArrowLeft",
            "ArrowRight" => "ArrowRight",
            "PageUp"     => "PageUp",
            "PageDown"   => "PageDown",
            "Home"       => "Home",
            "End"        => "End",
            "Delete"     => "Delete",
            "Backspace"  => "Backspace",
            "Space"      => " ",
            "Ctrl+A"     => "Control+A",
            "Control+A"  => "Control+A",
            "Ctrl+S"     => "Control+S",
            "Control+S"  => "Control+S",
            _            => trimmed
        };
    }

    private static (int DeltaX, int DeltaY) ResolveScrollDelta(string direction, int amount)
    {
        var normalized = direction.Trim().ToLowerInvariant();
        return normalized switch
        {
            "up"    => (0, -amount),
            "down"  => (0, amount),
            "left"  => (-amount, 0),
            "right" => (amount, 0),
            _       => throw new InvalidOperationException(
                $"Invalid scroll direction '{direction}'. Use up, down, left, or right."),
        };
    }

    private void EnsureSession()
    {
        if (!_sessionOpen || _page is null)
            throw new InvalidOperationException(
                "BrowserCapability: no active browser session. " +
                "Call OpenAsync() before invoking any browser method.");
    }

    private async Task TeardownAsync()
    {
        if (_page is not null)
        {
            try { await _page.CloseAsync(); } catch { /* ignore — we're tearing down */ }
            _page = null;
        }

        if (_context is not null)
        {
            try { await _context.CloseAsync(); } catch { }
            _context = null;
        }

        if (_browser is not null)
        {
            try { await _browser.CloseAsync(); } catch { }
            _browser = null;
        }

        if (_playwright is not null)
        {
            try { _playwright.Dispose(); } catch { }
            _playwright = null;
        }
    }

    private static string ResolveMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf"  => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls"  => "application/vnd.ms-excel",
            ".csv"  => "text/csv",
            ".zip"  => "application/zip",
            ".png"  => "image/png",
            ".jpg"  => "image/jpeg",
            ".html" => "text/html",
            ".xml"  => "application/xml",
            ".json" => "application/json",
            _       => "application/octet-stream"
        };
}
