using Company.Orchestrator.Application.DTOs.Worker;

namespace Company.Orchestrator.Application.Services;

public interface IWorkerService
{
    Task<IReadOnlyList<WorkerListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WorkerDetailDto?> GetByWorkerIdAsync(string workerId, CancellationToken cancellationToken = default);
    Task<WorkerSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
