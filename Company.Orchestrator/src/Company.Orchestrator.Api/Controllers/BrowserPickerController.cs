using Company.Orchestrator.Api.Authorization;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Company.Orchestrator.Api.Controllers;

/// <summary>
/// Dev-only helper for picking CSS selectors from a live headed Chromium window.
/// </summary>
[ApiController]
[Authorize]
[Route("api/browser-picker")]
public class BrowserPickerController : ControllerBase
{
    private readonly IBrowserPickerService _picker;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BrowserPickerController> _logger;

    public BrowserPickerController(
        IBrowserPickerService picker,
        IWebHostEnvironment env,
        ILogger<BrowserPickerController> logger)
    {
        _picker = picker;
        _env    = env;
        _logger = logger;
    }

    [HttpPost("start")]
    [RequirePermission(Permissions.WorkflowEdit)]
    public async Task<IActionResult> Start([FromBody] BrowserPickerStartRequest body, CancellationToken cancellationToken)
    {
        if (!IsDev()) return NotFound();

        if (string.IsNullOrWhiteSpace(body.Url))
            return BadRequest(new { message = "url is required." });

        try
        {
            var result = await _picker.StartAsync(body.Url.Trim(), cancellationToken);
            return Ok(new { sessionId = result.SessionId, status = result.Status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BrowserPicker start failed for {Url}", body.Url);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("stop")]
    [RequirePermission(Permissions.WorkflowEdit)]
    public async Task<IActionResult> Stop([FromBody] BrowserPickerStopRequest body, CancellationToken cancellationToken)
    {
        if (!IsDev()) return NotFound();

        if (body.SessionId == Guid.Empty)
            return BadRequest(new { message = "sessionId is required." });

        await _picker.StopAsync(body.SessionId, cancellationToken);
        return Ok(new { status = "stopped" });
    }

    [HttpGet("{sessionId:guid}/selected")]
    [RequirePermission(Permissions.WorkflowEdit)]
    public IActionResult GetSelected(Guid sessionId)
    {
        if (!IsDev()) return NotFound();

        var selected = _picker.GetSelected(sessionId);
        if (selected is null)
            return NotFound(new { message = "Picker session not found." });

        static object MapElement(BrowserPickerSelectedElement e) => new
        {
            tagName   = e.TagName,
            text      = e.Text,
            id        = e.Id,
            name      = e.Name,
            ariaLabel = e.AriaLabel,
            href      = e.Href,
        };

        return Ok(new
        {
            primarySelector = selected.PrimarySelector,
            selector        = selected.PrimarySelector,
            candidates      = selected.Candidates.Select(c => new
            {
                selector   = c.Selector,
                strategy   = c.Strategy,
                confidence = c.Confidence,
                matchCount = c.MatchCount,
                reason     = c.Reason,
            }),
            selectedElement            = MapElement(selected.SelectedElement),
            originalClickedElement     = MapElement(selected.OriginalClickedElement),
            resolvedClickableElement   = MapElement(selected.ResolvedClickableElement),
            tagName   = selected.SelectedElement.TagName,
            text      = selected.SelectedElement.Text,
            id        = selected.SelectedElement.Id,
            name      = selected.SelectedElement.Name,
            ariaLabel = selected.SelectedElement.AriaLabel,
            href      = selected.SelectedElement.Href,
        });
    }

    private bool IsDev() => _env.IsDevelopment();
}

public sealed class BrowserPickerStartRequest
{
    public string Url { get; set; } = string.Empty;
}

public sealed class BrowserPickerStopRequest
{
    public Guid SessionId { get; set; }
}
