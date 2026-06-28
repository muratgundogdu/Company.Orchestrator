using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Repositories;

public class ProcessInstanceRepository : Repository<ProcessInstance>, IProcessInstanceRepository
{
    public ProcessInstanceRepository(OrchestratorDbContext context) : base(context) { }

    public async Task<ProcessInstance?> GetWithStepsAsync(Guid instanceId, CancellationToken cancellationToken = default)
        => await _context.ProcessInstances
            .Include(x => x.StepInstances)
            .Include(x => x.ProcessDefinition)
            .Include(x => x.ProcessVersion)
            .FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);

    public async Task<IReadOnlyList<ProcessInstance>> GetByDefinitionIdAsync(Guid definitionId, CancellationToken cancellationToken = default)
        => await _context.ProcessInstances
            .Where(x => x.ProcessDefinitionId == definitionId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
}
