using System.Text;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.Excel;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Exports a worksheet from a .xlsx workbook artifact to a CSV string and,
/// optionally, to a separate .csv artifact in the artifact store.
///
/// Config keys:
///   artifactName        (required) — context artifact name of the source workbook
///   sheetName           (optional) — worksheet to export (default: "Sheet1")
///   outputVariable      (optional) — variable name to store the CSV text (default: "csvText")
///   saveAsArtifact      (optional) — "true" to also save as a .csv artifact (default: "false")
///   outputArtifactName  (optional) — name for the .csv artifact
///                                    (default: {workbook-name}.csv)
///
/// Output variables:
///   {outputVariable}           — full CSV text
///   {outputVariable}_lineCount — number of lines including header
/// </summary>
public sealed class ExcelToCsvStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<ExcelToCsvStepHandler> _logger;
    public string HandlerType => "excel.to-csv";

    public ExcelToCsvStepHandler(IArtifactStore store, ILogger<ExcelToCsvStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("artifactName", out var artifactNameRaw) ||
            artifactNameRaw is null)
            return StepResult.Fail("ExcelToCsvStepHandler: 'artifactName' is required.");

        var artifactName = context.Interpolate(artifactNameRaw.ToString()!);
        if (!context.HasArtifact(artifactName))
            return StepResult.Fail(
                $"ExcelToCsvStepHandler: artifact '{artifactName}' not found in context.");

        var sourceArtifact = context.GetArtifact(artifactName);
        var sheetName      = config.GetValueOrDefault("sheetName")?.ToString() ?? "Sheet1";
        var outputVar      = config.GetValueOrDefault("outputVariable")?.ToString() ?? "csvText";
        var saveAsArtifact = string.Equals(
            config.GetValueOrDefault("saveAsArtifact")?.ToString(), "true",
            StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "ExcelToCsvStepHandler: converting sheet '{Sheet}' from '{Name}' to CSV",
            sheetName, artifactName);

        var excel  = context.GetCapability<IExcelCapability>();
        var csvText = await excel.ToCsvAsync(sourceArtifact, sheetName, cancellationToken);

        var lineCount = csvText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        var outputVars = new Dictionary<string, object>
        {
            [outputVar]               = csvText,
            [$"{outputVar}_lineCount"] = lineCount
        };

        // Optionally persist the CSV text as an artifact
        if (!saveAsArtifact)
            return StepResult.Ok(output: outputVars,
                outputData: $"Converted sheet '{sheetName}' → {lineCount} CSV lines");

        var csvArtifactName = config.GetValueOrDefault("outputArtifactName")?.ToString()
            ?? Path.GetFileNameWithoutExtension(sourceArtifact.Name) + ".csv";
        csvArtifactName = context.Interpolate(csvArtifactName);

        var id    = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes(csvText);
        using var ms = new MemoryStream(bytes);
        var path  = await _store.SaveAsync(id, csvArtifactName, ms, cancellationToken);

        var csvArtifact = new ArtifactReference
        {
            Id          = id,
            Name        = csvArtifactName,
            ContentType = "text/csv",
            StoragePath = path,
            SizeBytes   = bytes.Length,
            Metadata    = new Dictionary<string, string>
            {
                ["operation"]        = "toCsv",
                ["sheetName"]        = sheetName,
                ["lineCount"]        = lineCount.ToString(),
                ["sourceArtifactId"] = sourceArtifact.Id.ToString()
            }
        };

        _logger.LogInformation(
            "ExcelToCsvStepHandler: saved CSV artifact '{Name}' ({Bytes:N0} bytes)",
            csvArtifactName, bytes.Length);

        return StepResult.Ok(
            output: outputVars,
            artifacts: new List<ArtifactReference> { csvArtifact },
            outputData: $"Exported sheet '{sheetName}' → '{csvArtifactName}' ({lineCount} lines)");
    }
}
