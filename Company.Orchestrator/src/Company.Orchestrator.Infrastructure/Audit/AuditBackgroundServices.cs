using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Company.Orchestrator.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Audit;

public sealed class AuditRetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuditRetentionBackgroundService> _logger;

    public AuditRetentionBackgroundService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<AuditRetentionBackgroundService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit retention cleanup failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var retentionDays = int.TryParse(_configuration["Audit:RetentionDays"], out var days) ? days : 365;
        if (retentionDays <= 0) return;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();

        var deleted = await context.AuditLogs
            .Where(a => a.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
            _logger.LogInformation("Deleted {Count} audit records older than {Cutoff:u}", deleted, cutoff);
    }
}

public sealed class WorkerStatusAuditBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _services;
    private readonly ILogger<WorkerStatusAuditBackgroundService> _logger;
    private readonly Dictionary<string, WorkerStatus> _lastStatus = new(StringComparer.OrdinalIgnoreCase);

    public WorkerStatusAuditBackgroundService(
        IServiceProvider services,
        ILogger<WorkerStatusAuditBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckWorkersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker status audit check failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CheckWorkersAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var now = DateTime.UtcNow;
        var workers = await context.WorkerHeartbeats.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var worker in workers)
        {
            var status = Workers.WorkerStatusCalculator.Calculate(worker.LastHeartbeatUtc, now);
            var hadPrevious = _lastStatus.TryGetValue(worker.WorkerId, out var previous);

            if (!hadPrevious)
            {
                _lastStatus[worker.WorkerId] = status;
                await audit.WriteSuccessAsync(new AuditWriteRequest
                {
                    EventType  = AuditEventTypes.WorkerRegistered,
                    Category   = AuditCategories.Worker,
                    EntityType = "Worker",
                    EntityId   = worker.WorkerId,
                    EntityName = worker.WorkerName,
                    Action     = "Worker registered",
                    Details    = new { worker.MachineName, worker.Version },
                }, cancellationToken);
                continue;
            }

            if (previous == status) continue;

            _lastStatus[worker.WorkerId] = status;

            if (status == WorkerStatus.Online && previous == WorkerStatus.Offline)
            {
                await audit.WriteSuccessAsync(new AuditWriteRequest
                {
                    EventType  = AuditEventTypes.WorkerOnline,
                    Category   = AuditCategories.Worker,
                    Severity   = AuditSeverity.Info,
                    EntityType = "Worker",
                    EntityId   = worker.WorkerId,
                    EntityName = worker.WorkerName,
                    Action     = "Worker came online",
                }, cancellationToken);
            }
            else if (status == WorkerStatus.Offline &&
                     previous != WorkerStatus.Offline)
            {
                await audit.WriteSuccessAsync(new AuditWriteRequest
                {
                    EventType  = AuditEventTypes.WorkerOffline,
                    Category   = AuditCategories.Worker,
                    Severity   = AuditSeverity.Warning,
                    EntityType = "Worker",
                    EntityId   = worker.WorkerId,
                    EntityName = worker.WorkerName,
                    Action     = "Worker went offline",
                }, cancellationToken);
            }
        }
    }
}
