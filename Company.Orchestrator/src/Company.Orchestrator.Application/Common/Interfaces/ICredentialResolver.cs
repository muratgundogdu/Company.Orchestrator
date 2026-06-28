namespace Company.Orchestrator.Application.Common.Interfaces;

/// <summary>
/// Resolves decrypted credential secrets at workflow runtime.
/// </summary>
public interface ICredentialResolver
{
    Task<string> GetSecretByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<string> GetSecretByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the credential name does not exist in the vault.</summary>
    Task<string?> TryGetSecretByNameAsync(string name, CancellationToken cancellationToken = default);
}
