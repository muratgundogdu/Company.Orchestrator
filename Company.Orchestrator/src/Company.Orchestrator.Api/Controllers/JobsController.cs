using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.DTOs.Job;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/jobs")]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly IJobService _service;

    public JobsController(IJobService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(Permissions.JobView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? instanceId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, instanceId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.JobView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/retry")]
    [RequirePermission(Permissions.JobRetry)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken = default)
    {
        var retried = await _service.RetryAsync(id, cancellationToken);
        return retried ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.JobCancel)]
    [ProducesResponseType(typeof(CancelJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelJobRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.CancelAsync(id, request ?? new CancelJobRequest(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (JobCancellationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
