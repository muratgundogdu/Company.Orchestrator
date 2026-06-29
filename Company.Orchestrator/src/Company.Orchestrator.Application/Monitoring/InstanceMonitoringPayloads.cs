namespace Company.Orchestrator.Application.Monitoring;

public sealed record StepStartedPayload(
    Guid ProcessInstanceId,
    Guid StepInstanceId,
    string StepKey,
    string StepName,
    string Status,
    DateTime StartedAt);

public sealed record StepCompletedPayload(
    Guid ProcessInstanceId,
    Guid StepInstanceId,
    string StepKey,
    string StepName,
    string Status,
    DateTime CompletedAt,
    long? DurationMs);

public sealed record StepFailedPayload(
    Guid ProcessInstanceId,
    Guid StepInstanceId,
    string StepKey,
    string StepName,
    string Status,
    DateTime CompletedAt,
    string? ErrorMessage);

public sealed record InstanceCompletedPayload(
    Guid ProcessInstanceId,
    string Status,
    DateTime CompletedAt,
    long? DurationMs);

public sealed record InstanceMonitoringEnvelope(
    string EventName,
    object Payload);
