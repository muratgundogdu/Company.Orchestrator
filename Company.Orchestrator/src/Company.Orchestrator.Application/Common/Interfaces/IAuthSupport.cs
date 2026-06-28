namespace Company.Orchestrator.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Permissions { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasPermission(string permission);
}

public interface IAuditLogWriter
{
    Task WriteAsync(
        string entityName,
        string entityId,
        string action,
        string? performedBy,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string hashedPassword, string providedPassword);
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(Guid userId, string username, IEnumerable<string> roles, IEnumerable<string> permissions);
}
