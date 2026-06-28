using Company.Orchestrator.Application.Common.Exceptions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Auth;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Auth;

public sealed class UserService : IUserService
{
    private readonly OrchestratorDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogWriter _audit;
    private readonly ILogger<UserService> _logger;

    public UserService(
        OrchestratorDbContext context,
        IPasswordHasher passwordHasher,
        IAuditLogWriter audit,
        ILogger<UserService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);

        return users.Select(MapDto).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken);
        return user is null ? null : MapDto(user);
    }

    public async Task<UserDto> CreateAsync(
        CreateUserRequest request,
        Guid? performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        if (await _context.Users.AnyAsync(u => u.Username == username, cancellationToken))
            throw new FieldValidationException("Username already exists.", "username");

        var email = request.Email.Trim();
        if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new FieldValidationException("Email already exists.", "email");

        var user = new User
        {
            Username     = username,
            DisplayName  = request.DisplayName.Trim(),
            Email        = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive     = request.IsActive,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        await AssignRolesInternalAsync(user, request.Roles, cancellationToken);
        await _audit.WriteAsync("User", user.Id.ToString(), "Create", performedByUserId?.ToString(),
            newValues: username, cancellationToken: cancellationToken);

        _logger.LogInformation("User {Username} created", username);
        return MapDto(await LoadUserAsync(user.Id, cancellationToken) ?? user);
    }

    public async Task<UserDto?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        Guid? performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken);
        if (user is null) return null;

        if (request.IsActive == false && performedByUserId == id)
            throw new FieldValidationException("Cannot deactivate the currently logged-in user.", "isActive");

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (await _context.Users.AnyAsync(u => u.Email == email && u.Id != id, cancellationToken))
                throw new FieldValidationException("Email already exists.", "email");
            user.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _passwordHasher.HashPassword(request.Password);

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("User", id.ToString(), "Update", performedByUserId?.ToString(), cancellationToken: cancellationToken);
        return MapDto(await LoadUserAsync(id, cancellationToken) ?? user);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? performedByUserId, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken);
        if (user is null) return false;

        if (performedByUserId == id)
            throw new FieldValidationException("Cannot delete the currently logged-in user.", "id");

        if (await IsLastAdminAsync(user, cancellationToken))
            throw new FieldValidationException("Cannot delete the last Admin user.", "id");

        user.IsDeleted = true;
        user.IsActive  = false;
        await _context.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("User", id.ToString(), "Delete", performedByUserId?.ToString(), cancellationToken: cancellationToken);
        return true;
    }

    public async Task<UserDto?> AssignRolesAsync(
        Guid id,
        AssignUserRolesRequest request,
        Guid? performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken);
        if (user is null) return null;

        var newRoles = request.Roles
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentlyAdmin = user.UserRoles.Any(ur =>
            ur.Role.Name.Equals(DefaultRoles.Admin, StringComparison.OrdinalIgnoreCase));
        var willBeAdmin = newRoles.Contains(DefaultRoles.Admin, StringComparer.OrdinalIgnoreCase);

        if (currentlyAdmin && !willBeAdmin && await CountAdminUsersAsync(cancellationToken) <= 1)
            throw new FieldValidationException("Cannot remove the last Admin user.", "roles");

        await AssignRolesInternalAsync(user, newRoles, cancellationToken);

        await _audit.WriteAsync("User", id.ToString(), "AssignRoles", performedByUserId?.ToString(),
            newValues: string.Join(",", newRoles), cancellationToken: cancellationToken);

        user = await LoadUserAsync(id, cancellationToken);
        return user is null ? null : MapDto(user);
    }

    private async Task AssignRolesInternalAsync(User user, IReadOnlyList<string> roleNames, CancellationToken cancellationToken)
    {
        var existing = await _context.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(cancellationToken);
        _context.UserRoles.RemoveRange(existing);

        foreach (var roleName in roleNames)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
                ?? throw new FieldValidationException($"Role '{roleName}' was not found.", "roles");
            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<User?> LoadUserAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    private async Task<bool> IsLastAdminAsync(User user, CancellationToken cancellationToken)
    {
        if (!user.UserRoles.Any(ur => ur.Role.Name == DefaultRoles.Admin))
            return false;

        return await CountAdminUsersAsync(cancellationToken) <= 1;
    }

    private async Task<int> CountAdminUsersAsync(CancellationToken cancellationToken)
    {
        return await _context.UserRoles
            .Include(ur => ur.Role)
            .Include(ur => ur.User)
            .CountAsync(ur => ur.Role.Name == DefaultRoles.Admin && ur.User != null && ur.User.IsActive && !ur.User.IsDeleted, cancellationToken);
    }

    private static UserDto MapDto(User user) => new()
    {
        Id          = user.Id,
        Username    = user.Username,
        DisplayName = user.DisplayName,
        Email       = user.Email,
        IsActive    = user.IsActive,
        Roles       = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
        CreatedAt   = user.CreatedAt,
    };
}
