using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Common.Interfaces;

public interface IProcessInstanceRepository : IRepository<ProcessInstance>
{
    Task<ProcessInstance?> GetWithStepsAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessInstance>> GetByDefinitionIdAsync(Guid definitionId, CancellationToken cancellationToken = default);
}
