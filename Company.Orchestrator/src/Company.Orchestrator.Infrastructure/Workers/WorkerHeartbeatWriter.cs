using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Workers;

public sealed class WorkerHeartbeatWriter : IWorkerHeartbeatWriter
{
    private readonly OrchestratorDbContext _context;
    private readonly ILogger<WorkerHeartbeatWriter> _logger;

    public WorkerHeartbeatWriter(OrchestratorDbContext context, ILogger<WorkerHeartbeatWriter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordHeartbeatAsync(
        WorkerHeartbeatPayload payload,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var existing = await _context.WorkerHeartbeats
            .FirstOrDefaultAsync(w => w.WorkerId == payload.WorkerId, cancellationToken);

        if (existing is null)
        {
            existing = new WorkerHeartbeat
            {
                WorkerId         = payload.WorkerId,
                WorkerName       = payload.WorkerName,
                MachineName      = payload.MachineName,
                Version          = payload.Version,
                StartedAtUtc     = now,
                LastHeartbeatUtc = now,
                Status           = WorkerStatus.Online,
                ProcessId        = payload.ProcessId,
                CreatedAt          = now,
            };

            _context.WorkerHeartbeats.Add(existing);
            _logger.LogInformation(
                "Worker registered: {WorkerId} ({WorkerName}) on {MachineName}",
                payload.WorkerId, payload.WorkerName, payload.MachineName);
        }
        else
        {
            var previousStatus = WorkerStatusCalculator.Calculate(existing.LastHeartbeatUtc, now);
            if (previousStatus != WorkerStatus.Online)
            {
                _logger.LogInformation(
                    "Worker status changed: {WorkerId} {PreviousStatus} → Online",
                    payload.WorkerId,
                    WorkerStatusCalculator.ToDisplayName(previousStatus));
            }
        }

        existing.WorkerName       = payload.WorkerName;
        existing.MachineName      = payload.MachineName;
        existing.Version          = payload.Version;
        existing.LastHeartbeatUtc = now;
        existing.RunningJobCount  = payload.RunningJobCount;
        existing.CpuUsagePercent  = payload.CpuUsagePercent;
        existing.MemoryUsageMb    = payload.MemoryUsageMb;
        existing.ProcessId        = payload.ProcessId;
        existing.MetadataJson     = payload.MetadataJson;
        existing.Status           = WorkerStatus.Online;
        existing.UpdatedAt        = now;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Heartbeat sent: {WorkerId}, runningJobs={RunningJobCount}",
            payload.WorkerId, payload.RunningJobCount);
    }

    public async Task<int> CleanupStaleAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var stale = await _context.WorkerHeartbeats
            .Where(w => w.LastHeartbeatUtc < cutoff)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
            return 0;

        _context.WorkerHeartbeats.RemoveRange(stale);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var worker in stale)
        {
            _logger.LogInformation(
                "Offline worker record removed: {WorkerId} (last seen {LastHeartbeatUtc:u})",
                worker.WorkerId, worker.LastHeartbeatUtc);
        }

        return stale.Count;
    }
}
