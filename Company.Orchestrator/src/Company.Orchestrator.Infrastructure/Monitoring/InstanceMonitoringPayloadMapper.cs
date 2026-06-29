using Company.Orchestrator.Application.Monitoring;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Infrastructure.Monitoring;

public static class InstanceMonitoringPayloadMapper
{
    public static StepStartedPayload ToStepStarted(ProcessStepInstance step) =>
        new(
            step.ProcessInstanceId,
            step.Id,
            step.StepId,
            step.StepName,
            "Running",
            step.StartedAt ?? DateTime.UtcNow);

    public static StepCompletedPayload ToStepCompleted(ProcessStepInstance step) =>
        new(
            step.ProcessInstanceId,
            step.Id,
            step.StepId,
            step.StepName,
            "Completed",
            step.CompletedAt ?? DateTime.UtcNow,
            step.DurationMs);

    public static StepFailedPayload ToStepFailed(ProcessStepInstance step) =>
        new(
            step.ProcessInstanceId,
            step.Id,
            step.StepId,
            step.StepName,
            "Failed",
            step.CompletedAt ?? DateTime.UtcNow,
            step.ErrorMessage);

    public static InstanceCompletedPayload ToInstanceCompleted(ProcessInstance instance, ProcessStatus status)
    {
        var completedAt = instance.CompletedAt ?? DateTime.UtcNow;
        long? durationMs = null;
        if (instance.StartedAt.HasValue)
            durationMs = (long)(completedAt - instance.StartedAt.Value).TotalMilliseconds;

        return new InstanceCompletedPayload(
            instance.Id,
            MapProcessStatus(status),
            completedAt,
            durationMs);
    }

    public static string MapProcessStatus(ProcessStatus status) => status switch
    {
        ProcessStatus.Success   => "Success",
        ProcessStatus.Failed    => "Failed",
        ProcessStatus.Cancelled => "Cancelled",
        ProcessStatus.Running   => "Running",
        _                       => "Pending",
    };
}
