using System.Diagnostics;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Worker.Workers;

/// <summary>
/// Sends a heartbeat every 30 seconds so administrators can monitor worker health.
/// </summary>
public sealed class WorkerHeartbeatBackgroundService : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkerIdentityProvider _identity;
    private readonly ILogger<WorkerHeartbeatBackgroundService> _logger;
    private readonly Process _process;

    public WorkerHeartbeatBackgroundService(
        IServiceProvider serviceProvider,
        IWorkerIdentityProvider identity,
        ILogger<WorkerHeartbeatBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _identity = identity;
        _logger = logger;
        _process = Process.GetCurrentProcess();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "WorkerHeartbeatBackgroundService started for {WorkerId} ({WorkerName})",
            _identity.WorkerId, _identity.WorkerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send worker heartbeat for {WorkerId}", _identity.WorkerId);
            }

            await Task.Delay(HeartbeatInterval, stoppingToken);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var writer  = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatWriter>();
        var context = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();

        var runningJobs = await context.Jobs.CountAsync(
            j => j.WorkerInstanceId == _identity.WorkerId && j.Status == JobStatus.Running,
            cancellationToken);

        _process.Refresh();
        var memoryMb = Math.Round(_process.WorkingSet64 / (1024d * 1024d), 1);

        await writer.RecordHeartbeatAsync(new WorkerHeartbeatPayload
        {
            WorkerId         = _identity.WorkerId,
            WorkerName         = _identity.WorkerName,
            MachineName        = Environment.MachineName,
            Version            = _identity.Version,
            ProcessId          = _process.Id,
            RunningJobCount    = runningJobs,
            CpuUsagePercent    = null,
            MemoryUsageMb      = memoryMb,
        }, cancellationToken);
    }
}
