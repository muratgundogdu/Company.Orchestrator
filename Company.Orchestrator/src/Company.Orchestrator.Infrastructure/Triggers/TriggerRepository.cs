using Company.Orchestrator.Application.Triggers;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Triggers;

public sealed class TriggerRepository : ITriggerRepository
{
    private readonly OrchestratorDbContext _context;

    public TriggerRepository(OrchestratorDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Trigger> Items, int TotalCount)> GetAllAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Triggers.OrderByDescending(t => t.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Trigger?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Triggers.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Trigger>> GetActiveFolderWatchersAsync(
        CancellationToken cancellationToken = default)
        => await _context.Triggers
            .Where(t => t.IsActive && t.Type == TriggerType.FolderWatcher)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Trigger>> GetActiveScheduledTriggersAsync(
        CancellationToken cancellationToken = default)
        => await _context.Triggers
            .Where(t => t.IsActive && t.Type == TriggerType.Scheduled && t.CronExpression != null)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Trigger trigger, CancellationToken cancellationToken = default)
        => await _context.Triggers.AddAsync(trigger, cancellationToken);
}
