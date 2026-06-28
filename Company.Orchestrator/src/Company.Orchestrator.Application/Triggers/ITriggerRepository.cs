using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Triggers;

public interface ITriggerRepository
{
    Task<(IReadOnlyList<Trigger> Items, int TotalCount)> GetAllAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Trigger?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all active FolderWatcher triggers — polled by FolderWatcherWorker.</summary>
    Task<IReadOnlyList<Trigger>> GetActiveFolderWatchersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Returns all active Scheduled triggers — polled by ScheduledTriggerWorker.</summary>
    Task<IReadOnlyList<Trigger>> GetActiveScheduledTriggersAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(Trigger trigger, CancellationToken cancellationToken = default);
}
