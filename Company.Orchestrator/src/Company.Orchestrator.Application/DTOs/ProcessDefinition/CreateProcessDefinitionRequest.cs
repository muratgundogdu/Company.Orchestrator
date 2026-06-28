using System.ComponentModel.DataAnnotations;

namespace Company.Orchestrator.Application.DTOs.ProcessDefinition;

public class CreateProcessDefinitionRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }
}
