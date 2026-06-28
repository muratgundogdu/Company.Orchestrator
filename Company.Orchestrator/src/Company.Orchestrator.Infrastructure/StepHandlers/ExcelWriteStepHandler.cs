using System.Text.Json;
using Company.Orchestrator.Application.Capabilities.Excel;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Writes tabular data from a workflow variable into a .xlsx workbook artifact.
///
/// Config keys:
///   outputName      (required) — artifact name for the produced workbook, e.g. "report.xlsx"
///   sheetName       (optional) — target worksheet name (default: "Sheet1")
///   dataVariable    (optional) — context variable holding a JSON array of row objects
///                                (produced by excel.read, a SQL query, or any prior step)
///   inputArtifact   (optional) — artifact name of an existing workbook to write into;
///                                when omitted a new empty workbook is created first
///   includeHeader   (optional) — bool string, default "true"
///   staticData      (optional) — inline JSON array string for use without a prior step,
///                                e.g. '[{"Name":"Alice","Score":95},{"Name":"Bob","Score":88}]'
///
/// Output variables:
///   {outputName}_artifactId   — Guid of the produced artifact
///   {outputName}_rowCount     — number of data rows written
/// </summary>
public sealed class ExcelWriteStepHandler : IStepHandler
{
    private readonly ILogger<ExcelWriteStepHandler> _logger;
    public string HandlerType => "excel.write";

    public ExcelWriteStepHandler(ILogger<ExcelWriteStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("outputName", out var outputNameRaw) || outputNameRaw is null)
            return StepResult.Fail("ExcelWriteStepHandler: 'outputName' is required.");

        var outputName   = context.Interpolate(outputNameRaw.ToString()!);
        var sheetName    = config.GetValueOrDefault("sheetName")?.ToString() ?? "Sheet1";
        var includeHdr   = !string.Equals(
            config.GetValueOrDefault("includeHeader")?.ToString(), "false",
            StringComparison.OrdinalIgnoreCase);

        // ---- Resolve row data ----
        var rows = ResolveRows(config, context);
        if (rows is null)
            return StepResult.Fail(
                "ExcelWriteStepHandler: no row data found. " +
                "Set 'dataVariable' to a context variable containing a JSON array, " +
                "or provide 'staticData' with an inline JSON array.");

        // ---- Resolve or create the target workbook ----
        var excel = context.GetCapability<IExcelCapability>();

        Application.Artifacts.ArtifactReference workbook;

        if (config.TryGetValue("inputArtifact", out var inputArtifactRaw) &&
            inputArtifactRaw is not null)
        {
            var inputName = context.Interpolate(inputArtifactRaw.ToString()!);
            if (!context.HasArtifact(inputName))
                return StepResult.Fail(
                    $"ExcelWriteStepHandler: inputArtifact '{inputName}' not found in context.");
            workbook = context.GetArtifact(inputName);
        }
        else
        {
            _logger.LogInformation(
                "ExcelWriteStepHandler: no inputArtifact — creating new workbook '{Name}'", outputName);
            workbook = await excel.CreateWorkbookAsync(outputName, cancellationToken);
        }

        // ---- Write data ----
        _logger.LogInformation(
            "ExcelWriteStepHandler: writing {Count} rows to sheet '{Sheet}'",
            rows.Count, sheetName);

        var result = await excel.WriteSheetAsync(
            workbook, sheetName, rows, includeHdr, cancellationToken);

        var varBase = SanitizeVarName(outputName);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                [$"{varBase}_artifactId"] = result.Id.ToString(),
                [$"{varBase}_rowCount"]   = rows.Count
            },
            artifacts: new List<Application.Artifacts.ArtifactReference> { result },
            outputData: $"Wrote {rows.Count} rows to sheet '{sheetName}' in '{result.Name}'");
    }

    // ---- Helpers ----

    private static List<Dictionary<string, object?>>? ResolveRows(
        Dictionary<string, object> config, WorkflowContext context)
    {
        // Try context variable first
        if (config.TryGetValue("dataVariable", out var dataVarRaw) && dataVarRaw is not null)
        {
            var varName = dataVarRaw.ToString()!;
            if (context.TryGetVariable<string>(varName, out var jsonStr) &&
                !string.IsNullOrWhiteSpace(jsonStr))
                return DeserializeRows(jsonStr!);

            // Variable might already be a List<Dictionary<string, object?>>
            if (context.TryGetVariable<List<Dictionary<string, object?>>>(varName, out var typed))
                return typed;
        }

        // Inline static data
        if (config.TryGetValue("staticData", out var staticRaw) && staticRaw is not null)
            return DeserializeRows(staticRaw.ToString()!);

        return null;
    }

    private static List<Dictionary<string, object?>>? DeserializeRows(string json)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
            if (list is null) return null;

            return list.Select(row =>
                row.ToDictionary(
                    kv => kv.Key,
                    kv => (object?)JsonElementToObject(kv.Value)))
                .ToList();
        }
        catch { return null; }
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String  => el.GetString(),
        JsonValueKind.Number  => el.TryGetDouble(out var d) ? d : (object?)el.GetRawText(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        JsonValueKind.Null    => null,
        _                     => el.GetRawText()
    };

    private static string SanitizeVarName(string name) =>
        Path.GetFileNameWithoutExtension(name)
            .Replace(' ', '_')
            .Replace('-', '_');
}
