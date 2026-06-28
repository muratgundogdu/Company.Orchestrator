using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserWaitDownloadStepHandler : IStepHandler
{
    private readonly ILogger<BrowserWaitDownloadStepHandler> _logger;
    public string HandlerType => "browser.wait-download";

    public BrowserWaitDownloadStepHandler(ILogger<BrowserWaitDownloadStepHandler> logger) => _logger = logger;

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.wait-download";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "clickSelector", out var clickSelector, out var failure))
            return failure!;

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "artifactName", out var artifactName, out failure))
            return failure!;

        int timeoutMs;
        try
        {
            timeoutMs = BrowserStepHandlerHelpers.ParseTimeoutMs(
                config.GetValueOrDefault("timeoutMs"), 60_000, stepType);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        _logger.LogInformation(
            "{StepType}: session='{Session}', clickSelector='{Selector}', artifactName='{Name}', timeoutMs={Timeout}",
            stepType,
            sessionName,
            clickSelector,
            artifactName,
            timeoutMs);

        var browser = context.GetCapability<IBrowserCapability>();
        try
        {
            var artifact = await browser.DownloadByClickAsync(
                clickSelector, artifactName, timeoutMs, cancellationToken);

            _logger.LogInformation(
                "{StepType}: downloaded '{FileName}' ({Bytes} bytes)",
                stepType,
                artifact.Name,
                artifact.SizeBytes);

            return StepResult.Ok(
                output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["artifactName"]            = artifact.Name,
                    ["downloadedFileName"]      = artifact.Name,
                    ["downloadedFileSizeBytes"] = artifact.SizeBytes,
                },
                artifacts: [artifact],
                outputData:
                    $"Downloaded '{artifact.Name}' ({artifact.SizeBytes:N0} bytes) via click on '{clickSelector}'.");
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail(
                $"{stepType}: timed out after {timeoutMs}ms waiting for download from '{clickSelector}'. {ex.Message}");
        }
    }
}
