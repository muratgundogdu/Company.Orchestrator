using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Monitoring;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Infrastructure.Monitoring;

public sealed class NoOpInstanceMonitoringPublisher : IInstanceMonitoringPublisher
{
    public Task PublishEnvelopeAsync(InstanceMonitoringEnvelope envelope, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishStepStartedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishStepCompletedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishStepFailedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishInstanceCompletedAsync(
        ProcessInstance instance,
        ProcessStatus status,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
