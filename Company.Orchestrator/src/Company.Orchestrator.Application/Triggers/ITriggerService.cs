using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Trigger;

namespace Company.Orchestrator.Application.Triggers;

public interface ITriggerService
{
    Task<PagedResult<TriggerDto>> GetAllAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<TriggerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TriggerDto> CreateAsync(
        CreateTriggerRequest request, CancellationToken cancellationToken = default);

    Task<TriggerDto?> UpdateAsync(
        Guid id, UpdateTriggerRequest request, CancellationToken cancellationToken = default);

    Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<TriggerEventDto>> GetEventsAsync(
        Guid triggerId, int page, int pageSize, CancellationToken cancellationToken = default);
}
