using System.ComponentModel.DataAnnotations;

namespace Company.Orchestrator.Application.DTOs.ProcessInstance;

public class StartProcessRequest
{
    [Required]
    public Guid ProcessDefinitionId { get; set; }

    public string? CorrelationId { get; set; }
    public string? InputData { get; set; }
    public string? TriggeredBy { get; set; }
}
