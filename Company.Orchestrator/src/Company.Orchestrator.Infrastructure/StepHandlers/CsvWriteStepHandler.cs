using System.Globalization;
using System.Text;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Writes a DataTable JSON workflow variable to a CSV artifact.
/// </summary>
public sealed class CsvWriteStepHandler : IStepHandler
{
    private const string CsvMime = "text/csv";

    private readonly IArtifactStore _store;
    private readonly ILogger<CsvWriteStepHandler> _logger;

    public string HandlerType => "csv.write";

    public CsvWriteStepHandler(IArtifactStore store, ILogger<CsvWriteStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var sourceVariable = CsvStepHandlerHelpers.NormalizeVarName(
            CsvStepHandlerHelpers.GetString(config, "sourceVariable"));
        if (string.IsNullOrWhiteSpace(sourceVariable))
            return StepResult.Fail("csv.write: 'sourceVariable' is required.");

        var outputName = context.Interpolate(CsvStepHandlerHelpers.GetString(config, "outputName"));
        if (string.IsNullOrWhiteSpace(outputName))
            return StepResult.Fail("csv.write: 'outputName' is required.");

        string delimiter;
        try
        {
            delimiter = CsvStepHandlerHelpers.ResolveDelimiter(config);
            CsvStepHandlerHelpers.ValidateDelimiter(delimiter, "csv.write");
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        Encoding encoding;
        try
        {
            encoding = CsvStepHandlerHelpers.ResolveEncoding(
                CsvStepHandlerHelpers.GetString(config, "encoding", "UTF-8"),
                "csv.write");
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        var includeHeaders = CsvStepHandlerHelpers.GetBool(config, "includeHeaders", defaultValue: true);
        var quoteValues    = CsvStepHandlerHelpers.GetBool(config, "quoteValues", defaultValue: true);

        if (!context.Variables.TryGetValue(sourceVariable, out var sourceRaw))
        {
            return StepResult.Fail(
                $"csv.write: source variable '{sourceVariable}' not found in workflow context.");
        }

        List<Dictionary<string, string>> rows;
        List<string> firstRowColumnOrder;
        try
        {
            rows = CsvStepHandlerHelpers.ParseDataTableRows(sourceRaw, out firstRowColumnOrder);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"csv.write: {ex.Message}");
        }

        var columns = CsvStepHandlerHelpers.BuildColumns(firstRowColumnOrder, rows);

        string csvText;
        try
        {
            csvText = WriteCsvText(columns, rows, delimiter, includeHeaders, quoteValues);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"csv.write: failed to serialize CSV: {ex.Message}");
        }

        var id    = Guid.NewGuid();
        var bytes = encoding.GetBytes(csvText);
        using var ms = new MemoryStream(bytes);
        var path = await _store.SaveAsync(id, outputName, ms, cancellationToken);

        var artifact = new ArtifactReference
        {
            Id          = id,
            Name        = outputName,
            ContentType = CsvMime,
            StoragePath = path,
            SizeBytes   = bytes.Length,
            Metadata    = new Dictionary<string, string>
            {
                ["operation"]      = "csv.write",
                ["sourceVariable"] = sourceVariable,
                ["rowCount"]       = rows.Count.ToString(CultureInfo.InvariantCulture),
                ["columnCount"]    = columns.Count.ToString(CultureInfo.InvariantCulture),
                ["delimiter"]      = delimiter,
                ["encoding"]       = encoding.WebName,
            }
        };

        _logger.LogInformation(
            "csv.write: sourceVariable='{SourceVar}', outputName='{OutputName}', rowsWritten={RowsWritten}, columnsWritten={ColumnsWritten}",
            sourceVariable,
            outputName,
            rows.Count,
            columns.Count);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["outputName"]      = outputName,
                ["rowsWritten"]     = rows.Count,
                ["columnsWritten"]  = columns.Count,
            },
            artifacts: [artifact],
            outputData:
                $"Wrote {rows.Count} row(s) and {columns.Count} column(s) to CSV artifact '{outputName}'.");
    }

    private static string WriteCsvText(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, string>> rows,
        string delimiter,
        bool includeHeaders,
        bool quoteValues)
    {
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter       = delimiter,
            HasHeaderRecord = includeHeaders,
            ShouldQuote     = quoteValues
                ? args => ShouldQuoteField(args.Field, delimiter)
                : _ => false,
        };

        using var writer = new StringWriter();
        using var csv    = new CsvWriter(writer, csvConfig);

        if (includeHeaders)
        {
            foreach (var column in columns)
                csv.WriteField(column);
            csv.NextRecord();
        }

        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                row.TryGetValue(column, out var value);
                csv.WriteField(value ?? string.Empty);
            }

            csv.NextRecord();
        }

        csv.Flush();
        return writer.ToString();
    }

    private static bool ShouldQuoteField(string? field, string delimiter)
    {
        if (string.IsNullOrEmpty(field))
            return false;

        return field.Contains('"')
            || field.Contains('\n')
            || field.Contains('\r')
            || field.Contains(delimiter[0]);
    }
}
