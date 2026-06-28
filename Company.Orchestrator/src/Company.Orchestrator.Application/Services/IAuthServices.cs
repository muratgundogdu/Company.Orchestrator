using Company.Orchestrator.Application.DTOs.Auth;

namespace Company.Orchestrator.Application.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, Guid? performedByUserId, CancellationToken cancellationToken = default);
    Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, Guid? performedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid? performedByUserId, CancellationToken cancellationToken = default);
    Task<UserDto?> AssignRolesAsync(Guid id, AssignUserRolesRequest request, Guid? performedByUserId, CancellationToken cancellationToken = default);
}

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);
    Task<RoleDto?> UpdatePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, Guid? performedByUserId, CancellationToken cancellationToken = default);
}
