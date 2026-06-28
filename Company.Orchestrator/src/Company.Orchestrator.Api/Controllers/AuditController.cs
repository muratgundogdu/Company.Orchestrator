using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.DTOs.Audit;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/audit")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _service;

    public AuditController(IAuditService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(Permissions.AuditView)]
    [ProducesResponseType(typeof(Application.DTOs.Common.PagedResult<AuditLogListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? eventType,
        [FromQuery] string? username,
        [FromQuery] string? severity,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] bool? success,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);

        var filter = new AuditQueryFilter
        {
            FromUtc    = fromUtc,
            ToUtc      = toUtc,
            Category   = category,
            EventType  = eventType,
            Username   = username,
            Severity   = severity,
            EntityType = entityType,
            EntityId   = entityId,
            Success    = success,
            Search     = search,
            Page       = page,
            PageSize   = pageSize,
        };

        var result = await _service.QueryAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    [RequirePermission(Permissions.AuditView)]
    [ProducesResponseType(typeof(AuditSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? eventType,
        [FromQuery] string? username,
        [FromQuery] string? severity,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] bool? success,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var filter = new AuditQueryFilter
        {
            FromUtc    = fromUtc,
            ToUtc      = toUtc,
            Category   = category,
            EventType  = eventType,
            Username   = username,
            Severity   = severity,
            EntityType = entityType,
            EntityId   = entityId,
            Success    = success,
            Search     = search,
        };

        return Ok(await _service.GetSummaryAsync(filter, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AuditView)]
    [ProducesResponseType(typeof(AuditLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
