using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Monitoring;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Api.Hubs;
using Company.Orchestrator.Infrastructure.Monitoring;
using Microsoft.AspNetCore.SignalR;

namespace Company.Orchestrator.Api.Monitoring;

public sealed class SignalRInstanceMonitoringPublisher : IInstanceMonitoringPublisher
{
    private readonly IHubContext<InstanceMonitoringHub> _hub;

    public SignalRInstanceMonitoringPublisher(IHubContext<InstanceMonitoringHub> hub)
    {
        _hub = hub;
    }

    public Task PublishEnvelopeAsync(InstanceMonitoringEnvelope envelope, CancellationToken cancellationToken = default)
        => DispatchAsync(envelope.EventName, envelope.Payload, cancellationToken);

    public Task PublishStepStartedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => DispatchAsync(
            InstanceMonitoringEventNames.StepStarted,
            InstanceMonitoringPayloadMapper.ToStepStarted(step),
            cancellationToken);

    public Task PublishStepCompletedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => DispatchAsync(
            InstanceMonitoringEventNames.StepCompleted,
            InstanceMonitoringPayloadMapper.ToStepCompleted(step),
            cancellationToken);

    public Task PublishStepFailedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => DispatchAsync(
            InstanceMonitoringEventNames.StepFailed,
            InstanceMonitoringPayloadMapper.ToStepFailed(step),
            cancellationToken);

    public Task PublishInstanceCompletedAsync(
        ProcessInstance instance,
        ProcessStatus status,
        CancellationToken cancellationToken = default)
        => DispatchAsync(
            InstanceMonitoringEventNames.InstanceCompleted,
            InstanceMonitoringPayloadMapper.ToInstanceCompleted(instance, status),
            cancellationToken);

    private Task DispatchAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        var processInstanceId = ExtractProcessInstanceId(payload);
        if (processInstanceId is null)
            return Task.CompletedTask;

        return _hub.Clients
            .Group(InstanceMonitoringGroups.ForInstance(processInstanceId.Value))
            .SendAsync(eventName, payload, cancellationToken);
    }

    internal static Guid? ExtractProcessInstanceId(object payload) => payload switch
    {
        StepStartedPayload started       => started.ProcessInstanceId,
        StepCompletedPayload completed   => completed.ProcessInstanceId,
        StepFailedPayload failed         => failed.ProcessInstanceId,
        InstanceCompletedPayload done    => done.ProcessInstanceId,
        _                                => null,
    };
}
