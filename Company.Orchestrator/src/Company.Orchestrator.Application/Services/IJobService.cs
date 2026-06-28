using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Job;

namespace Company.Orchestrator.Application.Services;

public interface IJobService
{
    Task<PagedResult<JobDto>> GetAllAsync(int page, int pageSize, Guid? instanceId = null, CancellationToken cancellationToken = default);
    Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<CancelJobResponse?> CancelAsync(
        Guid jobId,
        CancelJobRequest request,
        CancellationToken cancellationToken = default);
}
