using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Common.Interfaces;

public interface IProcessDefinitionRepository : IRepository<ProcessDefinition>
{
    Task<ProcessDefinition?> GetWithVersionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcessVersion?> GetPublishedVersionAsync(Guid processDefinitionId, CancellationToken cancellationToken = default);
    Task<ProcessVersion?> GetVersionByNumberAsync(Guid processDefinitionId, int versionNumber, CancellationToken cancellationToken = default);
}
