using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.Common.Exceptions;
using Company.Orchestrator.Application.DTOs.Credential;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/credentials")]
[Produces("application/json")]
public class CredentialsController : ControllerBase
{
    private readonly ICredentialService _service;

    public CredentialsController(ICredentialService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(Permissions.CredentialView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CredentialView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.CredentialManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (FieldValidationException ex)
        {
            return BadRequest(new { message = ex.Message, field = ex.Field });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CredentialManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (FieldValidationException ex)
        {
            return BadRequest(new { message = ex.Message, field = ex.Field });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CredentialManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/test")]
    [RequirePermission(Permissions.CredentialManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken = default)
    {
        var credential = await _service.GetByIdAsync(id, cancellationToken);
        if (credential is null)
            return NotFound();

        var result = await _service.TestAsync(id, cancellationToken);
        return Ok(result);
    }
}
