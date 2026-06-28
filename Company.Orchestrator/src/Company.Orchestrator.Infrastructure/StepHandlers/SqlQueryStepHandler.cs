using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Executes a SQL query against a named connection string.
/// Config: { "connectionName": "ReportingDb", "query": "SELECT ...", "outputVariable": "result" }
/// IMPORTANT: Use SqlParameters for user-supplied values to avoid SQL injection.
/// </summary>
public class SqlQueryStepHandler : IStepHandler
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqlQueryStepHandler> _logger;

    public string HandlerType => "SqlQuery";

    public SqlQueryStepHandler(IConfiguration configuration, ILogger<SqlQueryStepHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var connectionName  = config.GetValueOrDefault("connectionName")?.ToString() ?? "Default";
        var query           = config.GetValueOrDefault("query")?.ToString() ?? "";
        var outputVariable  = config.GetValueOrDefault("outputVariable")?.ToString() ?? "sqlResult";

        if (string.IsNullOrWhiteSpace(query))
            return StepResult.Fail("SqlQuery 'query' is required.");

        var connectionString = _configuration.GetConnectionString(connectionName);
        if (string.IsNullOrEmpty(connectionString))
            return StepResult.Fail($"Connection string '{connectionName}' not found.");

        query = context.Interpolate(query);

        _logger.LogInformation("Executing SQL query on {ConnectionName}", connectionName);

        var rows = new List<Dictionary<string, object?>>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader  = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        _logger.LogInformation("SQL query returned {RowCount} rows", rows.Count);

        var outputData = JsonSerializer.Serialize(rows);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                [outputVariable] = rows,
                ["rowCount"]     = rows.Count
            },
            outputData: outputData);
    }
}
