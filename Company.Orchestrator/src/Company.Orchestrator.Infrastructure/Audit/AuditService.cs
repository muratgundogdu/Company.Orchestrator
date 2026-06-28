using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Audit;
using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Audit;

public sealed class AuditService : IAuditService
{
    private readonly OrchestratorDbContext _context;

    public AuditService(OrchestratorDbContext context)
    {
        _context = context;
    }

    public Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
        => PersistAsync(request, cancellationToken);

    public Task WriteSuccessAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
        => PersistAsync(request with { Success = true }, cancellationToken);

    public Task WriteFailureAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        var severity = request.Severity == AuditSeverity.Info
            ? AuditSeverity.Warning
            : request.Severity;

        return PersistAsync(request with { Success = false, Severity = severity }, cancellationToken);
    }

    public async Task<PagedResult<AuditLogListItemDto>> QueryAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);

        var query = ApplyFilters(_context.AuditLogs.AsNoTracking(), filter);
        query = query.OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapListItem(a))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AuditLogDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return entity is null ? null : MapDetail(entity);
    }

    public async Task<AuditSummaryDto> GetSummaryAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(_context.AuditLogs.AsNoTracking(), filter);

        var total = await query.CountAsync(cancellationToken);
        var critical = await query.CountAsync(
            a => a.Severity == AuditSeverity.Critical, cancellationToken);
        var failed = await query.CountAsync(a => !a.Success, cancellationToken);

        var topUsers = await query
            .Where(a => a.Username != null && a.Username != "")
            .GroupBy(a => a.Username!)
            .Select(g => new AuditSummaryItemDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topCategories = await query
            .GroupBy(a => a.Category)
            .Select(g => new AuditSummaryItemDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        var uniqueUsers = await query
            .Where(a => a.Username != null && a.Username != "")
            .Select(a => a.Username!)
            .Distinct()
            .CountAsync(cancellationToken);

        return new AuditSummaryDto
        {
            TotalEvents = total,
            CriticalEvents = critical,
            FailedEvents = failed,
            UniqueUsers = uniqueUsers,
            TopUsers = topUsers,
            TopCategories = topCategories,
        };
    }

    public static AuditWriteRequest FromCurrentUser(
        ICurrentUser? user,
        AuditWriteRequest request)
    {
        if (user is null || !user.IsAuthenticated)
            return request;

        return request with
        {
            UserId = request.UserId ?? user.UserId,
            Username = request.Username ?? user.Username,
        };
    }

    private async Task PersistAsync(AuditWriteRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username ?? request.UserId?.ToString();

        _context.AuditLogs.Add(new AuditLog
        {
            EventType   = request.EventType,
            Category    = request.Category,
            Severity    = request.Severity,
            UserId      = request.UserId,
            Username    = username,
            DisplayName = request.DisplayName,
            EntityType  = request.EntityType,
            EntityId    = request.EntityId,
            EntityName  = request.EntityName,
            Action      = request.Action,
            DetailsJson = request.ResolveDetailsJson(),
            PerformedBy = username,
            IpAddress   = request.IpAddress,
            UserAgent   = request.UserAgent,
            Success     = request.Success,
            CorrelationId = request.CorrelationId,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> query, AuditQueryFilter filter)
    {
        if (filter.FromUtc.HasValue)
            query = query.Where(a => a.CreatedAt >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(a => a.CreatedAt < filter.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(a => a.Category == filter.Category);

        if (!string.IsNullOrWhiteSpace(filter.EventType))
            query = query.Where(a => a.EventType == filter.EventType);

        if (!string.IsNullOrWhiteSpace(filter.Username))
            query = query.Where(a => a.Username != null && a.Username.Contains(filter.Username));

        if (!string.IsNullOrWhiteSpace(filter.Severity))
            query = query.Where(a => a.Severity == filter.Severity);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
            query = query.Where(a => a.EntityId == filter.EntityId);

        if (filter.Success.HasValue)
            query = query.Where(a => a.Success == filter.Success.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(a =>
                (a.Username != null && a.Username.Contains(term)) ||
                (a.EntityName != null && a.EntityName.Contains(term)) ||
                a.Action.Contains(term) ||
                a.EventType.Contains(term) ||
                (a.DetailsJson != null && a.DetailsJson.Contains(term)));
        }

        return query;
    }

    private static AuditLogListItemDto MapListItem(AuditLog a) => new()
    {
        Id           = a.Id,
        TimestampUtc = a.CreatedAt,
        EventType    = string.IsNullOrEmpty(a.EventType) ? a.Action : a.EventType,
        Category     = string.IsNullOrEmpty(a.Category) ? AuditCategories.System : a.Category,
        Severity     = string.IsNullOrEmpty(a.Severity) ? AuditSeverity.Info : a.Severity,
        Username     = a.Username ?? a.PerformedBy,
        DisplayName  = a.DisplayName,
        EntityType   = a.EntityType,
        EntityId     = a.EntityId,
        EntityName   = a.EntityName,
        Action       = a.Action,
        Success      = a.Success,
    };

    private static AuditLogDetailDto MapDetail(AuditLog a) => new()
    {
        Id           = a.Id,
        TimestampUtc = a.CreatedAt,
        EventType    = string.IsNullOrEmpty(a.EventType) ? a.Action : a.EventType,
        Category     = string.IsNullOrEmpty(a.Category) ? AuditCategories.System : a.Category,
        Severity     = string.IsNullOrEmpty(a.Severity) ? AuditSeverity.Info : a.Severity,
        UserId       = a.UserId,
        Username     = a.Username ?? a.PerformedBy,
        DisplayName  = a.DisplayName,
        EntityType   = a.EntityType,
        EntityId     = a.EntityId,
        EntityName   = a.EntityName,
        Action       = a.Action,
        Success      = a.Success,
        DetailsJson  = a.DetailsJson ?? BuildLegacyDetails(a),
        IpAddress    = a.IpAddress,
        UserAgent    = a.UserAgent,
        CorrelationId = a.CorrelationId,
    };

    private static string? BuildLegacyDetails(AuditLog a)
    {
        if (a.OldValues is null && a.NewValues is null)
            return null;

        return $"{{\"oldValues\":{Quote(a.OldValues)},\"newValues\":{Quote(a.NewValues)}}}";

        static string Quote(string? value) =>
            value is null ? "null" : $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
