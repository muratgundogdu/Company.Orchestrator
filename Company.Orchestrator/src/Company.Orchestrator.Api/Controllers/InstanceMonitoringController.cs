using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[Route("api/internal/instance-monitoring")]
public sealed class InstanceMonitoringController : ControllerBase
{
    private readonly IInstanceMonitoringPublisher _publisher;
    private readonly IConfiguration _configuration;

    public InstanceMonitoringController(
        IInstanceMonitoringPublisher publisher,
        IConfiguration configuration)
    {
        _publisher     = publisher;
        _configuration = configuration;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish(
        [FromBody] InstanceMonitoringEnvelope envelope,
        [FromHeader(Name = "X-Instance-Monitoring-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        var expected = _configuration["InstanceMonitoring:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected) ||
            !string.Equals(expected, apiKey, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        await _publisher.PublishEnvelopeAsync(envelope, cancellationToken);
        return NoContent();
    }
}
