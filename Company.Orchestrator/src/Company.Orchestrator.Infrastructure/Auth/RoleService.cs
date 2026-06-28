using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Auth;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Auth;

public sealed class RoleService : IRoleService
{
    private readonly OrchestratorDbContext _context;
    private readonly IAuditLogWriter _audit;

    public RoleService(OrchestratorDbContext context, IAuditLogWriter audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _context.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(MapDto).ToList();
    }

    public async Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .OrderBy(p => p.Name)
            .Select(p => new PermissionDto { Id = p.Id, Name = p.Name, Description = p.Description })
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDto?> UpdatePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        Guid? performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null) return null;

        var permissionNames = request.Permissions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (role.Name == DefaultRoles.Admin
            && !permissionNames.Contains(Permissions.AdminManage, StringComparer.OrdinalIgnoreCase))
        {
            permissionNames = Permissions.All.ToList();
        }

        _context.RolePermissions.RemoveRange(role.RolePermissions);

        foreach (var name in permissionNames)
        {
            var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
            if (permission is null) continue;
            _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("Role", role.Id.ToString(), "UpdatePermissions", performedByUserId?.ToString(),
            newValues: string.Join(",", permissionNames), cancellationToken: cancellationToken);

        return MapDto(await _context.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == roleId, cancellationToken));
    }

    private static RoleDto MapDto(Role role) => new()
    {
        Id          = role.Id,
        Name        = role.Name,
        Description = role.Description,
        Permissions = role.RolePermissions.Select(rp => rp.Permission.Name).OrderBy(n => n).ToList(),
    };
}
