using System.Globalization;
using System.Text;
using System.Text.Json;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using CsvHelper;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Reads a CSV artifact into a DataTable-style JSON workflow variable.
/// </summary>
public sealed class CsvReadStepHandler : IStepHandler
{
    private readonly IArtifactStore _store;
    private readonly ILogger<CsvReadStepHandler> _logger;

    public string HandlerType => "csv.read";

    public CsvReadStepHandler(IArtifactStore store, ILogger<CsvReadStepHandler> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var inputArtifactName = context.Interpolate(CsvStepHandlerHelpers.GetString(config, "inputArtifactName"));
        if (string.IsNullOrWhiteSpace(inputArtifactName))
            return StepResult.Fail("csv.read: 'inputArtifactName' is required.");

        var outputVar = CsvStepHandlerHelpers.GetString(config, "outputVariable").Trim();
        if (string.IsNullOrEmpty(outputVar))
            return StepResult.Fail("csv.read: 'outputVariable' is required.");

        if (!context.HasArtifact(inputArtifactName))
            return StepResult.Fail($"csv.read: input artifact '{inputArtifactName}' not found in context.");

        string delimiter;
        try
        {
            delimiter = CsvStepHandlerHelpers.ResolveDelimiter(config);
            CsvStepHandlerHelpers.ValidateDelimiter(delimiter, "csv.read");
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
                "csv.read");
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }

        var hasHeader        = CsvStepHandlerHelpers.GetBool(config, "hasHeader", defaultValue: true);
        var trimValues       = CsvStepHandlerHelpers.GetBool(config, "trimValues", defaultValue: true);
        var includeEmptyRows = CsvStepHandlerHelpers.GetBool(config, "includeEmptyRows", defaultValue: false);

        var inputArtifact = context.GetArtifact(inputArtifactName);
        byte[] bytes;
        try
        {
            bytes = await _store.ReadAllBytesAsync(inputArtifact.StoragePath, cancellationToken);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"csv.read: failed to read artifact '{inputArtifactName}': {ex.Message}");
        }

        List<Dictionary<string, string>> rows;
        string[] headers;
        try
        {
            (rows, headers) = ParseCsv(bytes, encoding, delimiter, hasHeader, trimValues, includeEmptyRows);
        }
        catch (InvalidOperationException ex)
        {
            return StepResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return StepResult.Fail($"csv.read: invalid CSV in artifact '{inputArtifactName}': {ex.Message}");
        }

        _logger.LogInformation(
            "csv.read: artifact='{Artifact}', delimiter='{Delimiter}', rows={RowCount}, columns={ColumnCount}, outputVariable='{OutputVar}'",
            inputArtifactName,
            EscapeDelimiterForLog(delimiter),
            rows.Count,
            headers.Length,
            outputVar);

        return BuildResult(outputVar, rows, headers);
    }

    private static (List<Dictionary<string, string>> Rows, string[] Headers) ParseCsv(
        byte[] bytes,
        Encoding encoding,
        string delimiter,
        bool hasHeader,
        bool trimValues,
        bool includeEmptyRows)
    {
        var csvConfig = CsvStepHandlerHelpers.BuildCsvConfiguration(delimiter, trimValues);
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        using var csv    = new CsvReader(reader, csvConfig);

        var rows    = new List<Dictionary<string, string>>();
        string[] headers;

        if (hasHeader)
        {
            if (!csv.Read())
            {
                return (rows, []);
            }

            csv.ReadHeader();
            var rawHeaders = csv.HeaderRecord ?? [];
            headers = CsvStepHandlerHelpers.BuildHeaderNames(rawHeaders);

            while (csv.Read())
            {
                var row = ReadRecord(csv, headers, trimValues);
                if (!includeEmptyRows && CsvStepHandlerHelpers.IsEmptyRow(row))
                    continue;

                rows.Add(row);
            }
        }
        else
        {
            headers = [];
            while (csv.Read())
            {
                var fieldCount = csv.Parser.Count;
                if (fieldCount <= 0)
                    continue;

                if (headers.Length == 0)
                {
                    headers = Enumerable.Range(1, fieldCount)
                        .Select(i => $"Column{i}")
                        .ToArray();
                }

                var row = new Dictionary<string, string>(fieldCount, StringComparer.Ordinal);
                for (var i = 0; i < fieldCount; i++)
                {
                    var colName = i < headers.Length ? headers[i] : $"Column{i + 1}";
                    var val     = csv.GetField(i) ?? string.Empty;
                    if (trimValues)
                        val = val.Trim();
                    row[colName] = val;
                }

                if (!includeEmptyRows && CsvStepHandlerHelpers.IsEmptyRow(row))
                    continue;

                rows.Add(row);
            }
        }

        return (rows, headers);
    }

    private static Dictionary<string, string> ReadRecord(
        CsvReader csv,
        IReadOnlyList<string> headers,
        bool trimValues)
    {
        var row = new Dictionary<string, string>(headers.Count, StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            var val = csv.GetField(i) ?? string.Empty;
            if (trimValues)
                val = val.Trim();
            row[headers[i]] = val;
        }

        return row;
    }

    private static StepResult BuildResult(
        string outputVar,
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<string> headers)
    {
        var json        = JsonSerializer.Serialize(rows);
        var columnsJson = JsonSerializer.Serialize(headers);
        var firstJson   = rows.Count > 0 ? JsonSerializer.Serialize(rows[0]) : "{}";

        var output = new Dictionary<string, object>
        {
            [outputVar]              = json,
            [$"{outputVar}_count"]   = rows.Count,
            [$"{outputVar}_columns"] = columnsJson,
            [$"{outputVar}_first"]   = firstJson,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonSerializer.Serialize(rows[i]);

        return StepResult.Ok(
            output: output,
            outputData: $"Read {rows.Count} row(s) and {headers.Count} column(s) into '{outputVar}'.");
    }

    private static string EscapeDelimiterForLog(string delimiter) =>
        delimiter switch
        {
            "\t" => "\\t",
            _    => delimiter,
        };
}
