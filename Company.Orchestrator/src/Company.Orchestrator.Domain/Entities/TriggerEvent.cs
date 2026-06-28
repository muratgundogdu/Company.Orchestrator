using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Domain.Entities;

/// <summary>
/// Represents one file-detection event raised by a FolderWatcher trigger.
/// Used for deduplication (EventKey = path|size|lastModified) and audit trail.
/// </summary>
public class TriggerEvent : BaseEntity
{
    public Guid TriggerId { get; set; }

    /// <summary>
    /// Deduplication key: "{filePath}|{fileSizeBytes}|{lastWriteTimeUtc:O}".
    /// The same physical file will never produce a second Completed or Processing event.
    /// </summary>
    public string EventKey { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    public TriggerEventStatus Status { get; set; } = TriggerEventStatus.Pending;

    /// <summary>ProcessInstance started for this event. Null until the instance is created.</summary>
    public Guid? ProcessInstanceId { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }

    // ---- Navigation ----
    public Trigger Trigger { get; set; } = null!;
    public ProcessInstance? ProcessInstance { get; set; }
}
