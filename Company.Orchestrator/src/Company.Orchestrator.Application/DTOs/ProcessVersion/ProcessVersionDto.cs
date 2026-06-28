using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Application.DTOs.ProcessVersion;

public class ProcessVersionDto
{
    public Guid Id { get; set; }
    public Guid ProcessDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public string JsonDefinition { get; set; } = string.Empty;
    public VersionStatus Status { get; set; }
    public string? ChangeNotes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
