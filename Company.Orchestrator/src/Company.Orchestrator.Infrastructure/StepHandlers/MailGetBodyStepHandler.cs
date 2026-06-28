using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Fetches the full body of a message previously received via mail.receive
/// or mail.read-attachments.
///
/// Message resolution order:
///   1. explicit messageId config
///   2. sourceVariable JSON from mail.receive (default: receivedMails)
///   3. selectedMessageId from mail.read-attachments
///   4. selectedMessageUid from mail.read-attachments
/// </summary>
public sealed class MailGetBodyStepHandler : IStepHandler
{
    private readonly ILogger<MailGetBodyStepHandler> _logger;

    public string HandlerType => "mail.get-body";

    public MailGetBodyStepHandler(ILogger<MailGetBodyStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;

        var bodyType = config.GetValueOrDefault("bodyType")?.ToString()?.Trim().ToLowerInvariant() ?? "text";
        if (bodyType is not ("text" or "html"))
            return StepResult.Fail("mail.get-body: 'bodyType' must be 'text' or 'html'.");

        var outputVar = config.GetValueOrDefault("outputVariable")?.ToString()?.Trim();
        if (string.IsNullOrEmpty(outputVar))
            return StepResult.Fail("mail.get-body: 'outputVariable' is required.");

        var messageIndex = ParseInt(config.GetValueOrDefault("messageIndex"), defaultValue: 0);
        var folder       = config.GetValueOrDefault("folder")?.ToString()?.Trim();
        var messageId    = config.TryGetValue("messageId", out var midRaw) && midRaw is not null
            ? context.Interpolate(midRaw.ToString()!)
            : null;

        var sourceVar = config.GetValueOrDefault("sourceVariable")?.ToString()?.Trim() ?? "receivedMails";

        LogWorkflowVariables(context.Variables);

        var resolved = MailVariableHelper.ResolveMessageForGetBody(
            context.Variables,
            messageId,
            folder,
            sourceVar,
            messageIndex,
            msg => _logger.LogInformation("mail.get-body: {Message}", msg),
            out var resolveError);

        if (resolved is null)
            return StepResult.Fail($"mail.get-body: {resolveError}");

        messageId = resolved.Value.MessageId;
        folder    = resolved.Value.Folder;

        _logger.LogInformation(
            "MailGetBody resolved message: source={Source} messageId={MessageId} uid={Uid} folder={Folder}",
            resolved.Value.Source,
            messageId,
            resolved.Value.Uid ?? messageId,
            folder);

        _logger.LogInformation(
            "mail.get-body: fetching {BodyType} body for messageId={MessageId}, folder='{Folder}' → '{OutputVar}'",
            bodyType, messageId, folder, outputVar);

        var mail = context.GetCapability<IMailCapability>();
        var body = await mail.GetMessageBodyAsync(messageId, folder, bodyType, cancellationToken);

        _logger.LogInformation(
            "mail.get-body: retrieved {Length} character(s) for messageId={MessageId}",
            body.Length, messageId);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                [outputVar]             = body,
                [$"{outputVar}_length"] = body.Length,
            },
            outputData: $"Fetched {bodyType} body ({body.Length} chars) from message {messageId}");
    }

    private void LogWorkflowVariables(Dictionary<string, object> variables)
    {
        if (variables.Count == 0)
        {
            _logger.LogInformation("mail.get-body: workflow variables visible to this step: (none)");
            return;
        }

        var entries = variables
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv =>
            {
                var value = MailVariableHelper.GetVariableStringValue(kv.Value) ?? "(null)";
                if (value.Length > 200)
                    value = value[..200] + "…";
                return $"{kv.Key}={value}";
            });

        _logger.LogInformation(
            "mail.get-body: workflow variables visible to this step ({Count}): {Variables}",
            variables.Count,
            string.Join(" | ", entries));
    }

    private static int ParseInt(object? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
    }
}
