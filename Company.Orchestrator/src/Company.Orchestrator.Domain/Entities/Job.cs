using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Domain.Entities;

public class Job : BaseEntity
{
    public Guid ProcessInstanceId { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int AttemptCount { get; set; } = 0;
    public int MaxAttempts { get; set; } = 3;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WorkerInstanceId { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? CancelRequestedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledBy { get; set; }

    public ProcessInstance? ProcessInstance { get; set; }
    public ICollection<JobLog> Logs { get; set; } = new List<JobLog>();
}
