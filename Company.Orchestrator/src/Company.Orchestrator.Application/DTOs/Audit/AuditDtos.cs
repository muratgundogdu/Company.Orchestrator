namespace Company.Orchestrator.Application.DTOs.Audit;

public sealed class AuditQueryFilter
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Category { get; set; }
    public string? EventType { get; set; }
    public string? Username { get; set; }
    public string? Severity { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public bool? Success { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class AuditLogListItemDto
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public sealed class AuditLogDetailDto
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class AuditSummaryDto
{
    public int TotalEvents { get; set; }
    public int CriticalEvents { get; set; }
    public int FailedEvents { get; set; }
    public int UniqueUsers { get; set; }
    public IReadOnlyList<AuditSummaryItemDto> TopUsers { get; set; } = Array.Empty<AuditSummaryItemDto>();
    public IReadOnlyList<AuditSummaryItemDto> TopCategories { get; set; } = Array.Empty<AuditSummaryItemDto>();
}

public sealed class AuditSummaryItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
