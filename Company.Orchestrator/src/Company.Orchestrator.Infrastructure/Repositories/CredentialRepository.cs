using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Repositories;

public sealed class CredentialRepository : Repository<Credential>, ICredentialRepository
{
    public CredentialRepository(OrchestratorDbContext context) : base(context)
    {
    }

    public Task<Credential?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(
            x => x.Name == name && x.IsActive,
            cancellationToken);

    public Task<bool> NameExistsAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(x => x.Name == name);
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return query.AnyAsync(cancellationToken);
    }
}
