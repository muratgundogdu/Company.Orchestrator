using Company.Orchestrator.Application.Artifacts;

namespace Company.Orchestrator.Application.Capabilities.Mail;

/// <summary>
/// Capability for sending and receiving email via SMTP / IMAP.
/// Phase 4: backed by MailKit for production-grade reliability.
/// </summary>
public interface IMailCapability : ICapability
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MailMessage>> ReceiveAsync(
        MailQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the mailbox for messages matching <paramref name="query"/>,
    /// records selected message metadata, downloads matching attachments into the artifact store,
    /// and returns both the selected messages and download results.
    /// </summary>
    Task<MailAttachmentDownloadBatchResult> DownloadAttachmentsAsync(
        MailQuery query, string artifactPrefix, CancellationToken cancellationToken = default);

    /// <summary>Marks the message identified by IMAP UID as read.</summary>
    Task<bool> MarkAsReadAsync(
        string messageId,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default);

    /// <summary>Marks the message read or unread on the IMAP server.</summary>
    Task<bool> MarkReadAsync(
        string messageId,
        bool isRead,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default);

    /// <summary>Moves the message identified by IMAP UID to <paramref name="targetFolder"/>.</summary>
    Task<bool> MoveToFolderAsync(
        string messageId,
        string targetFolder,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default);

    /// <summary>Moves the message identified by IMAP UID to <paramref name="targetFolder"/>.</summary>
    Task<bool> MoveAsync(
        string messageId,
        string targetFolder,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default);

    /// <summary>Replies to an existing message, preserving thread headers when possible.</summary>
    Task<MailReplyResult> ReplyAsync(
        MailReplyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Forwards an existing message to new recipients.</summary>
    Task<MailForwardResult> ForwardAsync(
        MailForwardRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a message to Deleted Items or permanently deletes it.</summary>
    Task<bool> DeleteAsync(
        string messageId,
        string sourceFolder,
        bool permanent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the full message body for the given IMAP UID.
    /// <paramref name="bodyType"/> is "text" or "html".
    /// </summary>
    Task<string> GetMessageBodyAsync(
        string messageId,
        string folder,
        string bodyType,
        CancellationToken cancellationToken = default);
}

// ──────────────────────────────────────────────────────────────────────────────
// Shared types
// ──────────────────────────────────────────────────────────────────────────────

public sealed class MailMessage
{
    public string To { get; set; } = string.Empty;
    public List<string> Cc  { get; set; } = new();
    public List<string> Bcc { get; set; } = new();

    public string  Subject { get; set; } = string.Empty;
    public string  Body    { get; set; } = string.Empty;
    public bool    IsHtml  { get; set; } = false;
    public string? From    { get; set; }

    /// <summary>Artifact references to attach when sending; populated on receive for bookkeeping.</summary>
    public List<ArtifactReference> Attachments { get; set; } = new();

    // ── Receive-only metadata ────────────────────────────────────────────────

    /// <summary>
    /// IMAP UID (decimal string). Set on receive; pass back to MarkAsReadAsync / MoveToFolderAsync.
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>Folder from which this message was retrieved.</summary>
    public string? Folder { get; set; }

    public DateTime? ReceivedAt { get; set; }

    /// <summary>First 200 characters of the plain-text body. Empty when not fetched.</summary>
    public string BodyPreview { get; set; } = string.Empty;

    /// <summary>File names of any attachments carried by this message.</summary>
    public List<string> AttachmentFileNames { get; set; } = new();
}

public sealed class MailReplyRequest
{
    public string MessageId { get; set; } = string.Empty;
    public string SourceFolder { get; set; } = "INBOX";
    public string Body { get; set; } = string.Empty;
    public bool ReplyAll { get; set; }
    public bool IncludeOriginalBody { get; set; } = true;
    public bool IsHtml { get; set; }
    public List<ArtifactReference> Attachments { get; set; } = new();
}

public sealed class MailReplyResult
{
    public string ReplyMessageId { get; set; } = string.Empty;
    public string ReplyConversationId { get; set; } = string.Empty;
}

public sealed class MailForwardRequest
{
    public string MessageId { get; set; } = string.Empty;
    public string SourceFolder { get; set; } = "INBOX";
    public string To { get; set; } = string.Empty;
    public List<string> Cc { get; set; } = new();
    public List<string> Bcc { get; set; } = new();
    public string Body { get; set; } = string.Empty;
    public bool IncludeAttachments { get; set; } = true;
    public bool IsHtml { get; set; }
}

public sealed class MailForwardResult
{
    public string ForwardMessageId { get; set; } = string.Empty;
}

public sealed class MailQuery
{
    public string Folder       { get; set; } = "INBOX";
    public bool   UnreadOnly   { get; set; } = true;

    public string?   SubjectContains { get; set; }
    public string?   FromContains    { get; set; }
    public DateTime? ReceivedAfter   { get; set; }

    /// <summary>When true, only return messages that have at least one attachment.</summary>
    public bool? HasAttachments { get; set; }

    /// <summary>When true, only the single newest matching message is processed.</summary>
    public bool LatestOnly { get; set; } = false;

    /// <summary>Maximum number of matching messages. Ignored when <see cref="LatestOnly"/> is true (always 1).</summary>
    public int MaxCount { get; set; } = 10;

    /// <summary>
    /// When set, limits how many matching attachments are downloaded per selected message.
    /// Null = all matching attachments in each selected message.
    /// </summary>
    public int? MaxAttachmentCount { get; set; }

    /// <summary>Sort order for matched messages: "newest" (default) or "oldest".</summary>
    public string SortOrder { get; set; } = "newest";

    /// <summary>Only download attachments whose file name contains this text (case-insensitive).</summary>
    public string? AttachmentNameContains { get; set; }

    /// <summary>Wildcard pattern for attachment file names, e.g. "*.xlsx" or "file*.xlsx".</summary>
    public string? AttachmentPattern { get; set; }
}

/// <summary>
/// Result of <see cref="IMailCapability.DownloadAttachmentsAsync"/>.
/// Selected messages are captured before attachment download begins.
/// </summary>
public sealed class MailAttachmentDownloadBatchResult
{
    public IReadOnlyList<MailMessage> SelectedMessages { get; init; } = Array.Empty<MailMessage>();

    public IReadOnlyList<MailAttachmentDownloadResult> DownloadResults { get; init; } =
        Array.Empty<MailAttachmentDownloadResult>();
}

/// <summary>
/// Per-message attachment download result (artifacts may be empty when filters exclude all files).
/// </summary>
public sealed class MailAttachmentDownloadResult
{
    public MailMessage               Message   { get; init; } = new();
    public IReadOnlyList<ArtifactReference> Artifacts { get; init; } = new List<ArtifactReference>();
}
