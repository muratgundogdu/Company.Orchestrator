using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Infrastructure.Audit;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Services;

public sealed class CredentialResolver : ICredentialResolver
{
    private readonly ICredentialRepository _repository;
    private readonly ISecretProtector _protector;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<CredentialResolver> _logger;

    public CredentialResolver(
        ICredentialRepository repository,
        ISecretProtector protector,
        ICurrentUser currentUser,
        IAuditService audit,
        ILogger<CredentialResolver> logger)
    {
        _repository  = repository;
        _protector   = protector;
        _currentUser = currentUser;
        _audit       = audit;
        _logger      = logger;
    }

    public async Task<string> GetSecretByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureCanUseCredential();
        var secret = await TryGetSecretByNameAsync(name, cancellationToken);
        if (secret is null)
            throw new InvalidOperationException($"Credential '{name}' was not found in the vault.");

        return secret;
    }

    public async Task<string> GetSecretByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanUseCredential();
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            throw new InvalidOperationException($"Credential '{id}' was not found in the vault.");

        _logger.LogInformation("Credential used: {CredentialName}", entity.Name);
        await AuditCredentialUsedAsync(entity.Id, entity.Name, cancellationToken);
        return _protector.Unprotect(entity.EncryptedValue);
    }

    public async Task<string?> TryGetSecretByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        EnsureCanUseCredential();

        var entity = await _repository.GetByNameAsync(name.Trim(), cancellationToken);
        if (entity is null)
            return null;

        _logger.LogInformation("Credential used: {CredentialName}", entity.Name);
        await AuditCredentialUsedAsync(entity.Id, entity.Name, cancellationToken);
        return _protector.Unprotect(entity.EncryptedValue);
    }

    private void EnsureCanUseCredential()
    {
        if (_currentUser.IsAuthenticated && !_currentUser.HasPermission(Permissions.CredentialUse))
            throw new UnauthorizedAccessException("Credential use is not permitted.");
    }

    private async Task AuditCredentialUsedAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return;

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.CredentialUsed,
            Category   = AuditCategories.Credential,
            EntityType = "Credential",
            EntityId   = id.ToString(),
            EntityName = name,
            Action     = "Credential used",
        }), cancellationToken);
    }
}
