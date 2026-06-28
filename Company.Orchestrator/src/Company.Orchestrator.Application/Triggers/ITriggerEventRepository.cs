using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Triggers;

public interface ITriggerEventRepository
{
    Task<TriggerEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an event with this key already exists for the trigger
    /// and has been processed or is currently being processed.
    /// Used for deduplication.
    /// </summary>
    Task<bool> EventKeyExistsAsync(
        Guid triggerId, string eventKey, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<TriggerEvent> Items, int TotalCount)> GetByTriggerIdAsync(
        Guid triggerId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(TriggerEvent triggerEvent, CancellationToken cancellationToken = default);
}
