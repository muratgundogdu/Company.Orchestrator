using Company.Orchestrator.Application.Artifacts;

namespace Company.Orchestrator.Application.Common;

/// <summary>
/// Immutable snapshot of one completed step's results.
/// Stored in WorkflowContext.StepOutputs so downstream steps can access prior step data
/// without reaching back into the database.
/// </summary>
public sealed class StepOutput
{
    public string StepId { get; init; } = string.Empty;
    public string StepName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Variables this step added or overwrote in the context.</summary>
    public IReadOnlyDictionary<string, object> Variables { get; init; }
        = new Dictionary<string, object>();

    /// <summary>All artifacts produced by this step (by their context name).</summary>
    public IReadOnlyList<ArtifactReference> ProducedArtifacts { get; init; }
        = new List<ArtifactReference>();

    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public long DurationMs { get; init; }

    /// <summary>Convenience accessor: returns a produced artifact by name, or null.</summary>
    public ArtifactReference? GetArtifact(string name)
        => ProducedArtifacts.FirstOrDefault(a => a.Name == name);
}
