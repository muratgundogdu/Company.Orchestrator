using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workers")]
[Produces("application/json")]
public class WorkersController : ControllerBase
{
    private readonly IWorkerService _service;

    public WorkersController(IWorkerService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(Permissions.WorkerView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var workers = await _service.GetAllAsync(cancellationToken);
        return Ok(workers);
    }

    [HttpGet("summary")]
    [RequirePermission(Permissions.WorkerView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        var summary = await _service.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpGet("{workerId}")]
    [RequirePermission(Permissions.WorkerView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByWorkerId(string workerId, CancellationToken cancellationToken = default)
    {
        var worker = await _service.GetByWorkerIdAsync(workerId, cancellationToken);
        return worker is null ? NotFound() : Ok(worker);
    }
}
