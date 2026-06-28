using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Artifacts;

/// <summary>
/// Persistence contract for Artifact metadata.
/// The engine calls this after each step to record produced artifacts.
/// </summary>
public interface IArtifactRepository
{
    Task<(IReadOnlyList<Artifact> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Artifact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Artifact>> GetByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Artifact>> GetByStepInstanceAsync(Guid stepInstanceId, CancellationToken cancellationToken = default);
    Task AddAsync(Artifact artifact, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Artifact> artifacts, CancellationToken cancellationToken = default);

    /// <summary>Returns artifacts eligible for cleanup (non-persistent, process completed).</summary>
    Task<IReadOnlyList<Artifact>> GetEligibleForCleanupAsync(DateTime olderThan, CancellationToken cancellationToken = default);
    Task DeleteAsync(Artifact artifact, CancellationToken cancellationToken = default);
}
