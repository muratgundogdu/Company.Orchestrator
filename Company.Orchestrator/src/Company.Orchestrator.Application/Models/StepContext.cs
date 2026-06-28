using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Models;

public class StepContext
{
    public ProcessInstance ProcessInstance { get; set; } = null!;
    public StepDefinition StepDefinition { get; set; } = null!;
    public Dictionary<string, object> Variables { get; set; } = new();
    public Guid JobId { get; set; }
}
