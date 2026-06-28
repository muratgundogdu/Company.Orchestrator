using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Application.Common.Interfaces;

public interface IJobRepository : IRepository<Job>
{
    Task<IReadOnlyList<Job>> GetPendingJobsAsync(int batchSize, CancellationToken cancellationToken = default);
    Task<Job?> GetWithLogsAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLockAsync(Guid jobId, string workerInstanceId, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(Guid jobId, CancellationToken cancellationToken = default);
}
