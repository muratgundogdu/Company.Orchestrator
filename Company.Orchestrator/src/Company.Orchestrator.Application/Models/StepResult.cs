using Company.Orchestrator.Application.Artifacts;

namespace Company.Orchestrator.Application.Models;

/// <summary>
/// Value returned by IStepHandler.ExecuteAsync().
/// Carries output variables, produced artifacts, and success/failure state.
/// The WorkflowEngine merges these into the WorkflowContext after each step.
/// </summary>
public class StepResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Variables to merge into WorkflowContext.Variables after this step.</summary>
    public Dictionary<string, object>? OutputVariables { get; set; }

    /// <summary>
    /// Artifacts produced by this step.
    /// The engine persists these to IArtifactRepository and registers them in WorkflowContext.Artifacts
    /// under each artifact's Name.
    /// </summary>
    public List<ArtifactReference>? ProducedArtifacts { get; set; }

    /// <summary>Optional free-text summary stored in ProcessStepInstance.OutputData.</summary>
    public string? OutputData { get; set; }

    // --- Factories ---

    public static StepResult Ok(
        Dictionary<string, object>? output = null,
        List<ArtifactReference>? artifacts = null,
        string? outputData = null) =>
        new()
        {
            Success = true,
            OutputVariables = output,
            ProducedArtifacts = artifacts,
            OutputData = outputData
        };

    public static StepResult Fail(string error, Dictionary<string, object>? output = null) =>
        new() { Success = false, ErrorMessage = error, OutputVariables = output };

    /// <summary>Produces a result with a single artifact as primary output.</summary>
    public static StepResult WithArtifact(ArtifactReference artifact, string? outputData = null) =>
        Ok(artifacts: [artifact], outputData: outputData ?? artifact.ToString());
}
