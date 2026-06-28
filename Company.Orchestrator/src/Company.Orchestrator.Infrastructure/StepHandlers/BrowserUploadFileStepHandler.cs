using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserUploadFileStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<BrowserUploadFileStepHandler> _logger;

    public string HandlerType => "browser.upload-file";

    public BrowserUploadFileStepHandler(
        IArtifactStore store,
        ILogger<BrowserUploadFileStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.upload-file";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        if (!BrowserStepHandlerHelpers.TryRequire(context, config, "selector", out var selector, out var failure))
            return failure!;

        int timeoutMs;
        try
        {
            timeoutMs = BrowserStepHandlerHelpers.ParseTimeoutMs(
                config.GetValueOrDefault("timeoutMs"), 30_000, stepType);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        var artifactNameRaw = config.GetValueOrDefault("artifactName")?.ToString();
        var filePathRaw     = config.GetValueOrDefault("filePath")?.ToString();
        var artifactName    = string.IsNullOrWhiteSpace(artifactNameRaw)
            ? null
            : context.Interpolate(artifactNameRaw);
        var filePath = string.IsNullOrWhiteSpace(filePathRaw)
            ? null
            : context.Interpolate(filePathRaw);

        if (string.IsNullOrWhiteSpace(artifactName) && string.IsNullOrWhiteSpace(filePath))
        {
            return StepResult.Fail(
                $"{stepType}: either 'artifactName' or 'filePath' is required.");
        }

        string uploadPath;
        string sourceType;
        string uploadedFileName;
        string? tempPath = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(artifactName))
            {
                if (!context.HasArtifact(artifactName))
                {
                    return StepResult.Fail(
                        $"{stepType}: artifact '{artifactName}' not found in context.");
                }

                var artifact = context.GetArtifact(artifactName);
                uploadPath = await MaterializeArtifactAsync(artifact, cancellationToken);
                tempPath       = uploadPath;
                sourceType     = "artifact";
                uploadedFileName = artifact.Name;
            }
            else
            {
                uploadPath = filePath!;
                if (!System.IO.File.Exists(uploadPath))
                {
                    return StepResult.Fail(
                        $"{stepType}: file path not found: '{BrowserStepHandlerHelpers.SanitizeForLog(uploadPath)}'.");
                }

                sourceType       = "filePath";
                uploadedFileName = Path.GetFileName(uploadPath);
            }

            _logger.LogInformation(
                "{StepType}: session='{Session}', selector='{Selector}', sourceType={SourceType}, fileName='{FileName}'",
                stepType,
                sessionName,
                selector,
                sourceType,
                BrowserStepHandlerHelpers.SanitizeForLog(uploadedFileName));

            var browser = context.GetCapability<IBrowserCapability>();
            try
            {
                await browser.UploadFileAsync(selector, uploadPath, timeoutMs, cancellationToken);
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                return StepResult.Fail(
                    $"{stepType}: upload timed out after {timeoutMs}ms for selector '{selector}'. {ex.Message}");
            }
            catch (PlaywrightException ex)
            {
                return StepResult.Fail(
                    $"{stepType}: failed to upload file to selector '{selector}'. {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return StepResult.Fail($"{stepType}: {ex.Message}");
            }

            return StepResult.Ok(
                output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["uploadedFileName"]   = uploadedFileName,
                    ["uploadedSourceType"] = sourceType,
                },
                outputData:
                    $"Uploaded '{uploadedFileName}' ({sourceType}) to '{selector}'.");
        }
        finally
        {
            if (tempPath is not null && System.IO.File.Exists(tempPath))
            {
                try { System.IO.File.Delete(tempPath); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{StepType}: failed to delete temp upload file", stepType);
                }
            }
        }
    }

    private async Task<string> MaterializeArtifactAsync(
        ArtifactReference artifact,
        CancellationToken cancellationToken)
    {
        var bytes = await _store.ReadAllBytesAsync(artifact.StoragePath, cancellationToken);
        var extension = Path.GetExtension(artifact.Name);
        var tempPath  = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        await System.IO.File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
        return tempPath;
    }
}
