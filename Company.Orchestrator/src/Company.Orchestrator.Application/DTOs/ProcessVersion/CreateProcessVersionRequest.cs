using System.ComponentModel.DataAnnotations;

namespace Company.Orchestrator.Application.DTOs.ProcessVersion;

public class CreateProcessVersionRequest
{
    [Required]
    public string JsonDefinition { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ChangeNotes { get; set; }
}
