using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Job;
using Company.Orchestrator.Application.DTOs.ProcessInstance;

namespace Company.Orchestrator.Application.Services;

public interface IProcessInstanceService
{
    Task<PagedResult<ProcessInstanceDto>> GetAllAsync(int page, int pageSize, Guid? definitionId = null, CancellationToken cancellationToken = default);
    Task<ProcessInstanceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcessInstanceDto> StartAsync(StartProcessRequest request, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(Guid instanceId, CancellationToken cancellationToken = default);
    /// <summary>Returns all job log entries for every job that belongs to this process instance, ordered by creation time.</summary>
    Task<IReadOnlyList<JobLogDto>> GetLogsAsync(Guid instanceId, CancellationToken cancellationToken = default);
}
