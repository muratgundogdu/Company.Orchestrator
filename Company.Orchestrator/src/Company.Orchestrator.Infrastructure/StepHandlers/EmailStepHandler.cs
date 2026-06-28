using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Legacy simulation handler kept for backward compatibility with workflows using type "Email".
/// For new workflows use "mail.send" (MailSendStepHandler + IMailCapability).
/// </summary>
public class EmailStepHandler : IStepHandler
{
    private readonly ILogger<EmailStepHandler> _logger;

    public string HandlerType => "Email";

    public EmailStepHandler(ILogger<EmailStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("to", out var toRaw) || toRaw is null)
            return StepResult.Fail("Email 'to' address is required.");

        var to = context.Interpolate(toRaw.ToString()!);
        var subject = context.Interpolate(config.GetValueOrDefault("subject")?.ToString() ?? "(no subject)");
        var body = context.Interpolate(config.GetValueOrDefault("body")?.ToString() ?? "");

        _logger.LogInformation("[Simulated] Sending email to {To} | Subject: {Subject}", to, subject);
        await Task.Delay(100, cancellationToken);
        _logger.LogInformation("[Simulated] Email sent successfully to {To}", to);

        return StepResult.Ok(
            output: new Dictionary<string, object> { ["emailSent"] = true, ["recipient"] = to },
            outputData: $"Email sent to {to}");
    }
}
