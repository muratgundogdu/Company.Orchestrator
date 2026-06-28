using Company.Orchestrator.Domain.Common;

namespace Company.Orchestrator.Domain.Entities;

public class ProcessDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProcessVersion> Versions { get; set; } = new List<ProcessVersion>();
    public ICollection<ProcessInstance> Instances { get; set; } = new List<ProcessInstance>();
    public ICollection<Trigger> Triggers { get; set; } = new List<Trigger>();
}
