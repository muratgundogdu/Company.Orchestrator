using Company.Orchestrator.Domain.Common;

namespace Company.Orchestrator.Domain.Entities;

public class JobLog : BaseEntity
{
    public Guid JobId { get; set; }
    public Guid? StepInstanceId { get; set; }
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Exception { get; set; }

    public Job Job { get; set; } = null!;
    public ProcessStepInstance? StepInstance { get; set; }
}
