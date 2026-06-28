using System.Text.Json;
using Company.Orchestrator.Application.Capabilities.Excel;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Reads a sheet from an Excel/CSV artifact and exposes rows as a JSON variable.
///
/// Config keys:
///   artifactName  — name of the workbook artifact in WorkflowContext.Artifacts
///   sheetName     — sheet to read (default: "Sheet1")
///   outputVariable — variable name to store the JSON array of rows (default: "excelRows")
///   hasHeaderRow  — bool (default: true)
/// </summary>
public sealed class ExcelReadStepHandler : IStepHandler
{
    private readonly ILogger<ExcelReadStepHandler> _logger;
    public string HandlerType => "excel.read";

    public ExcelReadStepHandler(ILogger<ExcelReadStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("artifactName", out var artifactNameRaw))
            return StepResult.Fail("ExcelReadStepHandler: 'artifactName' is required.");

        var artifactNameRaw2 = artifactNameRaw?.ToString() ?? "";
        var artifactName     = context.Interpolate(artifactNameRaw2);

        if (!string.Equals(artifactNameRaw2, artifactName, StringComparison.Ordinal))
            _logger.LogInformation(
                "ExcelReadStepHandler: resolved artifactName: '{Raw}' -> '{Resolved}'",
                artifactNameRaw2, artifactName);

        if (!context.HasArtifact(artifactName))
            return StepResult.Fail($"ExcelReadStepHandler: artifact '{artifactName}' not found in context.");

        var sheetName = config.GetValueOrDefault("sheetName")?.ToString() ?? "Sheet1";
        var outputVar = config.GetValueOrDefault("outputVariable")?.ToString() ?? "excelRows";
        var hasHeader = config.GetValueOrDefault("hasHeaderRow")?.ToString()?.Equals("false", StringComparison.OrdinalIgnoreCase) != true;

        var excel = context.GetCapability<IExcelCapability>();
        var artifact = context.GetArtifact(artifactName);

        _logger.LogInformation("ExcelReadStepHandler: reading sheet '{Sheet}' from artifact '{Name}'", sheetName, artifactName);
        var rows = await excel.ReadSheetAsync(artifact, sheetName, hasHeader, cancellationToken);

        var json = JsonSerializer.Serialize(rows);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                [outputVar] = json,
                [$"{outputVar}_count"] = rows.Count
            },
            outputData: $"Read {rows.Count} rows from sheet '{sheetName}'");
    }
}
