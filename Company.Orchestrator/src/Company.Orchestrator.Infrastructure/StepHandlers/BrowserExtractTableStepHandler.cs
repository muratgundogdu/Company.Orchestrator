using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public sealed class BrowserExtractTableStepHandler : IStepHandler
{
    private readonly ILogger<BrowserExtractTableStepHandler> _logger;

    public string HandlerType => "browser.extract-table";

    public BrowserExtractTableStepHandler(ILogger<BrowserExtractTableStepHandler> logger)
        => _logger = logger;

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "browser.extract-table";
        var config = context.StepDefinition.Config;
        var sessionName = BrowserStepHandlerHelpers.GetSessionName(config, context);

        var outputVar = config.GetValueOrDefault("outputVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(outputVar))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");

        var mode = config.GetValueOrDefault("mode")?.ToString()?.Trim().ToLowerInvariant() ?? "htmltable";
        if (mode is not ("htmltable" or "cssgrid"))
            return StepResult.Fail($"{stepType}: 'mode' must be 'htmlTable' or 'cssGrid'.");

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

        var selectorRaw   = config.GetValueOrDefault("selector")?.ToString();
        var rowSelectorRaw    = config.GetValueOrDefault("rowSelector")?.ToString();
        var cellSelectorRaw   = config.GetValueOrDefault("cellSelector")?.ToString();
        var tableIndex        = BrowserStepHandlerHelpers.ParseInt(config.GetValueOrDefault("tableIndex"), defaultValue: 0);
        var includeHeaders    = BrowserStepHandlerHelpers.ParseBool(config.GetValueOrDefault("includeHeaders"), defaultValue: true);
        var skipEmptyRows     = BrowserStepHandlerHelpers.ParseBool(config.GetValueOrDefault("skipEmptyRows"), defaultValue: true);
        var normalizeWhitespace = BrowserStepHandlerHelpers.ParseBool(config.GetValueOrDefault("normalizeWhitespace"), defaultValue: true);

        if (tableIndex < 0)
            return StepResult.Fail($"{stepType}: 'tableIndex' must be >= 0.");

        string selector;
        string? rowSelector = null;
        string? cellSelector = null;

        if (mode == "htmltable")
        {
            if (string.IsNullOrWhiteSpace(selectorRaw))
                return StepResult.Fail($"{stepType}: 'selector' is required for htmlTable mode.");

            selector = context.Interpolate(selectorRaw);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rowSelectorRaw))
                return StepResult.Fail($"{stepType}: 'rowSelector' is required for cssGrid mode.");
            if (string.IsNullOrWhiteSpace(cellSelectorRaw))
                return StepResult.Fail($"{stepType}: 'cellSelector' is required for cssGrid mode.");

            rowSelector = context.Interpolate(rowSelectorRaw);
            cellSelector = context.Interpolate(cellSelectorRaw);
            selector = string.IsNullOrWhiteSpace(selectorRaw)
                ? string.Empty
                : context.Interpolate(selectorRaw);
        }

        var options = new BrowserTableExtractOptions
        {
            Mode                = mode == "cssgrid" ? "cssGrid" : "htmlTable",
            Selector            = selector,
            TableIndex          = tableIndex,
            RowSelector         = rowSelector,
            CellSelector        = cellSelector,
            IncludeHeaders      = includeHeaders,
            SkipEmptyRows       = skipEmptyRows,
            NormalizeWhitespace = normalizeWhitespace,
            TimeoutMs           = timeoutMs,
        };

        _logger.LogInformation(
            "{StepType}: session='{Session}', mode={Mode}, selector='{Selector}', tableIndex={TableIndex}, output='{OutputVar}'",
            stepType,
            sessionName,
            options.Mode,
            mode == "htmltable" ? selector : rowSelector ?? selector,
            tableIndex,
            outputVar);

        var browser = context.GetCapability<IBrowserCapability>();

        BrowserTableExtractResult result;
        try
        {
            result = await browser.ExtractTableAsync(options, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }
        catch (PlaywrightException ex)
        {
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }

        _logger.LogInformation(
            "{StepType}: extracted {RowCount} row(s), {ColumnCount} column(s) → '{OutputVar}'",
            stepType,
            result.Rows.Count,
            result.Columns.Count,
            outputVar);

        return PdfStepHandlerHelpers.BuildDataTableOutput(outputVar, result.Columns, result.Rows);
    }
}
