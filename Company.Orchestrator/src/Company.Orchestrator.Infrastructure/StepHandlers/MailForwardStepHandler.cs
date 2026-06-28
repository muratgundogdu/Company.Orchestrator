using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Forwards an existing mail message via IMailCapability.
///
/// Config keys:
///   messageId           — IMAP UID (supports {{variable}})
///   to                  — recipient address(es), comma-separated (required)
///   cc                  — comma-separated CC list (optional)
///   bcc                 — comma-separated BCC list (optional)
///   body                — forward preamble (supports {{variable}})
///   includeAttachments  — bool (default: true)
///   isHtml              — bool (default: false)
///   sourceFolder        — folder of the original message (default: resolved)
/// </summary>
public sealed class MailForwardStepHandler : IStepHandler
{
    private readonly ILogger<MailForwardStepHandler> _logger;
    public string HandlerType => "mail.forward";

    public MailForwardStepHandler(ILogger<MailForwardStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("to", out var toRaw) || toRaw is null)
            return StepResult.Fail("mail.forward: 'to' is required.");

        var (messageId, folder, failure) = MailStepHandlerHelper.ResolveMessageReference(
            context, config, _logger, "mail.forward");
        if (failure is not null)
            return failure;

        var to = context.Interpolate(toRaw.ToString()!);
        var body = context.Interpolate(config.GetValueOrDefault("body")?.ToString() ?? "");
        var includeAttachments = MailStepHandlerHelper.ParseBool(
            config.GetValueOrDefault("includeAttachments"), defaultValue: true);
        var isHtml = MailStepHandlerHelper.ParseBool(config.GetValueOrDefault("isHtml"));

        var request = new MailForwardRequest
        {
            MessageId          = messageId,
            SourceFolder         = folder,
            To                   = to,
            Body                 = body,
            IncludeAttachments   = includeAttachments,
            IsHtml               = isHtml,
        };

        var ccRaw = config.GetValueOrDefault("cc")?.ToString();
        if (!string.IsNullOrWhiteSpace(ccRaw))
        {
            foreach (var addr in ccRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                request.Cc.Add(context.Interpolate(addr.Trim()));
        }

        var bccRaw = config.GetValueOrDefault("bcc")?.ToString();
        if (!string.IsNullOrWhiteSpace(bccRaw))
        {
            foreach (var addr in bccRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                request.Bcc.Add(context.Interpolate(addr.Trim()));
        }

        _logger.LogInformation(
            "mail.forward: messageId={MessageId}, to={To}, includeAttachments={IncludeAttachments}",
            messageId, to, includeAttachments);

        var mail   = context.GetCapability<IMailCapability>();
        var result = await mail.ForwardAsync(request, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["forwardMessageId"] = result.ForwardMessageId,
            },
            outputData: $"Message {messageId} forwarded to {to}");
    }
}
