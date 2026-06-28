using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Repositories;

public class ProcessDefinitionRepository : Repository<ProcessDefinition>, IProcessDefinitionRepository
{
    public ProcessDefinitionRepository(OrchestratorDbContext context) : base(context) { }

    public async Task<ProcessDefinition?> GetWithVersionsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.ProcessDefinitions
            .Include(x => x.Versions.Where(v => !v.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ProcessVersion?> GetPublishedVersionAsync(Guid processDefinitionId, CancellationToken cancellationToken = default)
        => await _context.ProcessVersions
            .Where(v => v.ProcessDefinitionId == processDefinitionId && v.Status == VersionStatus.Published && !v.IsDeleted)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ProcessVersion?> GetVersionByNumberAsync(Guid processDefinitionId, int versionNumber, CancellationToken cancellationToken = default)
        => await _context.ProcessVersions
            .FirstOrDefaultAsync(v => v.ProcessDefinitionId == processDefinitionId
                                   && v.VersionNumber == versionNumber
                                   && !v.IsDeleted, cancellationToken);
}
