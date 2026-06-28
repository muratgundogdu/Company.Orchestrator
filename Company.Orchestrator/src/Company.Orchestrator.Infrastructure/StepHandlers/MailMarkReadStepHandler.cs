using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Marks a mail message read or unread on the IMAP server.
///
/// Config keys:
///   messageId  — IMAP UID string (supports {{variable}})
///   isRead     — bool (default: true)
///   sourceFolder — folder the message lives in (default: resolved)
/// </summary>
public sealed class MailMarkReadStepHandler : IStepHandler
{
    private readonly ILogger<MailMarkReadStepHandler> _logger;
    public string HandlerType => "mail.mark";

    public MailMarkReadStepHandler(ILogger<MailMarkReadStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var (messageId, folder, failure) = MailStepHandlerHelper.ResolveMessageReference(
            context, config, _logger, "mail.mark");
        if (failure is not null)
            return failure;

        var isRead = MailStepHandlerHelper.ParseBool(config.GetValueOrDefault("isRead"), defaultValue: true);

        _logger.LogInformation(
            "mail.mark: messageId={MessageId}, folder={Folder}, isRead={IsRead}",
            messageId, folder, isRead);

        var mail = context.GetCapability<IMailCapability>();
        var ok   = await mail.MarkReadAsync(messageId, isRead, folder, cancellationToken);

        if (!ok)
            return StepResult.Fail($"mail.mark: failed to mark message {messageId} as {(isRead ? "read" : "unread")}.");

        return StepResult.Ok(
            output: new Dictionary<string, object> { ["mailReadState"] = isRead ? "read" : "unread" },
            outputData: $"Message {messageId} in '{folder}' marked as {(isRead ? "read" : "unread")}");
    }
}

/// <summary>Legacy alias for workflows using mail.mark-read (always marks as read).</summary>
public sealed class MailMarkReadLegacyStepHandler : IStepHandler
{
    private readonly ILogger<MailMarkReadLegacyStepHandler> _logger;
    public string HandlerType => "mail.mark-read";

    public MailMarkReadLegacyStepHandler(ILogger<MailMarkReadLegacyStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var (messageId, folder, failure) = MailStepHandlerHelper.ResolveMessageReference(
            context, config, _logger, "mail.mark-read");
        if (failure is not null)
            return failure;

        _logger.LogInformation(
            "mail.mark-read: messageId={MessageId}, folder={Folder}",
            messageId, folder);

        var mail = context.GetCapability<IMailCapability>();
        var ok   = await mail.MarkReadAsync(messageId, isRead: true, folder, cancellationToken);

        if (!ok)
            return StepResult.Fail($"mail.mark-read: failed to mark message {messageId} as read.");

        return StepResult.Ok(
            output: new Dictionary<string, object> { ["mailReadState"] = "read" },
            outputData: $"Message {messageId} in '{folder}' marked as read");
    }
}
