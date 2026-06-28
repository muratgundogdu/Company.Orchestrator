using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Application.DTOs.ProcessInstance;

public class ProcessInstanceDto
{
    public Guid Id { get; set; }
    public Guid ProcessDefinitionId { get; set; }
    public string ProcessDefinitionName { get; set; } = string.Empty;
    public Guid ProcessVersionId { get; set; }
    public int VersionNumber { get; set; }
    public ProcessStatus Status { get; set; }
    public string? CorrelationId { get; set; }
    public string? InputData { get; set; }
    public string? OutputData { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TriggeredBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<StepInstanceDto> Steps { get; set; } = new();
}

public class StepInstanceDto
{
    public Guid Id { get; set; }
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public StepStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    /// <summary>Final attempt number for this step (1 = no retries occurred).</summary>
    public int AttemptNumber { get; set; } = 1;
}
