using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Constants;

namespace Company.Orchestrator.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = AuditSeverity.Info;

    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }

    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? EntityName { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }

    /// <summary>Legacy field — kept for backward compatibility with older audit rows.</summary>
    public string? OldValues { get; set; }
    /// <summary>Legacy field — kept for backward compatibility with older audit rows.</summary>
    public string? NewValues { get; set; }
    /// <summary>Legacy field — maps to Username for older rows.</summary>
    public string? PerformedBy { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool Success { get; set; } = true;
    public string? CorrelationId { get; set; }
}
