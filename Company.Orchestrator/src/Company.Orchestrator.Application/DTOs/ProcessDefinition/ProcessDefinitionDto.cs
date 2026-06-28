namespace Company.Orchestrator.Application.DTOs.ProcessDefinition;

public class ProcessDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int VersionCount { get; set; }
    public int? LatestVersionNumber { get; set; }
}
