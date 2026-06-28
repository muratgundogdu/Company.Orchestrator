using System.Collections.Concurrent;
using Company.Orchestrator.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.BrowserPicker;

public sealed class BrowserPickerService : IBrowserPickerService, IAsyncDisposable
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);

    private readonly ILogger<BrowserPickerService> _logger;
    private readonly ConcurrentDictionary<Guid, BrowserPickerSession> _sessions = new();
    private readonly Timer _cleanupTimer;

    public BrowserPickerService(ILogger<BrowserPickerService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(_ => _ = CleanupExpiredAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task<BrowserPickerStartResult> StartAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));

        await CleanupExpiredAsync();

        var sessionId = Guid.NewGuid();
        var session = new BrowserPickerSession
        {
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = ["--no-sandbox", "--disable-dev-shm-usage"]
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1366, Height = 768 }
            });

            var page = await context.NewPageAsync();

            await page.ExposeFunctionAsync<string, Task<bool>>("alteroneReportSelector", async _ =>
            {
                if (!_sessions.TryGetValue(sessionId, out var active) || active.Page is null)
                    return true;

                try
                {
                    active.Selected = await BrowserPickerCandidateGenerator.BuildAsync(active.Page, _logger);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BrowserPicker: candidate generation failed for session {SessionId}", sessionId);
                }

                return true;
            });

            await page.AddInitScriptAsync(BrowserPickerScript.Source);
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout   = 60_000,
            });

            session.Playwright = playwright;
            session.Browser    = browser;
            session.Context    = context;
            session.Page       = page;

            _sessions[sessionId] = session;

            _logger.LogInformation("BrowserPicker: started session {SessionId} for {Url}", sessionId, url);
            return new BrowserPickerStartResult(sessionId, "started");
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    public async Task StopAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.DisposeAsync();
            _logger.LogInformation("BrowserPicker: stopped session {SessionId}", sessionId);
        }
    }

    public BrowserPickerSelectedResult? GetSelected(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return null;

        session.Touch();
        return session.Selected ?? new BrowserPickerSelectedResult();
    }

    private async Task CleanupExpiredAsync()
    {
        var cutoff = DateTime.UtcNow - SessionTtl;
        foreach (var pair in _sessions)
        {
            if (pair.Value.LastTouchedUtc < cutoff)
                await StopAsync(pair.Key);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cleanupTimer.DisposeAsync();
        foreach (var sessionId in _sessions.Keys.ToList())
            await StopAsync(sessionId);
    }

    private sealed class BrowserPickerSession : IAsyncDisposable
    {
        public Guid SessionId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime LastTouchedUtc { get; private set; } = DateTime.UtcNow;
        public BrowserPickerSelectedResult? Selected { get; set; }

        public IPlaywright? Playwright { get; set; }
        public IBrowser? Browser { get; set; }
        public IBrowserContext? Context { get; set; }
        public IPage? Page { get; set; }

        public void Touch() => LastTouchedUtc = DateTime.UtcNow;

        public async ValueTask DisposeAsync()
        {
            if (Page is not null)
            {
                try { await Page.CloseAsync(); } catch { /* ignore */ }
                Page = null;
            }

            if (Context is not null)
            {
                try { await Context.CloseAsync(); } catch { /* ignore */ }
                Context = null;
            }

            if (Browser is not null)
            {
                try { await Browser.CloseAsync(); } catch { /* ignore */ }
                Browser = null;
            }

            if (Playwright is not null)
            {
                try { Playwright.Dispose(); } catch { /* ignore */ }
                Playwright = null;
            }
        }
    }
}
