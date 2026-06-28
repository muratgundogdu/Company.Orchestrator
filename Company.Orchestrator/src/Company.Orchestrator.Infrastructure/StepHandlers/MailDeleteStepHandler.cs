using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Deletes a mail message via IMailCapability.
///
/// Config keys:
///   messageId     — IMAP UID (supports {{variable}})
///   permanent     — bool (default: false — move to Deleted Items)
///   sourceFolder  — folder of the message (default: resolved)
/// </summary>
public sealed class MailDeleteStepHandler : IStepHandler
{
    private readonly ILogger<MailDeleteStepHandler> _logger;
    public string HandlerType => "mail.delete";

    public MailDeleteStepHandler(ILogger<MailDeleteStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var (messageId, folder, failure) = MailStepHandlerHelper.ResolveMessageReference(
            context, config, _logger, "mail.delete");
        if (failure is not null)
            return failure;

        var permanent = MailStepHandlerHelper.ParseBool(config.GetValueOrDefault("permanent"));

        _logger.LogInformation(
            "mail.delete: messageId={MessageId}, folder={Folder}, permanent={Permanent}",
            messageId, folder, permanent);

        var mail = context.GetCapability<IMailCapability>();
        var ok   = await mail.DeleteAsync(messageId, folder, permanent, cancellationToken);

        if (!ok)
            return StepResult.Fail($"mail.delete: failed to delete message {messageId} from '{folder}'.");

        return StepResult.Ok(
            output: new Dictionary<string, object> { ["mailDeleted"] = true },
            outputData: permanent
                ? $"Message {messageId} permanently deleted from '{folder}'"
                : $"Message {messageId} moved to Deleted Items from '{folder}'");
    }
}
