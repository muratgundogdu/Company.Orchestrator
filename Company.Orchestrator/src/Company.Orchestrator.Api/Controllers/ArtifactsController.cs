using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.DTOs.Artifact;
using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/artifacts")]
[Produces("application/json")]
public class ArtifactsController : ControllerBase
{
    private readonly IArtifactRepository _repository;
    private readonly IArtifactStore _store;

    public ArtifactsController(IArtifactRepository repository, IArtifactStore store)
    {
        _repository = repository;
        _store      = store;
    }

    // ------------------------------------------------------------------ //
    // GET /api/artifacts?page=1&pageSize=20
    // ------------------------------------------------------------------ //

    [HttpGet]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(typeof(PagedResult<ArtifactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _repository.GetAllAsync(page, pageSize, cancellationToken);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var result = new PagedResult<ArtifactDto>
        {
            Items      = items.Select(a => ArtifactDto.FromEntity(a, baseUrl)).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
        return Ok(result);
    }

    // ------------------------------------------------------------------ //
    // GET /api/artifacts/{id}
    // ------------------------------------------------------------------ //

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(typeof(ArtifactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var artifact = await _repository.GetByIdAsync(id, cancellationToken);
        if (artifact is null) return NotFound(new { message = $"Artifact {id} not found." });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(ArtifactDto.FromEntity(artifact, baseUrl));
    }

    // ------------------------------------------------------------------ //
    // GET /api/artifacts/process-instance/{processInstanceId}
    // ------------------------------------------------------------------ //

    [HttpGet("process-instance/{processInstanceId:guid}")]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(typeof(IReadOnlyList<ArtifactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProcessInstance(
        Guid processInstanceId,
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _repository.GetByProcessInstanceAsync(processInstanceId, cancellationToken);
        var baseUrl   = $"{Request.Scheme}://{Request.Host}";
        return Ok(artifacts.Select(a => ArtifactDto.FromEntity(a, baseUrl)));
    }

    // ------------------------------------------------------------------ //
    // GET /api/artifacts/{id}/download
    // ------------------------------------------------------------------ //

    [HttpGet("{id:guid}/download")]
    [RequirePermission(Permissions.WorkflowView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken = default)
    {
        var artifact = await _repository.GetByIdAsync(id, cancellationToken);
        if (artifact is null)
            return NotFound(new { message = $"Artifact {id} not found." });

        if (!await _store.ExistsAsync(artifact.StoragePath, cancellationToken))
            return Conflict(new
            {
                message     = "Artifact metadata exists but content file is missing from the store.",
                artifactId  = id,
                storagePath = artifact.StoragePath
            });

        var stream = await _store.OpenReadAsync(artifact.StoragePath, cancellationToken);

        // Let ASP.NET Core manage stream lifetime — it disposes after response.
        return File(stream, artifact.ContentType, artifact.Name);
    }
}
