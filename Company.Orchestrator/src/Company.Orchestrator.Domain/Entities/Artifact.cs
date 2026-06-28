using Company.Orchestrator.Domain.Common;

namespace Company.Orchestrator.Domain.Entities;

/// <summary>
/// Persistent record of a piece of data produced or consumed by a workflow step.
/// Actual binary content lives in IArtifactStore; this entity carries metadata only.
/// </summary>
public class Artifact : BaseEntity
{
    public Guid ProcessInstanceId { get; set; }
    public Guid? StepInstanceId { get; set; }

    /// <summary>Logical name used to reference the artifact within the workflow context (e.g. "invoice-pdf").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>MIME type (e.g. "application/pdf", "application/vnd.ms-excel").</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Opaque path understood by the configured IArtifactStore (e.g. a relative file path or blob URI).</summary>
    public string StoragePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>JSON-serialised key/value pairs — capability-specific (e.g. original filename, sheet count).</summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When true the artifact survives process completion.
    /// When false it is eligible for cleanup after the process finishes.
    /// </summary>
    public bool IsPersistent { get; set; } = true;

    public ProcessInstance? ProcessInstance { get; set; }
    public ProcessStepInstance? StepInstance { get; set; }
}
