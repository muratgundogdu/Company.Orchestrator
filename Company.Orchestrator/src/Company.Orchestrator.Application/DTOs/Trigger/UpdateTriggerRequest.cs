using System.ComponentModel.DataAnnotations;

namespace Company.Orchestrator.Application.DTOs.Trigger;

public sealed class UpdateTriggerRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public bool? IsActive { get; set; }
    public string? CronExpression { get; set; }
    public string? ConfigJson { get; set; }
    public string? DefaultInputData { get; set; }
}
