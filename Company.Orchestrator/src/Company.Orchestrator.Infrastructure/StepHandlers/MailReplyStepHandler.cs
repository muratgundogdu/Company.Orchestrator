using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Replies to an existing mail message via IMailCapability.
///
/// Config keys:
///   messageId            — IMAP UID (supports {{variable}})
///   body                 — reply body (supports {{variable}})
///   replyAll             — bool (default: false)
///   includeOriginalBody  — bool (default: true)
///   isHtml               — bool (default: false)
///   attachments          — comma-separated artifact names (optional)
///   sourceFolder         — folder of the original message (default: resolved)
/// </summary>
public sealed class MailReplyStepHandler : IStepHandler
{
    private readonly ILogger<MailReplyStepHandler> _logger;
    public string HandlerType => "mail.reply";

    public MailReplyStepHandler(ILogger<MailReplyStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var (messageId, folder, failure) = MailStepHandlerHelper.ResolveMessageReference(
            context, config, _logger, "mail.reply");
        if (failure is not null)
            return failure;

        var body = context.Interpolate(config.GetValueOrDefault("body")?.ToString() ?? "");
        var replyAll = MailStepHandlerHelper.ParseBool(config.GetValueOrDefault("replyAll"));
        var includeOriginal = MailStepHandlerHelper.ParseBool(
            config.GetValueOrDefault("includeOriginalBody"), defaultValue: true);
        var isHtml = MailStepHandlerHelper.ParseBool(config.GetValueOrDefault("isHtml"));
        var attachments = MailStepHandlerHelper.ResolveAttachmentArtifacts(
            context, config, _logger, "mail.reply");

        _logger.LogInformation(
            "mail.reply: messageId={MessageId}, replyAll={ReplyAll}, attachmentCount={AttachmentCount}",
            messageId, replyAll, attachments.Count);

        var request = new MailReplyRequest
        {
            MessageId           = messageId,
            SourceFolder        = folder,
            Body                = body,
            ReplyAll            = replyAll,
            IncludeOriginalBody = includeOriginal,
            IsHtml              = isHtml,
            Attachments         = attachments,
        };

        var mail   = context.GetCapability<IMailCapability>();
        var result = await mail.ReplyAsync(request, cancellationToken);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["replyMessageId"]       = result.ReplyMessageId,
                ["replyConversationId"] = result.ReplyConversationId,
            },
            outputData: $"Reply sent for message {messageId}");
    }
}
