using Company.Orchestrator.Application.DTOs.Worker;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Company.Orchestrator.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Services;

public sealed class WorkerService : IWorkerService
{
    private readonly OrchestratorDbContext _context;

    public WorkerService(OrchestratorDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WorkerListItemDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var workers = await _context.WorkerHeartbeats
            .OrderBy(w => w.WorkerName)
            .ThenBy(w => w.WorkerId)
            .ToListAsync(cancellationToken);

        return workers.Select(w => MapListItem(w, now)).ToList();
    }

    public async Task<WorkerDetailDto?> GetByWorkerIdAsync(
        string workerId,
        CancellationToken cancellationToken = default)
    {
        var worker = await _context.WorkerHeartbeats
            .FirstOrDefaultAsync(w => w.WorkerId == workerId, cancellationToken);

        return worker is null ? null : MapDetail(worker, DateTime.UtcNow);
    }

    public async Task<WorkerSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var workers = await _context.WorkerHeartbeats.ToListAsync(cancellationToken);

        var summary = new WorkerSummaryDto
        {
            Total       = workers.Count,
            RunningJobs = workers.Sum(w => w.RunningJobCount),
        };

        foreach (var worker in workers)
        {
            switch (WorkerStatusCalculator.Calculate(worker.LastHeartbeatUtc, now))
            {
                case WorkerStatus.Online:
                    summary.Online++;
                    break;
                case WorkerStatus.Warning:
                    summary.Warning++;
                    break;
                case WorkerStatus.Offline:
                    summary.Offline++;
                    break;
            }
        }

        return summary;
    }

    private static WorkerListItemDto MapListItem(Domain.Entities.WorkerHeartbeat worker, DateTime now)
    {
        var status = WorkerStatusCalculator.Calculate(worker.LastHeartbeatUtc, now);
        return new WorkerListItemDto
        {
            WorkerId          = worker.WorkerId,
            WorkerName        = worker.WorkerName,
            MachineName       = worker.MachineName,
            Version           = worker.Version,
            Status            = WorkerStatusCalculator.ToDisplayName(status),
            LastHeartbeatUtc  = worker.LastHeartbeatUtc,
            RunningJobCount   = worker.RunningJobCount,
            CpuUsagePercent   = worker.CpuUsagePercent,
            MemoryUsageMb     = worker.MemoryUsageMb,
        };
    }

    private static WorkerDetailDto MapDetail(Domain.Entities.WorkerHeartbeat worker, DateTime now)
    {
        var list = MapListItem(worker, now);
        return new WorkerDetailDto
        {
            WorkerId          = list.WorkerId,
            WorkerName        = list.WorkerName,
            MachineName       = list.MachineName,
            Version           = list.Version,
            Status            = list.Status,
            LastHeartbeatUtc  = list.LastHeartbeatUtc,
            RunningJobCount   = list.RunningJobCount,
            CpuUsagePercent   = list.CpuUsagePercent,
            MemoryUsageMb     = list.MemoryUsageMb,
            StartedAtUtc      = worker.StartedAtUtc,
            ProcessId         = worker.ProcessId,
            MetadataJson      = worker.MetadataJson,
            CreatedAtUtc      = worker.CreatedAt,
            UpdatedAtUtc      = worker.UpdatedAt,
        };
    }
}
