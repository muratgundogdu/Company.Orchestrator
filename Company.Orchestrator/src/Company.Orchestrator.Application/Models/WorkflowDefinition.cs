namespace Company.Orchestrator.Application.Models;

public class WorkflowDefinition
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object>? DefaultVariables { get; set; }
    public List<StepDefinition> Steps { get; set; } = new();
}

/// <summary>
/// Per-step retry policy. Exported as:
/// <code>{ "retry": { "maxAttempts": 3, "delaySeconds": 10 } }</code>
/// When present, takes precedence over the legacy flat RetryCount / RetryDelaySeconds fields.
/// </summary>
public class RetryPolicy
{
    /// <summary>Total number of attempts including the first. Must be >= 1.</summary>
    public int MaxAttempts { get; set; } = 1;

    /// <summary>Seconds to wait between attempts. Must be >= 0.</summary>
    public int DelaySeconds { get; set; } = 0;
}

public class StepDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Config { get; set; } = new();
    public string? NextStepId { get; set; }

    /// <summary>
    /// When set, the engine routes here whenever this step fails (after all retries are
    /// exhausted) instead of aborting the job.  Error details are injected as context
    /// variables: errorMessage, failedStepId, failedStepName, failedStepType,
    /// failureReportArtifactName.
    /// </summary>
    public string? OnFailureStepId { get; set; }

    /// <summary>Legacy alias — OnFailureStepId takes precedence when both are set.</summary>
    public string? OnErrorStepId { get; set; }

    /// <summary>For condition.if steps: routes here when condition is true.</summary>
    public string? TrueStepId { get; set; }

    /// <summary>For condition.if steps: routes here when condition is false.</summary>
    public string? FalseStepId { get; set; }

    /// <summary>
    /// For foreach.loop / foreach.row / foreach.file steps: the first step of the loop body.
    /// The handler injects the current item/index into context before routing here.
    /// </summary>
    public string? LoopStepId { get; set; }

    /// <summary>
    /// For foreach.loop / foreach.row / foreach.file steps: the step to execute after all items/rows/files have been processed.
    /// Also used when the collection is empty (body is skipped entirely).
    /// </summary>
    public string? CompletedStepId { get; set; }

    /// <summary>
    /// Structured retry policy (preferred). When present, MaxAttempts and DelaySeconds
    /// override the legacy RetryCount / RetryDelaySeconds flat fields.
    /// </summary>
    public RetryPolicy? Retry { get; set; }

    /// <summary>Legacy flat field — still honoured when Retry is null.</summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>Legacy flat field — still honoured when Retry is null.</summary>
    public int RetryDelaySeconds { get; set; } = 5;

    public bool ContinueOnError { get; set; } = false;

    // ── Resolved helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Effective total attempts (Retry.MaxAttempts wins over legacy RetryCount + 1).
    /// Always >= 1.
    /// </summary>
    public int EffectiveMaxAttempts =>
        Retry is not null
            ? Math.Max(1, Retry.MaxAttempts)
            : Math.Max(1, RetryCount + 1);   // legacy: RetryCount=0 means 1 total attempt

    /// <summary>Effective delay in seconds between attempts.</summary>
    public int EffectiveDelaySeconds =>
        Retry is not null ? Math.Max(0, Retry.DelaySeconds) : RetryDelaySeconds;
}
