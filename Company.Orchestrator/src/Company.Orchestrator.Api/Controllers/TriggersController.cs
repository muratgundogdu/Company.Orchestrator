using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Trigger;
using Company.Orchestrator.Application.Triggers;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/triggers")]
[Produces("application/json")]
public class TriggersController : ControllerBase
{
    private readonly ITriggerService _service;

    public TriggersController(ITriggerService service)
    {
        _service = service;
    }

    // ------------------------------------------------------------------ //
    // GET /api/triggers?page=1&pageSize=20
    // ------------------------------------------------------------------ //

    [HttpGet]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(typeof(PagedResult<TriggerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    // ------------------------------------------------------------------ //
    // POST /api/triggers
    // ------------------------------------------------------------------ //

    [HttpPost]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(typeof(TriggerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var dto = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ------------------------------------------------------------------ //
    // GET /api/triggers/{id}
    // ------------------------------------------------------------------ //

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(typeof(TriggerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _service.GetByIdAsync(id, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    // ------------------------------------------------------------------ //
    // PUT /api/triggers/{id}
    // ------------------------------------------------------------------ //

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(typeof(TriggerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = await _service.UpdateAsync(id, request, cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    // ------------------------------------------------------------------ //
    // POST /api/triggers/{id}/activate
    // ------------------------------------------------------------------ //

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(
        Guid id, CancellationToken cancellationToken = default)
    {
        var ok = await _service.ActivateAsync(id, cancellationToken);
        return ok ? NoContent() : NotFound();
    }

    // ------------------------------------------------------------------ //
    // POST /api/triggers/{id}/deactivate
    // ------------------------------------------------------------------ //

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(Permissions.WorkflowEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        Guid id, CancellationToken cancellationToken = default)
    {
        var ok = await _service.DeactivateAsync(id, cancellationToken);
        return ok ? NoContent() : NotFound();
    }

    // ------------------------------------------------------------------ //
    // GET /api/triggers/{id}/events?page=1&pageSize=50
    // ------------------------------------------------------------------ //

    [HttpGet("{id:guid}/events")]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(typeof(PagedResult<TriggerEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvents(
        Guid id,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var trigger = await _service.GetByIdAsync(id, cancellationToken);
        if (trigger is null) return NotFound();

        var result = await _service.GetEventsAsync(id, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
