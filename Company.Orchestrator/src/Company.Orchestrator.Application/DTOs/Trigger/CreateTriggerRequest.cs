using System.ComponentModel.DataAnnotations;

namespace Company.Orchestrator.Application.DTOs.Trigger;

public sealed class CreateTriggerRequest
{
    [Required]
    public Guid ProcessDefinitionId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Manual", "Scheduled", "Api", or "FolderWatcher"</summary>
    [Required]
    public string Type { get; set; } = "FolderWatcher";

    public bool IsActive { get; set; } = true;

    /// <summary>Cron expression for Scheduled triggers.</summary>
    public string? CronExpression { get; set; }

    /// <summary>JSON-serialised FolderWatcherConfig for FolderWatcher triggers.</summary>
    public string? ConfigJson { get; set; }

    /// <summary>Default input data merged into every triggered ProcessInstance.</summary>
    public string? DefaultInputData { get; set; }
}
