using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Auth;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
[Produces("application/json")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _service;
    private readonly ICurrentUser _currentUser;

    public RolesController(IRoleService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("roles")]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("permissions")]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
        => Ok(await _service.GetAllPermissionsAsync(cancellationToken));

    [HttpPut("roles/{id:guid}/permissions")]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> UpdatePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _service.UpdatePermissionsAsync(id, request, _currentUser.UserId, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }
}
