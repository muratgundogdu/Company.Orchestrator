using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Repositories;

public class JobRepository : Repository<Job>, IJobRepository
{
    public JobRepository(OrchestratorDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Job>> GetPendingJobsAsync(int batchSize, CancellationToken cancellationToken = default)
        => await _context.Jobs
            .Where(j => (j.Status == JobStatus.Pending || j.Status == JobStatus.Retrying)
                     && (j.ScheduledAt == null || j.ScheduledAt <= DateTime.UtcNow)
                     && j.LockedAt == null)
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<Job?> GetWithLogsAsync(Guid jobId, CancellationToken cancellationToken = default)
        => await _context.Jobs
            .Include(j => j.Logs)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    public async Task<bool> TryAcquireLockAsync(Guid jobId, string workerInstanceId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var affected = await _context.Jobs
            .Where(j => j.Id == jobId
                     && j.LockedAt == null
                     && (j.Status == JobStatus.Pending || j.Status == JobStatus.Retrying))
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.LockedAt, now)
                .SetProperty(j => j.WorkerInstanceId, workerInstanceId)
                .SetProperty(j => j.Status, JobStatus.Running)
                .SetProperty(j => j.StartedAt, now),
                cancellationToken);

        return affected > 0;
    }

    public async Task ReleaseLockAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _context.Jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.LockedAt, (DateTime?)null)
                .SetProperty(j => j.WorkerInstanceId, (string?)null),
                cancellationToken);
    }
}
