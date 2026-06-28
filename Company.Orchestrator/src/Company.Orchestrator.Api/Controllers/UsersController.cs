using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.Common.Exceptions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Auth;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _service;
    private readonly ICurrentUser _currentUser;

    public UsersController(IUserService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _service.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.CreateAsync(request, _currentUser.UserId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (FieldValidationException ex)
        {
            return BadRequest(new { message = ex.Message, field = ex.Field });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.UpdateAsync(id, request, _currentUser.UserId, cancellationToken);
            return user is null ? NotFound() : Ok(user);
        }
        catch (FieldValidationException ex)
        {
            return BadRequest(new { message = ex.Message, field = ex.Field });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id, _currentUser.UserId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (FieldValidationException ex)
        {
            return BadRequest(new { message = ex.Message, field = ex.Field });
        }
    }

    [HttpPost("{id:guid}/roles")]
    [RequirePermission(Permissions.AdminManage)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignUserRolesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.AssignRolesAsync(id, request, _currentUser.UserId, cancellationToken);
            return user is null ? NotFound() : Ok(user);
        }
        catch (FieldValidationException ex)
        {
            return BadRequest(new { message = ex.Message, field = ex.Field });
        }
    }
}
