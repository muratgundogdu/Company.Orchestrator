using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Moves a mail message from one IMAP folder to another.
///
/// Config keys:
///   messageId      — IMAP UID string (supports {{variable}})
///   targetFolder   — destination folder name (required, supports {{variable}})
///   sourceFolder   — source folder (default: resolved from context)
/// </summary>
public sealed class MailMoveStepHandler : IStepHandler
{
    private readonly ILogger<MailMoveStepHandler> _logger;
    public string HandlerType => "mail.move";

    public MailMoveStepHandler(ILogger<MailMoveStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        if (!config.TryGetValue("targetFolder", out var targetRaw) || targetRaw is null)
            return StepResult.Fail("mail.move: 'targetFolder' is required.");

        var (messageId, sourceFolder, failure) = MailStepHandlerHelper.ResolveMessageReference(
            context, config, _logger, "mail.move");
        if (failure is not null)
            return failure;

        var targetFolder = context.Interpolate(targetRaw.ToString()!);

        _logger.LogInformation(
            "mail.move: messageId={MessageId}, from '{Src}' → '{Dest}'",
            messageId, sourceFolder, targetFolder);

        var mail = context.GetCapability<IMailCapability>();
        var ok   = await mail.MoveAsync(messageId, targetFolder, sourceFolder, cancellationToken);

        if (!ok)
            return StepResult.Fail(
                $"mail.move: failed to move message {messageId} from '{sourceFolder}' to '{targetFolder}'.");

        return StepResult.Ok(
            output: new Dictionary<string, object> { ["movedFolder"] = targetFolder },
            outputData: $"Message {messageId} moved from '{sourceFolder}' → '{targetFolder}'");
    }
}
