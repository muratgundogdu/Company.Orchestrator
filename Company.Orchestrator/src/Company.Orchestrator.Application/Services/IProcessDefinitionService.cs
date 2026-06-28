using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.ProcessDefinition;
using Company.Orchestrator.Application.DTOs.ProcessVersion;

namespace Company.Orchestrator.Application.Services;

public interface IProcessDefinitionService
{
    Task<PagedResult<ProcessDefinitionDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ProcessDefinitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProcessDefinitionDto> CreateAsync(CreateProcessDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<ProcessDefinitionDto?> UpdateAsync(Guid id, UpdateProcessDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessVersionDto>> GetVersionsAsync(Guid definitionId, CancellationToken cancellationToken = default);
    Task<ProcessVersionDto> CreateVersionAsync(Guid definitionId, CreateProcessVersionRequest request, CancellationToken cancellationToken = default);
    Task<ProcessVersionDto?> PublishVersionAsync(Guid definitionId, Guid versionId, CancellationToken cancellationToken = default);
}
