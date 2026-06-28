using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Application.DTOs.Job;

public class JobDto
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public JobStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CancelRequestedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
