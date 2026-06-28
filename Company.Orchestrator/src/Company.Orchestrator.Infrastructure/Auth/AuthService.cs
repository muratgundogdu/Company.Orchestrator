using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Auth;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly OrchestratorDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditService _audit;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        OrchestratorDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuditService audit,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await _audit.WriteFailureAsync(new AuditWriteRequest
            {
                EventType  = AuditEventTypes.LoginFailed,
                Category   = AuditCategories.Authentication,
                Severity   = AuditSeverity.Warning,
                EntityType = "User",
                EntityId   = username,
                EntityName = username,
                Action     = "Login failed",
                Username   = username,
                IpAddress  = ipAddress,
                UserAgent  = userAgent,
                Details    = new { reason = user is null ? "User not found" : "User inactive" },
            }, cancellationToken);

            return null;
        }

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            await _audit.WriteFailureAsync(new AuditWriteRequest
            {
                EventType   = AuditEventTypes.LoginFailed,
                Category    = AuditCategories.Authentication,
                Severity    = AuditSeverity.Warning,
                UserId      = user.Id,
                Username    = user.Username,
                DisplayName = user.DisplayName,
                EntityType  = "User",
                EntityId    = user.Id.ToString(),
                EntityName  = user.Username,
                Action      = "Login failed",
                IpAddress   = ipAddress,
                UserAgent   = userAgent,
                Details     = new { reason = "Invalid password" },
            }, cancellationToken);

            return null;
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (token, expires) = _jwtTokenService.CreateToken(user.Id, user.Username, roles, permissions);

        await _audit.WriteSuccessAsync(new AuditWriteRequest
        {
            EventType   = AuditEventTypes.UserLogin,
            Category    = AuditCategories.Authentication,
            UserId      = user.Id,
            Username    = user.Username,
            DisplayName = user.DisplayName,
            EntityType  = "User",
            EntityId    = user.Id.ToString(),
            EntityName  = user.Username,
            Action      = "User logged in",
            IpAddress   = ipAddress,
            UserAgent   = userAgent,
        }, cancellationToken);

        _logger.LogInformation("User {Username} logged in", user.Username);

        return new LoginResponse
        {
            Token        = token,
            ExpiresAtUtc = expires,
            User         = MapProfile(user, roles, permissions),
        };
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
            return null;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return MapProfile(user, roles, permissions);
    }

    private static UserProfileDto MapProfile(
        Domain.Entities.User user,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions) => new()
    {
        Id          = user.Id,
        Username    = user.Username,
        DisplayName = user.DisplayName,
        Email       = user.Email,
        Roles       = roles,
        Permissions = permissions,
    };
}
