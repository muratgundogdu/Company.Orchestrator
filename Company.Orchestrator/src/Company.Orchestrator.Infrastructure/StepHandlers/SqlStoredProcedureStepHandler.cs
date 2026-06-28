using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Executes a SQL Server stored procedure with input/output parameters and optional result sets.
/// </summary>
public sealed class SqlStoredProcedureStepHandler : IStepHandler
{
    private readonly IConfiguration _configuration;
    private readonly ICredentialResolver _credentialResolver;
    private readonly ILogger<SqlStoredProcedureStepHandler> _logger;

    public string HandlerType => "sql.stored-procedure";

    public SqlStoredProcedureStepHandler(
        IConfiguration configuration,
        ICredentialResolver credentialResolver,
        ILogger<SqlStoredProcedureStepHandler> logger)
    {
        _configuration      = configuration;
        _credentialResolver = credentialResolver;
        _logger             = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "sql.stored-procedure";
        var config = context.StepDefinition.Config;

        var procedureName  = SqlStepHandlerHelpers.GetString(config, "procedureName").Trim();
        var outputVariable = SqlStepHandlerHelpers.GetString(config, "outputVariable").Trim();
        var timeoutSeconds = SqlStepHandlerHelpers.GetInt(config, "timeoutSeconds", defaultValue: 300);

        if (string.IsNullOrWhiteSpace(procedureName))
            return StepResult.Fail($"{stepType}: 'procedureName' is required.");
        if (string.IsNullOrWhiteSpace(outputVariable))
            return StepResult.Fail($"{stepType}: 'outputVariable' is required.");
        if (timeoutSeconds <= 0)
            return StepResult.Fail($"{stepType}: 'timeoutSeconds' must be greater than 0.");

        var (connection, connectionFailure) = await SqlStepHandlerHelpers.ResolveConnectionAsync(
            config, context, _configuration, _credentialResolver, stepType, cancellationToken);
        if (connectionFailure is not null)
            return connectionFailure;

        var parameterSpecs = SqlStepHandlerHelpers.ParseParametersArray(config, "parameters", context);
        var hasReturnValueParam = parameterSpecs.Any(p =>
            p.Direction.Equals("ReturnValue", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation(
            "{StepType}: connection={Connection}, procedure={Procedure}, timeout={TimeoutSeconds}s, parameters=[{Parameters}]",
            stepType,
            connection!.Label,
            procedureName,
            timeoutSeconds,
            string.Join(", ", parameterSpecs.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)));

        var stopwatch = Stopwatch.StartNew();
        List<Dictionary<string, string>> resultRows;
        List<string> columns;
        var totalRowsAffected = 0;

        try
        {
            await using var sqlConnection = new SqlConnection(connection.ConnectionString);
            await sqlConnection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(procedureName, sqlConnection)
            {
                CommandType    = CommandType.StoredProcedure,
                CommandTimeout = timeoutSeconds,
            };

            if (!hasReturnValueParam)
            {
                command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int)
                {
                    Direction = ParameterDirection.ReturnValue,
                });
            }

            foreach (var spec in parameterSpecs)
            {
                var direction = SqlStepHandlerHelpers.ParseParameterDirection(spec.Direction);
                if (direction == ParameterDirection.ReturnValue
                    && !spec.Name.Equals("RETURN_VALUE", StringComparison.OrdinalIgnoreCase)
                    && !spec.Name.Equals("@RETURN_VALUE", StringComparison.OrdinalIgnoreCase))
                {
                    command.Parameters.Add(SqlStepHandlerHelpers.CreateProcedureParameter(spec));
                    continue;
                }

                if (direction == ParameterDirection.ReturnValue)
                    continue;

                command.Parameters.Add(SqlStepHandlerHelpers.CreateProcedureParameter(spec));
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            columns    = SqlStepHandlerHelpers.ReadColumnNames(reader);
            resultRows = SqlStepHandlerHelpers.ReadResultSet(reader);
            totalRowsAffected += reader.RecordsAffected;

            while (await reader.NextResultAsync(cancellationToken))
            {
                totalRowsAffected += reader.RecordsAffected;
                if (resultRows.Count == 0)
                {
                    resultRows = SqlStepHandlerHelpers.ReadResultSet(reader);
                    columns    = SqlStepHandlerHelpers.ReadColumnNames(reader);
                }
                else
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        // Skip additional result sets after the first captured set.
                    }
                }
            }

            stopwatch.Stop();

            var output = BuildProcedureOutput(
                outputVariable,
                resultRows,
                columns,
                command,
                parameterSpecs,
                totalRowsAffected,
                stopwatch.ElapsedMilliseconds);

            _logger.LogInformation(
                "{StepType}: completed on {Connection}, procedure={Procedure}, resultRows={ResultRows}, rowsAffected={RowsAffected}, duration={DurationMs}ms",
                stepType,
                connection.Label,
                procedureName,
                resultRows.Count,
                totalRowsAffected,
                stopwatch.ElapsedMilliseconds);

            return StepResult.Ok(
                output: output,
                outputData:
                    $"Stored procedure '{procedureName}' completed — {resultRows.Count} result row(s), {totalRowsAffected} row(s) affected in {stopwatch.ElapsedMilliseconds}ms.");
        }
        catch (SqlException ex) when (SqlStepHandlerHelpers.IsSqlTimeout(ex))
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{StepType}: timeout after {DurationMs}ms on {Connection}, procedure={Procedure}",
                stepType,
                stopwatch.ElapsedMilliseconds,
                connection!.Label,
                procedureName);
            return StepResult.Fail($"{stepType}: stored procedure timed out after {timeoutSeconds} second(s).");
        }
        catch (SqlException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{StepType}: SQL error after {DurationMs}ms on {Connection}, procedure={Procedure}",
                stepType,
                stopwatch.ElapsedMilliseconds,
                connection!.Label,
                procedureName);
            return StepResult.Fail($"{stepType}: SQL error: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{StepType}: execution failed after {DurationMs}ms on {Connection}, procedure={Procedure}",
                stepType,
                stopwatch.ElapsedMilliseconds,
                connection!.Label,
                procedureName);
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }
    }

    private static Dictionary<string, object> BuildProcedureOutput(
        string outputVar,
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyList<string> columns,
        SqlCommand command,
        IReadOnlyList<SqlParameterSpec> parameterSpecs,
        int rowsAffected,
        long durationMs)
    {
        var json        = JsonSerializer.Serialize(rows);
        var columnsJson = JsonSerializer.Serialize(columns);
        var firstJson   = rows.Count > 0 ? JsonSerializer.Serialize(rows[0]) : "{}";

        var output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            [outputVar]                      = json,
            [$"{outputVar}_count"]           = rows.Count,
            [$"{outputVar}_columns"]         = columnsJson,
            [$"{outputVar}_first"]           = firstJson,
            [$"{outputVar}_resultCount"]     = rows.Count,
            [$"{outputVar}_rowsAffected"]    = rowsAffected,
            [$"{outputVar}_executionDurationMs"] = durationMs,
        };

        for (var i = 0; i < Math.Min(rows.Count, 10); i++)
            output[$"{outputVar}_{i}"] = JsonSerializer.Serialize(rows[i]);

        var returnParam = command.Parameters.Cast<SqlParameter>()
            .FirstOrDefault(p => p.Direction == ParameterDirection.ReturnValue);
        if (returnParam?.Value is not null and not DBNull)
            output[$"{outputVar}_returnValue"] = SqlStepHandlerHelpers.ConvertDbValue(returnParam.Value);

        foreach (var spec in parameterSpecs)
        {
            var direction = SqlStepHandlerHelpers.ParseParameterDirection(spec.Direction);
            if (direction is not (ParameterDirection.Output or ParameterDirection.InputOutput))
                continue;

            var paramName = spec.Name.StartsWith('@') ? spec.Name : $"@{spec.Name}";
            var parameter = command.Parameters.Cast<SqlParameter>()
                .FirstOrDefault(p => p.ParameterName.Equals(paramName, StringComparison.OrdinalIgnoreCase));

            if (parameter?.Value is null or DBNull)
                output[$"{outputVar}_{spec.Name.TrimStart('@')}"] = string.Empty;
            else
                output[$"{outputVar}_{spec.Name.TrimStart('@')}"] = SqlStepHandlerHelpers.ConvertDbValue(parameter.Value);
        }

        return output;
    }
}
