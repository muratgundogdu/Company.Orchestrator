using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Domain.Entities;

public class ProcessInstance : BaseEntity
{
    public Guid ProcessDefinitionId { get; set; }
    public Guid ProcessVersionId { get; set; }
    public ProcessStatus Status { get; set; } = ProcessStatus.Pending;
    public string? CorrelationId { get; set; }
    public string? InputData { get; set; }
    public string? OutputData { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TriggeredBy { get; set; }

    public ProcessDefinition ProcessDefinition { get; set; } = null!;
    public ProcessVersion ProcessVersion { get; set; } = null!;
    public ICollection<ProcessStepInstance> StepInstances { get; set; } = new List<ProcessStepInstance>();
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
