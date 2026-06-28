using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Credential;

namespace Company.Orchestrator.Application.Services;

public interface ICredentialService
{
    Task<PagedResult<CredentialDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CredentialDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CredentialDto> CreateAsync(CreateCredentialRequest request, CancellationToken cancellationToken = default);
    Task<CredentialDto?> UpdateAsync(Guid id, UpdateCredentialRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CredentialTestResponse> TestAsync(Guid id, CancellationToken cancellationToken = default);
}
