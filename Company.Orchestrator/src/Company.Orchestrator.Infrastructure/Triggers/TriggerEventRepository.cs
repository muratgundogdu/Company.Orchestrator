using Company.Orchestrator.Application.Triggers;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Triggers;

public sealed class TriggerEventRepository : ITriggerEventRepository
{
    private readonly OrchestratorDbContext _context;

    public TriggerEventRepository(OrchestratorDbContext context)
    {
        _context = context;
    }

    public Task<TriggerEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.TriggerEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> EventKeyExistsAsync(
        Guid triggerId, string eventKey, CancellationToken cancellationToken = default)
        => _context.TriggerEvents
            .AnyAsync(e => e.TriggerId == triggerId && e.EventKey == eventKey,
                cancellationToken);

    public async Task<(IReadOnlyList<TriggerEvent> Items, int TotalCount)> GetByTriggerIdAsync(
        Guid triggerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.TriggerEvents
            .Where(e => e.TriggerId == triggerId)
            .OrderByDescending(e => e.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(TriggerEvent triggerEvent, CancellationToken cancellationToken = default)
        => await _context.TriggerEvents.AddAsync(triggerEvent, cancellationToken);
}
