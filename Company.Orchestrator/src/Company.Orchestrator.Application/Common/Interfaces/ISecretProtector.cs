namespace Company.Orchestrator.Application.Common.Interfaces;

/// <summary>
/// Encrypts and decrypts credential secret values at rest.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}
