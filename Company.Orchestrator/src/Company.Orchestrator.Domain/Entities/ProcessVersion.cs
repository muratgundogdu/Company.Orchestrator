using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Domain.Entities;

public class ProcessVersion : BaseEntity
{
    public Guid ProcessDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public string JsonDefinition { get; set; } = string.Empty;
    public VersionStatus Status { get; set; } = VersionStatus.Draft;
    public string? ChangeNotes { get; set; }
    public DateTime? PublishedAt { get; set; }

    public ProcessDefinition ProcessDefinition { get; set; } = null!;
    public ICollection<ProcessInstance> Instances { get; set; } = new List<ProcessInstance>();
}
