using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Application.Monitoring;

namespace Company.Orchestrator.Application.Common.Interfaces;

/// <summary>
/// Publishes live process-instance monitoring events (SignalR on API, HTTP relay from Worker).
/// Database updates remain the source of truth; publishing is best-effort.
/// </summary>
public interface IInstanceMonitoringPublisher
{
    Task PublishEnvelopeAsync(InstanceMonitoringEnvelope envelope, CancellationToken cancellationToken = default);

    Task PublishStepStartedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default);

    Task PublishStepCompletedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default);

    Task PublishStepFailedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default);

    Task PublishInstanceCompletedAsync(
        ProcessInstance instance,
        ProcessStatus status,
        CancellationToken cancellationToken = default);
}
