using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Trigger;
using Company.Orchestrator.Application.Triggers;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Triggers;

public sealed class TriggerService : ITriggerService
{
    private readonly OrchestratorDbContext _context;
    private readonly ITriggerRepository _triggerRepository;
    private readonly ITriggerEventRepository _eventRepository;
    private readonly ILogger<TriggerService> _logger;

    public TriggerService(
        OrchestratorDbContext context,
        ITriggerRepository triggerRepository,
        ITriggerEventRepository eventRepository,
        ILogger<TriggerService> logger)
    {
        _context          = context;
        _triggerRepository = triggerRepository;
        _eventRepository  = eventRepository;
        _logger           = logger;
    }

    public async Task<PagedResult<TriggerDto>> GetAllAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _triggerRepository.GetAllAsync(page, pageSize, cancellationToken);

        // Fetch the latest event per trigger in a single query, then group in memory
        var ids = items.Select(t => t.Id).ToList();
        var recentEvents = await _context.TriggerEvents
            .Where(e => ids.Contains(e.TriggerId))
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.TriggerId, e.Status, e.ErrorMessage, e.CreatedAt })
            .ToListAsync(cancellationToken);

        var lastEventByTrigger = recentEvents
            .GroupBy(e => e.TriggerId)
            .ToDictionary(g => g.Key, g => g.First());

        return new PagedResult<TriggerDto>
        {
            Items = items.Select(t =>
            {
                lastEventByTrigger.TryGetValue(t.Id, out var le);
                return TriggerDto.FromEntity(
                    t,
                    lastEventStatus: le?.Status.ToString(),
                    lastEventError:  le?.Status == Domain.Enums.TriggerEventStatus.Failed
                                       ? le.ErrorMessage
                                       : null);
            }).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<TriggerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _triggerRepository.GetByIdAsync(id, cancellationToken);
        return t is null ? null : TriggerDto.FromEntity(t);
    }

    public async Task<TriggerDto> CreateAsync(
        CreateTriggerRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TriggerType>(request.Type, ignoreCase: true, out var type))
            throw new ArgumentException($"Unknown trigger type '{request.Type}'.");

        var trigger = new Trigger
        {
            ProcessDefinitionId = request.ProcessDefinitionId,
            Name                = request.Name,
            Type                = type,
            IsActive            = request.IsActive,
            CronExpression      = request.CronExpression,
            ConfigJson          = request.ConfigJson,
            DefaultInputData    = request.DefaultInputData
        };

        await _triggerRepository.AddAsync(trigger, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Trigger '{Name}' ({Type}) created for ProcessDefinition {DefId}",
            trigger.Name, trigger.Type, trigger.ProcessDefinitionId);

        return TriggerDto.FromEntity(trigger);
    }

    public async Task<TriggerDto?> UpdateAsync(
        Guid id, UpdateTriggerRequest request, CancellationToken cancellationToken = default)
    {
        var trigger = await _triggerRepository.GetByIdAsync(id, cancellationToken);
        if (trigger is null) return null;

        if (request.Name is not null)           trigger.Name           = request.Name;
        if (request.IsActive.HasValue)          trigger.IsActive       = request.IsActive.Value;
        if (request.CronExpression is not null) trigger.CronExpression = request.CronExpression;
        if (request.ConfigJson is not null)     trigger.ConfigJson     = request.ConfigJson;
        if (request.DefaultInputData is not null) trigger.DefaultInputData = request.DefaultInputData;

        await _context.SaveChangesAsync(cancellationToken);
        return TriggerDto.FromEntity(trigger);
    }

    public async Task<bool> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trigger = await _triggerRepository.GetByIdAsync(id, cancellationToken);
        if (trigger is null) return false;
        trigger.IsActive = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trigger = await _triggerRepository.GetByIdAsync(id, cancellationToken);
        if (trigger is null) return false;
        trigger.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResult<TriggerEventDto>> GetEventsAsync(
        Guid triggerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _eventRepository.GetByTriggerIdAsync(
            triggerId, page, pageSize, cancellationToken);

        return new PagedResult<TriggerEventDto>
        {
            Items      = items.Select(TriggerEventDto.FromEntity).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }
}
