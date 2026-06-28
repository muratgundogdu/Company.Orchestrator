using System.Net.Sockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Company.Orchestrator.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(ILogger<DiagnosticsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Probes a raw TCP connection to imap.gmail.com:993 with a 10-second timeout.
    /// Use this to verify network reachability before blaming MailKit/IMAP auth.
    /// </summary>
    [HttpGet("imap-tcp")]
    public async Task<IActionResult> ImapTcpProbeAsync(CancellationToken cancellationToken)
    {
        const string host = "imap.gmail.com";
        const int    port = 993;

        _logger.LogInformation("DiagnosticsController: probing TCP {Host}:{Port}", host, port);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cts.Token);

            _logger.LogInformation(
                "DiagnosticsController: TCP connection to {Host}:{Port} successful", host, port);

            return Ok(new
            {
                host,
                port,
                result  = "TCP connection successful",
                success = true,
                probed  = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DiagnosticsController: TCP connection to {Host}:{Port} failed", host, port);

            return StatusCode(503, new
            {
                host,
                port,
                result    = $"TCP connection failed: {ex.Message}",
                exception = ex.GetType().Name,
                success   = false,
                probed    = DateTime.UtcNow
            });
        }
    }
}
