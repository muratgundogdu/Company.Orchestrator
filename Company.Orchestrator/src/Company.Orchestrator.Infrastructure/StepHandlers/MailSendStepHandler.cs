using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Sends an email via IMailCapability.
/// Replaces the old EmailStepHandler — completely decoupled from SMTP details.
///
/// Config keys:
///   to          — recipient address(es), comma-separated (supports {{variable}})
///   subject     — email subject (supports {{variable}})
///   body        — email body (supports {{variable}})
///   isHtml      — bool (default: false)
///   cc          — comma-separated CC list (optional)
///   bcc         — comma-separated BCC list (optional)
///   from        — override sender address (optional)
///   attachments — comma-separated artifact names to attach (optional)
/// </summary>
public sealed class MailSendStepHandler : IStepHandler
{
    private readonly ILogger<MailSendStepHandler> _logger;
    public string HandlerType => "mail.send";

    public MailSendStepHandler(ILogger<MailSendStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("to", out var toRaw) || toRaw is null)
            return StepResult.Fail("MailSendStepHandler: 'to' is required.");

        var to = context.Interpolate(toRaw.ToString()!);
        var subject = context.Interpolate(config.GetValueOrDefault("subject")?.ToString() ?? "(no subject)");
        var body = context.Interpolate(config.GetValueOrDefault("body")?.ToString() ?? "");
        var isHtml = config.GetValueOrDefault("isHtml")?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        var message = new MailMessage
        {
            To     = to,
            Subject = subject,
            Body   = body,
            IsHtml = isHtml,
            From   = config.GetValueOrDefault("from") is { } f ? context.Interpolate(f.ToString()!) : null
        };

        // CC list
        var ccRaw = config.GetValueOrDefault("cc")?.ToString();
        if (!string.IsNullOrEmpty(ccRaw))
            foreach (var addr in ccRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                message.Cc.Add(addr.Trim());

        // BCC list
        var bccRaw = config.GetValueOrDefault("bcc")?.ToString();
        if (!string.IsNullOrEmpty(bccRaw))
            foreach (var addr in bccRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                message.Bcc.Add(addr.Trim());

        // Attachments from context artifacts
        var attachmentsRaw = config.GetValueOrDefault("attachments")?.ToString();
        if (!string.IsNullOrEmpty(attachmentsRaw))
        {
            foreach (var name in attachmentsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = name.Trim();
                if (context.HasArtifact(trimmed))
                    message.Attachments.Add(context.GetArtifact(trimmed));
                else
                    _logger.LogWarning("MailSendStepHandler: attachment artifact '{Name}' not found in context", trimmed);
            }
        }

        _logger.LogInformation("MailSendStepHandler: sending mail to {To}, subject '{Subject}'", to, subject);
        var mail = context.GetCapability<IMailCapability>();
        await mail.SendAsync(message, cancellationToken);

        return StepResult.Ok(outputData: $"Email sent to {to} with subject '{subject}'");
    }
}
