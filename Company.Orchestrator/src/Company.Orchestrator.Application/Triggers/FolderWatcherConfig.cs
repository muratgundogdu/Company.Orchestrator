namespace Company.Orchestrator.Application.Triggers;

/// <summary>
/// Typed representation of Trigger.ConfigJson for TriggerType.FolderWatcher.
/// Stored as JSON in the Triggers table; deserialized by FolderWatcherWorker at runtime.
/// </summary>
public sealed class FolderWatcherConfig
{
    /// <summary>Folder to watch (local or UNC path). Required.</summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>File glob pattern (default: "*"). Examples: "*.xlsx", "report_*.csv".</summary>
    public string Pattern { get; set; } = "*";

    /// <summary>ProcessDefinition to start when a new file is detected. Required.</summary>
    public Guid ProcessDefinitionId { get; set; }

    /// <summary>When true, moves the file to ProcessingFolder before starting the instance.</summary>
    public bool MoveToProcessingFolder { get; set; } = false;

    /// <summary>Path to move the file to while it is being processed.</summary>
    public string? ProcessingFolder { get; set; }

    /// <summary>Path to move the file to after the process completes successfully.</summary>
    public string? ProcessedFolder { get; set; }

    /// <summary>Path to move the file to if the process fails.</summary>
    public string? ErrorFolder { get; set; }
}
