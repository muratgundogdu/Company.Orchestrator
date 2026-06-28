namespace Company.Orchestrator.Application.Common.Interfaces;

public interface IWorkflowEngine
{
    Task ExecuteJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
