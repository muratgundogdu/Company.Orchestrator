namespace Company.Orchestrator.Application.Common.Interfaces;

/// <summary>
/// Provides the persistent worker identity for this host process.
/// </summary>
public interface IWorkerIdentityProvider
{
    string WorkerId { get; }
    string WorkerName { get; }
    string Version { get; }
}
