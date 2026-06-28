using Company.Orchestrator.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Worker.Workers;

/// <summary>
/// Removes worker heartbeat records older than the retention window (default 30 days).
/// </summary>
public sealed class WorkerHeartbeatCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private const int RetentionDays = 30;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkerHeartbeatCleanupBackgroundService> _logger;

    public WorkerHeartbeatCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WorkerHeartbeatCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "WorkerHeartbeatCleanupBackgroundService started (retention={RetentionDays} days)",
            RetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var writer = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatWriter>();
                var removed = await writer.CleanupStaleAsync(RetentionDays, stoppingToken);

                if (removed > 0)
                {
                    _logger.LogInformation(
                        "Worker heartbeat cleanup removed {Count} stale record(s)",
                        removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker heartbeat cleanup failed");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
