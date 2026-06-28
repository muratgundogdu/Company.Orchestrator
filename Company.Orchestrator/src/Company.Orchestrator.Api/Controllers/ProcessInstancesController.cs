using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.DTOs.ProcessInstance;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/process-instances")]
[Produces("application/json")]
public class ProcessInstancesController : ControllerBase
{
    private readonly IProcessInstanceService _service;

    public ProcessInstancesController(IProcessInstanceService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? definitionId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, definitionId, cancellationToken);
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

    [HttpPost("start")]
    [RequirePermission(Permissions.WorkflowExecute)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start(
        [FromBody] StartProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.StartAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.JobCancel)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
    {
        var cancelled = await _service.CancelAsync(id, cancellationToken);
        return cancelled ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/logs")]
    [RequirePermission(Permissions.JobView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs(Guid id, CancellationToken cancellationToken = default)
    {
        var logs = await _service.GetLogsAsync(id, cancellationToken);
        return Ok(logs);
    }
}
