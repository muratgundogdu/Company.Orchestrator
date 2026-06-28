using System.Text.Json;

namespace Company.Orchestrator.Application.Audit;

public sealed record AuditWriteRequest
{
    public string EventType { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Severity { get; init; } = Domain.Constants.AuditSeverity.Info;

    public Guid? UserId { get; init; }
    public string? Username { get; init; }
    public string? DisplayName { get; init; }

    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? EntityName { get; init; }

    public string Action { get; init; } = string.Empty;
    public object? Details { get; init; }
    public string? DetailsJson { get; init; }

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public bool Success { get; init; } = true;
    public string? CorrelationId { get; init; }

    public string? ResolveDetailsJson()
    {
        if (!string.IsNullOrWhiteSpace(DetailsJson))
            return DetailsJson;

        return Details is null ? null : JsonSerializer.Serialize(Details);
    }
}
