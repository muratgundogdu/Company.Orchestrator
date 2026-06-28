using System.Diagnostics;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Executes parameterized non-query SQL statements (INSERT, UPDATE, DELETE, MERGE).
/// </summary>
public sealed class SqlExecuteStepHandler : IStepHandler
{
    private readonly IConfiguration _configuration;
    private readonly ICredentialResolver _credentialResolver;
    private readonly ILogger<SqlExecuteStepHandler> _logger;

    public string HandlerType => "sql.execute";

    public SqlExecuteStepHandler(
        IConfiguration configuration,
        ICredentialResolver credentialResolver,
        ILogger<SqlExecuteStepHandler> logger)
    {
        _configuration      = configuration;
        _credentialResolver = credentialResolver;
        _logger             = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        const string stepType = "sql.execute";
        var config = context.StepDefinition.Config;

        var sql            = SqlStepHandlerHelpers.GetString(config, "sql");
        var timeoutSeconds = SqlStepHandlerHelpers.GetInt(config, "timeoutSeconds", defaultValue: 300);

        if (string.IsNullOrWhiteSpace(sql))
            return StepResult.Fail($"{stepType}: 'sql' is required.");
        if (timeoutSeconds <= 0)
            return StepResult.Fail($"{stepType}: 'timeoutSeconds' must be greater than 0.");
        if (!SqlStepHandlerHelpers.IsAllowedExecuteStatement(sql, out var sqlError))
            return StepResult.Fail($"{stepType}: {sqlError}");

        var (connection, connectionFailure) = await SqlStepHandlerHelpers.ResolveConnectionAsync(
            config, context, _configuration, _credentialResolver, stepType, cancellationToken);
        if (connectionFailure is not null)
            return connectionFailure;

        var parameterSpecs = SqlStepHandlerHelpers.ParseParametersArray(config, "parameters", context);
        var parameters     = SqlStepHandlerHelpers.ParametersArrayToDictionary(parameterSpecs);
        if (parameterSpecs.Count == 0 && config.ContainsKey("parameters"))
        {
            foreach (var (name, value) in SqlStepHandlerHelpers.ParseParametersObject(config, "parameters", context))
                parameters[name] = value;
        }

        var requiredParams = SqlStepHandlerHelpers.ExtractParameterNames(sql);
        foreach (var paramName in requiredParams)
        {
            if (!parameters.ContainsKey(paramName))
            {
                return StepResult.Fail(
                    $"{stepType}: missing parameter '@{paramName}' in parameters config.");
            }
        }

        var statementType = SqlStepHandlerHelpers.DetectStatementType(sql);
        var sqlPreview    = SqlStepHandlerHelpers.BuildSqlPreview(sql, parameters.Keys);

        _logger.LogInformation(
            "{StepType}: connection={Connection}, statementType={StatementType}, timeout={TimeoutSeconds}s, parameters=[{Parameters}], sql={SqlPreview}",
            stepType,
            connection!.Label,
            statementType,
            timeoutSeconds,
            string.Join(", ", parameters.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)),
            sqlPreview);

        var stopwatch = Stopwatch.StartNew();
        int rowsAffected;

        try
        {
            await using var sqlConnection = new SqlConnection(connection.ConnectionString);
            await sqlConnection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, sqlConnection)
            {
                CommandTimeout = timeoutSeconds,
            };

            foreach (var (name, value) in parameters)
                SqlStepHandlerHelpers.AddInputParameter(command, name, value);

            rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (SqlStepHandlerHelpers.IsSqlTimeout(ex))
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{StepType}: timeout after {DurationMs}ms on {Connection}, statementType={StatementType}",
                stepType,
                stopwatch.ElapsedMilliseconds,
                connection!.Label,
                statementType);
            return StepResult.Fail($"{stepType}: statement timed out after {timeoutSeconds} second(s).");
        }
        catch (SqlException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{StepType}: SQL error after {DurationMs}ms on {Connection}, statementType={StatementType}",
                stepType,
                stopwatch.ElapsedMilliseconds,
                connection!.Label,
                statementType);
            return StepResult.Fail($"{stepType}: SQL error: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{StepType}: execution failed after {DurationMs}ms on {Connection}, statementType={StatementType}",
                stepType,
                stopwatch.ElapsedMilliseconds,
                connection!.Label,
                statementType);
            return StepResult.Fail($"{stepType}: {ex.Message}");
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "{StepType}: completed on {Connection}, statementType={StatementType}, rowsAffected={RowsAffected}, duration={DurationMs}ms",
            stepType,
            connection!.Label,
            statementType,
            rowsAffected,
            stopwatch.ElapsedMilliseconds);

        return StepResult.Ok(
            output: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["rowsAffected"]         = rowsAffected,
                ["executionSucceeded"]   = true,
                ["executionDurationMs"]  = stopwatch.ElapsedMilliseconds,
            },
            outputData:
                $"{statementType} affected {rowsAffected} row(s) in {stopwatch.ElapsedMilliseconds}ms.");
    }
}
