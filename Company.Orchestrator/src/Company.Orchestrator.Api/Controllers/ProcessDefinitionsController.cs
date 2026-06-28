using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.Common.Exceptions;
using Company.Orchestrator.Application.DTOs.ProcessDefinition;
using Company.Orchestrator.Application.DTOs.ProcessVersion;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/process-definitions")]
[Produces("application/json")]
public class ProcessDefinitionsController : ControllerBase
{
    private readonly IProcessDefinitionService _service;

    public ProcessDefinitionsController(IProcessDefinitionService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProcessDefinitionRequest request,
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
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProcessDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{definitionId:guid}/versions")]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(Guid definitionId, CancellationToken cancellationToken = default)
    {
        var versions = await _service.GetVersionsAsync(definitionId, cancellationToken);
        return Ok(versions);
    }

    [HttpPost("{definitionId:guid}/versions")]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVersion(
        Guid definitionId,
        [FromBody] CreateProcessVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var version = await _service.CreateVersionAsync(definitionId, request, cancellationToken);
        return CreatedAtAction(nameof(GetVersions), new { definitionId }, version);
    }

    [HttpPost("{definitionId:guid}/versions/{versionId:guid}/publish")]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishVersion(
        Guid definitionId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var version = await _service.PublishVersionAsync(definitionId, versionId, cancellationToken);
        return version is null ? NotFound() : Ok(version);
    }
}
