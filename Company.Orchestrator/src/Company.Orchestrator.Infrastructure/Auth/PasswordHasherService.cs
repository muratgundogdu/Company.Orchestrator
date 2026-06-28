using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Company.Orchestrator.Infrastructure.Auth;

public sealed class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password) =>
        _hasher.HashPassword(new User(), password);

    public bool VerifyPassword(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(new User(), hashedPassword, providedPassword)
            != PasswordVerificationResult.Failed;
}
