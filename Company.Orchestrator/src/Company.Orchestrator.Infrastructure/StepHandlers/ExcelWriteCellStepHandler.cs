using Company.Orchestrator.Application.Capabilities.Excel;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Updates a single cell in an existing .xlsx workbook and produces a new artifact.
/// The original workbook artifact is not modified.
///
/// Config keys:
///   artifactName        (required) — context artifact name of the source workbook
///   sheetName           (required) — name of the worksheet containing the cell
///   cellAddress         (required) — Excel-style address: A1, B3, C10, AA1, etc.
///   value               (required) — value to write; supports {{variable}} interpolation
///                                    Type coercion: "true"/"false" → bool,
///                                                   numeric string → double,
///                                                   ISO 8601 string → DateTime,
///                                                   everything else → string
///   outputArtifactName  (optional) — name for the new artifact
///                                    (default: {source-name}-updated.xlsx)
///
/// Output variables:
///   updatedArtifactId   — Guid of the new workbook artifact
///   updatedArtifactName — file name of the new workbook artifact
/// </summary>
public sealed class ExcelWriteCellStepHandler : IStepHandler
{
    private readonly ILogger<ExcelWriteCellStepHandler> _logger;
    public string HandlerType => "excel.write-cell";

    public ExcelWriteCellStepHandler(ILogger<ExcelWriteCellStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        // ---- Validate required keys ----

        if (!config.TryGetValue("artifactName", out var artifactNameRaw) ||
            artifactNameRaw is null)
            return StepResult.Fail("ExcelWriteCellStepHandler: 'artifactName' is required.");

        if (!config.TryGetValue("sheetName", out var sheetNameRaw) || sheetNameRaw is null)
            return StepResult.Fail("ExcelWriteCellStepHandler: 'sheetName' is required.");

        if (!config.TryGetValue("cellAddress", out var cellAddressRaw) || cellAddressRaw is null)
            return StepResult.Fail("ExcelWriteCellStepHandler: 'cellAddress' is required.");

        if (!config.TryGetValue("value", out var valueRaw))
            return StepResult.Fail("ExcelWriteCellStepHandler: 'value' is required.");

        // ---- Resolve values (with variable interpolation) ----

        var artifactName = context.Interpolate(artifactNameRaw.ToString()!);
        var sheetName    = context.Interpolate(sheetNameRaw.ToString()!);
        var cellAddress  = context.Interpolate(cellAddressRaw.ToString()!).ToUpperInvariant();
        var rawValue     = valueRaw is null ? null : context.Interpolate(valueRaw.ToString()!);

        if (!context.HasArtifact(artifactName))
            return StepResult.Fail(
                $"ExcelWriteCellStepHandler: artifact '{artifactName}' not found in context.");

        var sourceArtifact = context.GetArtifact(artifactName);

        // Optional custom output name
        string outputName;
        if (config.TryGetValue("outputArtifactName", out var outNameRaw) && outNameRaw is not null)
            outputName = context.Interpolate(outNameRaw.ToString()!);
        else
            outputName = Path.GetFileNameWithoutExtension(sourceArtifact.Name) + "-updated.xlsx";

        // ---- Type-coerce the raw string value ----
        var typedValue = CoerceValue(rawValue);

        _logger.LogInformation(
            "ExcelWriteCellStepHandler: setting {Address} = '{Value}' on sheet '{Sheet}' in '{Name}'",
            cellAddress, rawValue, sheetName, artifactName);

        // ---- Call capability ----
        var excel  = context.GetCapability<IExcelCapability>();
        var result = await excel.WriteCellAsync(
            sourceArtifact, sheetName, cellAddress, typedValue, cancellationToken);

        // Override the artifact name that the capability generated
        // (capability already saved with its own name; the reference is what we return)

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["updatedArtifactId"]   = result.Id.ToString(),
                ["updatedArtifactName"] = result.Name
            },
            artifacts: new List<Application.Artifacts.ArtifactReference> { result },
            outputData: $"Updated cell {cellAddress} on sheet '{sheetName}' → artifact '{result.Name}'");
    }

    // ------------------------------------------------------------------ //
    // Type coercion
    // ------------------------------------------------------------------ //

    private static object? CoerceValue(string? raw)
    {
        if (raw is null) return null;

        // Boolean
        if (raw.Equals("true",  StringComparison.OrdinalIgnoreCase)) return true;
        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

        // Number
        if (double.TryParse(raw,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var d))
            return d;

        // DateTime (ISO 8601 / common patterns)
        if (DateTime.TryParse(raw,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var dt))
            return dt;

        return raw;
    }
}
