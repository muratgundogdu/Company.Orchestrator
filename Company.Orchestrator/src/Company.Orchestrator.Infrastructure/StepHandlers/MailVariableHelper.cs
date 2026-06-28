using System.Text.Json;
using System.Text.RegularExpressions;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

internal static class MailVariableHelper
{
    /// <summary>Reads a workflow variable as a trimmed string (handles JsonElement values from JSON deserialization).</summary>
    internal static string? GetVariableStringValue(object? raw)
    {
        if (raw is null) return null;

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => NullIfWhiteSpace(je.GetString()),
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => null,
                _                     => NullIfWhiteSpace(je.ToString()),
            };
        }

        return NullIfWhiteSpace(raw.ToString());
    }

    internal static bool TryGetVariableString(
        Dictionary<string, object> variables,
        string key,
        out string? value)
    {
        value = null;
        if (!variables.TryGetValue(key, out var raw))
            return false;

        value = GetVariableStringValue(raw);
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>True when an explicit messageId config value is usable (not blank / unresolved {{ }}).</summary>
    internal static bool IsUsableExplicitMessageId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId)) return false;
        var trimmed = messageId.Trim();
        return !trimmed.Contains("{{", StringComparison.Ordinal);
    }

    /// <summary>Strips optional {{ }} wrappers from a variable name.</summary>
    internal static string NormalizeVariableName(string raw)
    {
        var name = raw.Trim();
        if (name.StartsWith("{{", StringComparison.Ordinal) && name.EndsWith("}}", StringComparison.Ordinal))
            name = name[2..^2].Trim();
        return name;
    }

    /// <summary>
    /// Resolves IMAP messageId and folder from a mail.receive JSON variable value.
    /// Accepts a JSON array (uses messageIndex) or a single message object.
    /// </summary>
    internal static (string MessageId, string Folder)? ResolveMessageReference(
        object? raw,
        int messageIndex,
        out string? error)
    {
        error = null;
        if (raw is null)
        {
            error = "value is null";
            return null;
        }

        try
        {
            if (raw is JsonElement je)
                return ResolveFromJsonElement(je, messageIndex, out error);

            var json = raw.ToString();
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "value is empty";
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            return ResolveFromJsonElement(doc.RootElement, messageIndex, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private static (string MessageId, string Folder)? ResolveFromJsonElement(
        JsonElement root,
        int messageIndex,
        out string? error)
    {
        error = null;

        JsonElement messageEl;
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (root.GetArrayLength() == 0)
            {
                error = "array is empty";
                return null;
            }

            if (messageIndex < 0 || messageIndex >= root.GetArrayLength())
            {
                error = $"messageIndex {messageIndex} is out of range (count={root.GetArrayLength()})";
                return null;
            }

            messageEl = root[messageIndex];
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            messageEl = root;
        }
        else
        {
            error = "expected JSON array or object";
            return null;
        }

        if (!messageEl.TryGetProperty("messageId", out var idProp)
            || string.IsNullOrWhiteSpace(idProp.GetString()))
        {
            error = "messageId not found in mail variable";
            return null;
        }

        var folder = "INBOX";
        if (messageEl.TryGetProperty("folder", out var folderProp)
            && !string.IsNullOrWhiteSpace(folderProp.GetString()))
        {
            folder = folderProp.GetString()!;
        }

        return (idProp.GetString()!, folder);
    }

    /// <summary>
    /// Resolves IMAP message id and folder for mail.get-body.
    /// Order: explicit id → mail.receive JSON (sourceVariable) → selectedMessageId → selectedMessageUid.
    /// </summary>
    internal static (string MessageId, string Folder, string Source, string? Uid)? ResolveMessageForGetBody(
        Dictionary<string, object> variables,
        string? explicitMessageId,
        string? explicitFolder,
        string sourceVariable,
        int messageIndex,
        Action<string>? log,
        out string? error)
    {
        error = null;
        log ??= _ => { };

        log("Trying explicit messageId...");
        if (IsUsableExplicitMessageId(explicitMessageId))
        {
            var folder = ResolveFolder(explicitFolder, variables, "INBOX");
            var uid    = GetVariableStringValue(
                variables.GetValueOrDefault("selectedMessageUid")) ?? explicitMessageId!.Trim();
            return (explicitMessageId!.Trim(), folder, "explicit", uid);
        }

        log($"Trying sourceVariable '{NormalizeVariableName(sourceVariable)}'...");
        var sourceKey = NormalizeVariableName(sourceVariable);
        if (variables.TryGetValue(sourceKey, out var mailRaw))
        {
            var fromReceive = ResolveMessageReference(mailRaw, messageIndex, out var receiveError);
            if (fromReceive is not null)
            {
                return (
                    fromReceive.Value.MessageId,
                    ResolveFolder(explicitFolder, variables, fromReceive.Value.Folder),
                    "sourceVariable",
                    fromReceive.Value.MessageId);
            }

            error = receiveError;
            log($"sourceVariable '{sourceKey}' present but not a mail message reference: {receiveError}");
        }
        else
        {
            log($"sourceVariable '{sourceKey}' not found in workflow context");
        }

        log("Trying selectedMessageId...");
        if (TryGetVariableString(variables, "selectedMessageId", out var selectedId))
        {
            var folder = ResolveFolder(explicitFolder, variables, ResolveReadAttachmentsFolder(variables));
            var uid    = GetVariableStringValue(variables.GetValueOrDefault("selectedMessageUid")) ?? selectedId!;
            return (selectedId!, folder, "selectedMessageId", uid);
        }

        log("Trying selectedMessageUid...");
        if (TryGetVariableString(variables, "selectedMessageUid", out var selectedUid))
        {
            var folder = ResolveFolder(explicitFolder, variables, ResolveReadAttachmentsFolder(variables));
            return (selectedUid!, folder, "selectedMessageUid", selectedUid);
        }

        if (error is not null)
        {
            return null;
        }

        error =
            "no message reference found. Run mail.receive or mail.read-attachments first, " +
            "or set 'messageId' explicitly.";
        return null;
    }

    private static string ResolveReadAttachmentsFolder(Dictionary<string, object> variables)
    {
        return TryGetVariableString(variables, "selectedMessageFolder", out var folder)
            ? folder!
            : "INBOX";
    }

    private static string ResolveFolder(
        string? explicitFolder,
        Dictionary<string, object> variables,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(explicitFolder))
            return explicitFolder.Trim();

        return fallback;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static bool TryGetVariableString(
        Dictionary<string, object> variables,
        string variableName,
        out string content,
        out string? error)
    {
        content = string.Empty;
        error   = null;

        var key = NormalizeVariableName(variableName);
        if (!variables.TryGetValue(key, out var raw) || raw is null)
        {
            error = $"variable '{key}' not found in workflow context";
            return false;
        }

        content = GetVariableStringValue(raw) ?? string.Empty;
        return !string.IsNullOrEmpty(content);
    }

    internal static string? ExtractByLabel(string content, string label)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(label))
            return null;

        var escaped = Regex.Escape(label.Trim());
        var patterns = new[]
        {
            $@"{escaped}\s*[:：]\s*(.+?)(?:\r?\n|$)",
            $@"{escaped}\s+([^\r\n<]+)",
            $@"<[^>]*>\s*{escaped}\s*</[^>]*>\s*<[^>]*>\s*(.*?)\s*</",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success) continue;

            var value = match.Groups[1].Value.Trim();
            value = Regex.Replace(value, "<[^>]+>", " ").Trim();
            value = Regex.Replace(value, @"\s+", " ").Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
