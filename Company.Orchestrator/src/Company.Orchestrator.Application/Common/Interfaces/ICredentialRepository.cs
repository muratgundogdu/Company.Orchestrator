using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Common.Interfaces;

public interface ICredentialRepository : IRepository<Credential>
{
    Task<Credential?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
