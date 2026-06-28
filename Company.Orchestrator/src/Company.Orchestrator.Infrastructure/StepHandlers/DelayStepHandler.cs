using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

public class DelayStepHandler : IStepHandler
{
    private readonly ILogger<DelayStepHandler> _logger;

    public string HandlerType => "Delay";

    public DelayStepHandler(ILogger<DelayStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var seconds = 1;
        if (config.TryGetValue("seconds", out var secondsRaw))
            int.TryParse(secondsRaw?.ToString(), out seconds);
        if (seconds <= 0) seconds = 1;

        _logger.LogInformation("Delaying for {Seconds} seconds", seconds);
        await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
        _logger.LogInformation("Delay of {Seconds} seconds completed", seconds);

        return StepResult.Ok(
            output: new Dictionary<string, object> { ["delayedSeconds"] = seconds },
            outputData: $"Delayed {seconds}s");
    }
}
