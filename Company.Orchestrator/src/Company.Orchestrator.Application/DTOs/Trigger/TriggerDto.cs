using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Application.DTOs.Trigger;

public sealed class TriggerDto
{
    public Guid Id { get; init; }
    public Guid ProcessDefinitionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? CronExpression { get; init; }
    public string? ConfigJson { get; init; }
    public DateTime? LastTriggeredAt { get; init; }
    public DateTime? NextScheduledAt { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Status of the most recent TriggerEvent (null if no events exist).</summary>
    public string? LastEventStatus { get; init; }

    /// <summary>Error message from the most recent failed TriggerEvent.</summary>
    public string? LastEventError { get; init; }

    public static TriggerDto FromEntity(
        Domain.Entities.Trigger t,
        string? lastEventStatus = null,
        string? lastEventError  = null) => new()
    {
        Id                  = t.Id,
        ProcessDefinitionId = t.ProcessDefinitionId,
        Name                = t.Name,
        Type                = t.Type.ToString(),
        IsActive            = t.IsActive,
        CronExpression      = t.CronExpression,
        ConfigJson          = t.ConfigJson,
        LastTriggeredAt     = t.LastTriggeredAt,
        NextScheduledAt     = t.NextScheduledAt,
        CreatedAt           = t.CreatedAt,
        LastEventStatus     = lastEventStatus,
        LastEventError      = lastEventError,
    };
}
