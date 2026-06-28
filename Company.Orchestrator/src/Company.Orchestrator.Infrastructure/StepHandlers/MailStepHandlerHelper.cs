using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class MailStepHandlerHelper
{
    internal static (string MessageId, string Folder, StepResult? Failure) ResolveMessageReference(
        WorkflowContext context,
        IReadOnlyDictionary<string, object> config,
        ILogger logger,
        string stepType)
    {
        var explicitMessageId = config.TryGetValue("messageId", out var midRaw) && midRaw is not null
            ? context.Interpolate(midRaw.ToString()!)
            : null;

        var explicitFolder = config.GetValueOrDefault("sourceFolder")?.ToString()?.Trim()
            ?? config.GetValueOrDefault("folder")?.ToString()?.Trim();

        var sourceVar    = config.GetValueOrDefault("sourceVariable")?.ToString()?.Trim() ?? "receivedMails";
        var messageIndex = ParseInt(config.GetValueOrDefault("messageIndex"), defaultValue: 0);

        var resolved = MailVariableHelper.ResolveMessageForGetBody(
            context.Variables,
            explicitMessageId,
            explicitFolder,
            sourceVar,
            messageIndex,
            msg => logger.LogDebug("{StepType}: {Message}", stepType, msg),
            out var resolveError);

        if (resolved is null)
        {
            return (string.Empty, "INBOX", StepResult.Fail($"{stepType}: {resolveError}"));
        }

        return (resolved.Value.MessageId, resolved.Value.Folder, null);
    }

    internal static List<string> ParseAttachmentNames(
        WorkflowContext context,
        IReadOnlyDictionary<string, object> config,
        ILogger logger,
        string stepType)
    {
        var result = new List<string>();
        var raw = config.GetValueOrDefault("attachments")?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (var name in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = context.Interpolate(name.Trim());
            if (context.HasArtifact(trimmed))
                result.Add(trimmed);
            else
                logger.LogWarning("{StepType}: attachment artifact '{Name}' not found in context", stepType, trimmed);
        }

        return result;
    }

    internal static List<Company.Orchestrator.Application.Artifacts.ArtifactReference> ResolveAttachmentArtifacts(
        WorkflowContext context,
        IReadOnlyDictionary<string, object> config,
        ILogger logger,
        string stepType)
    {
        var names = ParseAttachmentNames(context, config, logger, stepType);
        return names.Select(context.GetArtifact).ToList();
    }

    internal static bool ParseBool(object? value, bool defaultValue = false)
    {
        if (value is null) return defaultValue;
        if (value is bool b) return b;
        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        return text.Equals("true", StringComparison.OrdinalIgnoreCase)
            || text.Equals("1", StringComparison.OrdinalIgnoreCase)
            || text.Equals("yes", StringComparison.OrdinalIgnoreCase);
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
