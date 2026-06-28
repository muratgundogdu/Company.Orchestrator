using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Artifacts;

public sealed class ArtifactRepository : IArtifactRepository
{
    private readonly OrchestratorDbContext _context;

    public ArtifactRepository(OrchestratorDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Artifact> Items, int TotalCount)> GetAllAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Artifacts.OrderByDescending(a => a.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Artifact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Artifacts.FindAsync(new object[] { id }, cancellationToken).AsTask();

    public async Task<IReadOnlyList<Artifact>> GetByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default)
        => await _context.Artifacts
            .Where(a => a.ProcessInstanceId == processInstanceId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Artifact>> GetByStepInstanceAsync(Guid stepInstanceId, CancellationToken cancellationToken = default)
        => await _context.Artifacts
            .Where(a => a.StepInstanceId == stepInstanceId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Artifact artifact, CancellationToken cancellationToken = default)
        => await _context.Artifacts.AddAsync(artifact, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Artifact> artifacts, CancellationToken cancellationToken = default)
        => await _context.Artifacts.AddRangeAsync(artifacts, cancellationToken);

    public async Task<IReadOnlyList<Artifact>> GetEligibleForCleanupAsync(DateTime olderThan, CancellationToken cancellationToken = default)
        => await _context.Artifacts
            .Where(a => !a.IsPersistent && a.CreatedAt < olderThan)
            .ToListAsync(cancellationToken);

    public Task DeleteAsync(Artifact artifact, CancellationToken cancellationToken = default)
    {
        _context.Artifacts.Remove(artifact);
        return Task.CompletedTask;
    }
}
