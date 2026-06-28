using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Domain.Entities;

public class ProcessStepInstance : BaseEntity
{
    public Guid ProcessInstanceId { get; set; }
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string? InputData { get; set; }
    public string? OutputData { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }

    public ProcessInstance? ProcessInstance { get; set; }
    public ICollection<JobLog> Logs { get; set; } = new List<JobLog>();
}
