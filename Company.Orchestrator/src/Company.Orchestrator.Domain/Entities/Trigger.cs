using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Domain.Entities;

public class Trigger : BaseEntity
{
    public Guid ProcessDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TriggerType Type { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CronExpression { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultInputData { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime? NextScheduledAt { get; set; }

    /// <summary>
    /// JSON configuration specific to the trigger type.
    /// For FolderWatcher: serialised FolderWatcherConfig.
    /// </summary>
    public string? ConfigJson { get; set; }

    public ProcessDefinition ProcessDefinition { get; set; } = null!;
    public ICollection<TriggerEvent> Events { get; set; } = new List<TriggerEvent>();
}
