using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Auth;

public sealed class AuthDataSeeder
{
    private readonly OrchestratorDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthDataSeeder> _logger;

    public AuthDataSeeder(
        OrchestratorDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<AuthDataSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        foreach (var name in Permissions.All)
        {
            if (await _context.Permissions.AnyAsync(p => p.Name == name, cancellationToken))
                continue;

            _context.Permissions.Add(new Permission
            {
                Name        = name,
                Description = Permissions.Descriptions.TryGetValue(name, out var desc) ? desc : null,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var (roleName, permissionNames) in DefaultRoles.DefaultPermissions)
        {
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);

            if (role is null)
            {
                role = new Role { Name = roleName, Description = $"{roleName} role" };
                _context.Roles.Add(role);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var existingPermissionIds = role.RolePermissions
                .Select(rp => rp.PermissionId)
                .ToHashSet();

            foreach (var permName in permissionNames)
            {
                var permission = await _context.Permissions.FirstAsync(p => p.Name == permName, cancellationToken);
                if (existingPermissionIds.Contains(permission.Id))
                    continue;

                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(u => u.Username == "admin", cancellationToken))
            return;

        var adminRole = await _context.Roles.FirstAsync(r => r.Name == DefaultRoles.Admin, cancellationToken);

        var admin = new User
        {
            Username     = "admin",
            DisplayName  = "Administrator",
            Email        = "admin@alterone.local",
            PasswordHash = _passwordHasher.HashPassword("Admin123!"),
            IsActive     = true,
        };

        _context.Users.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        _context.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded default admin user (username: admin)");
    }
}
