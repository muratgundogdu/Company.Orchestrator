namespace Company.Orchestrator.Infrastructure.Capabilities.Mail;

/// <summary>
/// Raised when an IMAP attachment download operation exceeds the configured timeout.
/// Carries structured diagnostics for failure reports and step handler output.
/// </summary>
internal sealed class MailImapTimeoutException : TimeoutException
{
    public Dictionary<string, object> Diagnostics { get; }

    public MailImapTimeoutException(string message, Dictionary<string, object> diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }
}

internal sealed class MailDownloadProgress
{
    public string Folder { get; set; } = "INBOX";
    public int MatchedEmailCount { get; set; }
    public int SelectedEmailCount { get; set; }
    public string? SelectedSubject { get; set; }
    public string? SelectedFrom { get; set; }
    public string? SelectedMessageUid { get; set; }
    public DateTime? SelectedDate { get; set; }
    public List<string> AttachmentNames { get; } = [];
    public List<string> AttachmentSizes { get; } = [];
    public string? DownloadingAttachment { get; set; }
    public string? LastDownloadedAttachment { get; set; }

    public Dictionary<string, object> ToDiagnostics(int timeoutSeconds) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["mailRead_matchedEmailCount"]      = MatchedEmailCount,
            ["mailRead_selectedEmailCount"]     = SelectedEmailCount,
            ["mailRead_selectedEmailSubject"]   = SelectedSubject ?? "(unknown)",
            ["mailRead_selectedEmailDate"]        = SelectedDate?.ToString("O") ?? "(unknown)",
            ["mailRead_selectedEmailFrom"]        = SelectedFrom ?? "(unknown)",
            ["mailRead_selectedMessageUid"]       = SelectedMessageUid ?? "(unknown)",
            ["mailRead_selectedMessageId"]        = SelectedMessageUid ?? "(unknown)",
            ["mailRead_selectedMessageFolder"]    = Folder,
            ["mailRead_attachmentNames"]          = string.Join(", ", AttachmentNames),
            ["mailRead_attachmentSizes"]          = string.Join(", ", AttachmentSizes),
            ["mailRead_downloadingAttachment"]    = DownloadingAttachment ?? "(none)",
            ["mailRead_lastDownloadedAttachment"] = LastDownloadedAttachment ?? "(none)",
            ["mailRead_timeoutSeconds"]           = timeoutSeconds,
        };
}
