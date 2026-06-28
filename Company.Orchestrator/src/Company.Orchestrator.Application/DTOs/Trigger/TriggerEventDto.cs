using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.DTOs.Trigger;

public sealed class TriggerEventDto
{
    public Guid Id { get; init; }
    public Guid TriggerId { get; init; }
    public string EventKey { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? ProcessInstanceId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    public static TriggerEventDto FromEntity(TriggerEvent e) => new()
    {
        Id                = e.Id,
        TriggerId         = e.TriggerId,
        EventKey          = e.EventKey,
        FilePath          = e.FilePath,
        FileName          = e.FileName,
        Status            = e.Status.ToString(),
        ProcessInstanceId = e.ProcessInstanceId,
        ErrorMessage      = e.ErrorMessage,
        CreatedAt         = e.CreatedAt,
        CompletedAt       = e.CompletedAt
    };
}
