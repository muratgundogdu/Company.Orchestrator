using System.Text.Json;
using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Reads unread (or filtered) messages from an IMAP mailbox and stores
/// their metadata as a JSON array in a workflow variable.
///
/// Config keys:
///   folder            — IMAP folder to read (default: "INBOX")
///   unreadOnly        — bool (default: true)
///   subjectContains   — filter by subject substring (optional)
///   from              — filter by sender substring (optional)
///   since             — ISO-8601 date: only messages received after this date (optional)
///   hasAttachments    — bool: only messages that have attachments (optional)
///   maxCount          — max number of messages to return (default: 10)
///   outputVariable    — name of the workflow variable to store results (default: "receivedMails")
///
/// Output variable value: JSON array of objects with fields:
///   messageId, subject, from, receivedAt, folder, bodyPreview, attachmentFileNames[]
/// </summary>
public sealed class MailReceiveStepHandler : IStepHandler
{
    private readonly ILogger<MailReceiveStepHandler> _logger;
    public string HandlerType => "mail.receive";

    public MailReceiveStepHandler(ILogger<MailReceiveStepHandler> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var config = context.StepDefinition.Config;
        var query  = BuildQuery(config);

        var outputVar = config.GetValueOrDefault("outputVariable")?.ToString() ?? "receivedMails";

        _logger.LogInformation(
            "MailReceiveStepHandler: reading folder '{Folder}' (unreadOnly={Unread}, maxCount={Max})",
            query.Folder, query.UnreadOnly, query.MaxCount);

        var mail     = context.GetCapability<IMailCapability>();
        var messages = await mail.ReceiveAsync(query, cancellationToken);

        var dtos = messages.Select(m => new
        {
            messageId           = m.MessageId,
            subject             = m.Subject,
            from                = m.From,
            receivedAt          = m.ReceivedAt?.ToString("O"),
            folder              = m.Folder,
            bodyPreview         = m.BodyPreview,
            attachmentFileNames = m.AttachmentFileNames,
            hasAttachments      = m.AttachmentFileNames.Count > 0
        });

        var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = false });

        _logger.LogInformation(
            "MailReceiveStepHandler: {Count} message(s) stored in variable '{Var}'",
            messages.Count, outputVar);

        return StepResult.Ok(
            output: new Dictionary<string, object>
            {
                [outputVar]           = json,
                [$"{outputVar}_count"] = messages.Count
            },
            outputData: $"Received {messages.Count} message(s) from '{query.Folder}'");
    }

    internal static MailQuery BuildQuery(Dictionary<string, object> config)
    {
        var query = new MailQuery
        {
            Folder          = config.GetValueOrDefault("folder")?.ToString() ?? "INBOX",
            UnreadOnly      = !string.Equals(
                                  config.GetValueOrDefault("unreadOnly")?.ToString(), "false",
                                  StringComparison.OrdinalIgnoreCase),
            SubjectContains = NullIfEmpty(config.GetValueOrDefault("subjectContains")?.ToString()),
            FromContains    = NullIfEmpty(
                                  config.GetValueOrDefault("fromContains")?.ToString()
                                  ?? config.GetValueOrDefault("from")?.ToString()),
            LatestOnly      = ParseBool(config.GetValueOrDefault("latestOnly"), defaultValue: false),
            SortOrder       = config.GetValueOrDefault("sortOrder")?.ToString()?.ToLowerInvariant() ?? "newest",
            AttachmentNameContains = NullIfEmpty(config.GetValueOrDefault("attachmentNameContains")?.ToString()),
            AttachmentPattern      = NullIfEmpty(config.GetValueOrDefault("attachmentPattern")?.ToString()),
            MaxCount        = int.TryParse(
                                  config.GetValueOrDefault("maxCount")?.ToString(), out var mc) ? mc : 10,
        };

        if (config.TryGetValue("maxAttachmentCount", out var macRaw) && macRaw is not null
            && !string.IsNullOrWhiteSpace(macRaw.ToString())
            && int.TryParse(macRaw.ToString(), out var mac) && mac > 0)
        {
            query.MaxAttachmentCount = mac;
        }

        if (config.GetValueOrDefault("since") is { } sinceRaw
            && DateTime.TryParse(sinceRaw.ToString(), out var since))
            query.ReceivedAfter = since;

        if (string.Equals(
                config.GetValueOrDefault("hasAttachments")?.ToString(), "true",
                StringComparison.OrdinalIgnoreCase))
            query.HasAttachments = true;

        return query;
    }

    internal static bool ParseBool(object? raw, bool defaultValue)
    {
        if (raw is null) return defaultValue;
        if (raw is bool b) return b;
        var s = raw.ToString();
        if (string.Equals(s, "true",  StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
        return defaultValue;
    }

    internal static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
