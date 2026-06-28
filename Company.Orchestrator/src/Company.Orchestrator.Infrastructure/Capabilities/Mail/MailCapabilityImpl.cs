using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.Mail;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;
using System.Text.RegularExpressions;

namespace Company.Orchestrator.Infrastructure.Capabilities.Mail;

/// <summary>
/// Production MailKit implementation of IMailCapability.
///
/// appsettings.json structure:
/// "Mail": {
///   "Smtp": { "Host":"", "Port":587, "UseSsl":true, "Username":"", "Password":"", "From":"" },
///   "Imap": { "Host":"", "Port":993, "UseSsl":true, "Username":"", "Password":"",
///             "TimeoutSeconds":120 }
/// }
///
/// Security:
///   - Passwords are never logged.
///   - Methods throw InvalidOperationException with a clear message if settings are missing.
///   - All IMAP operations are subject to a hard timeout (default 30s) to prevent jobs from
///     hanging forever in the Running state.
/// </summary>
public sealed class MailCapabilityImpl : IMailCapability
{
    private readonly MailSettings _settings;
    private readonly IArtifactStore _store;
    private readonly ILogger<MailCapabilityImpl> _logger;

    /// <summary>
    /// Operation-level timeout in milliseconds.
    /// Covers the entire IMAP session (connect → auth → search/fetch → disconnect).
    /// Configured via Mail:Imap:TimeoutSeconds (default 120).
    /// </summary>
    private readonly int _imapTimeoutMs;

    public string CapabilityName => "Mail";

    public MailCapabilityImpl(
        IConfiguration configuration,
        IArtifactStore store,
        ILogger<MailCapabilityImpl> logger)
    {
        _settings      = configuration.GetSection("Mail").Get<MailSettings>() ?? new MailSettings();
        _store         = store;
        _logger        = logger;
        _imapTimeoutMs = (_settings.Imap?.TimeoutSeconds ?? 120) * 1_000;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SEND
    // ══════════════════════════════════════════════════════════════════════════

    public async Task SendAsync(
        Application.Capabilities.Mail.MailMessage message,
        CancellationToken cancellationToken = default)
    {
        var smtp = _settings.Smtp ?? new SmtpSettings();

        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException(
                "MailCapability: SMTP host is not configured. Set Mail:Smtp:Host in appsettings.json.");

        if (string.IsNullOrWhiteSpace(smtp.Username) || string.IsNullOrWhiteSpace(smtp.Password))
            throw new InvalidOperationException(
                "MailCapability: SMTP credentials (Username/Password) are not configured.");

        var from = message.From ?? smtp.From
            ?? throw new InvalidOperationException(
                "MailCapability: sender address not found. Set Mail:Smtp:From in appsettings.json.");

        _logger.LogInformation(
            "MailCapability SMTP: sending to '{To}' subject '{Subject}' via {Host}:{Port}",
            message.To, message.Subject, smtp.Host, smtp.Port);

        var mimeMsg = await BuildMimeMessageAsync(message, from, cancellationToken);

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (_, _, _, _) => true;

        var sslOpts = smtp.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : smtp.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        await client.ConnectAsync(smtp.Host, smtp.Port, sslOpts, cancellationToken);
        await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        await client.SendAsync(mimeMsg, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation(
            "MailCapability SMTP: email sent successfully to '{To}'", message.To);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RECEIVE  (metadata only — no attachment bytes)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<Application.Capabilities.Mail.MailMessage>> ReceiveAsync(
        MailQuery query, CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_imapTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = await ConnectImapAsync(ct);

            _logger.LogInformation(
                "MailCapability IMAP: opening folder '{Folder}'", query.Folder);
            var folder = await OpenFolderAsync(client, query.Folder, FolderAccess.ReadOnly, ct);
            _logger.LogInformation(
                "MailCapability IMAP: folder '{Folder}' opened", query.Folder);

            _logger.LogInformation(
                "MailCapability IMAP: searching messages (unread={Unread}, subject='{Subj}', from='{From}', latestOnly={Latest}, sortOrder={Sort}, maxCount={Max})",
                query.UnreadOnly, query.SubjectContains, query.FromContains,
                query.LatestOnly, query.SortOrder, query.MaxCount);
            var uids = await SearchAsync(folder, query, ct);
            _logger.LogInformation(
                "MailCapability IMAP: search returned {TotalCount} UID(s)", uids.Count);

            var selectedUids = await SelectMessageUidsAsync(folder, uids, query, ct);
            _logger.LogInformation(
                "MailCapability IMAP: selected {SelectedCount} message(s) after sort/limit", selectedUids.Count);

            var summaries = await folder.FetchAsync(
                selectedUids,
                MessageSummaryItems.UniqueId
                | MessageSummaryItems.Envelope
                | MessageSummaryItems.BodyStructure
                | MessageSummaryItems.Flags
                | MessageSummaryItems.PreviewText,
                ct);
            _logger.LogInformation(
                "MailCapability IMAP: fetched {Count} summaries", summaries.Count);

            var results = new List<Application.Capabilities.Mail.MailMessage>();

            foreach (var summary in summaries)
            {
                var attachNames = summary.Attachments
                    .Select(a => a.FileName ?? "attachment")
                    .ToList();

                if (query.HasAttachments == true && attachNames.Count == 0) continue;
                if (results.Count >= query.MaxCount) break;

                results.Add(new Application.Capabilities.Mail.MailMessage
                {
                    MessageId           = summary.UniqueId.Id.ToString(),
                    Subject             = summary.Envelope?.Subject ?? "(no subject)",
                    From                = summary.Envelope?.From?.Mailboxes.FirstOrDefault()?.Address ?? "",
                    ReceivedAt          = summary.Envelope?.Date?.UtcDateTime,
                    Folder              = query.Folder,
                    AttachmentFileNames = attachNames,
                    BodyPreview         = summary.PreviewText ?? string.Empty
                });
            }

            _logger.LogInformation(
                "MailCapability IMAP: disconnecting after receive");
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "MailCapability IMAP: receive completed — {Count} message(s) returned from '{Folder}'",
                results.Count, query.Folder);
            return results;
        }
        catch (OperationCanceledException) when (IsOurTimeout(timeoutCts, cancellationToken))
        {
            throw new TimeoutException(
                $"IMAP receive timed out after {_imapTimeoutMs / 1_000}s " +
                $"(folder='{query.Folder}'). " +
                $"Increase Mail:Imap:TimeoutSeconds (currently {_imapTimeoutMs / 1_000}s).");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DOWNLOAD ATTACHMENTS
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<MailAttachmentDownloadBatchResult> DownloadAttachmentsAsync(
        MailQuery query, string artifactPrefix, CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        var progress = new MailDownloadProgress { Folder = query.Folder };

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_imapTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = await ConnectImapAsync(ct);

            _logger.LogInformation(
                "MailCapability IMAP: opening folder '{Folder}' for attachment download (timeout={Timeout}s, latestOnly={LatestOnly}, maxCount={MaxCount}, maxAttachmentCount={MaxAttach})",
                query.Folder, _imapTimeoutMs / 1_000, query.LatestOnly, query.MaxCount,
                query.MaxAttachmentCount?.ToString() ?? "(all)");
            var folder = await OpenFolderAsync(client, query.Folder, FolderAccess.ReadOnly, ct);

            _logger.LogInformation(
                "MailCapability IMAP: searching (unreadOnly={Unread}, subject='{Subj}', from='{From}', attachmentPattern='{Pattern}')",
                query.UnreadOnly,
                query.SubjectContains ?? "(any)",
                query.FromContains ?? "(any)",
                query.AttachmentPattern ?? "(any)");

            var uids = await SearchAsync(folder, query, ct);
            progress.MatchedEmailCount = uids.Count;
            _logger.LogInformation(
                "MailCapability IMAP: search matched {Count} email(s)", uids.Count);

            var selectedUids = await SelectMessageUidsAsync(folder, uids, query, ct);
            progress.SelectedEmailCount = selectedUids.Count;
            _logger.LogInformation(
                "MailCapability IMAP: selected {Count} email(s) for processing (latestOnly={LatestOnly}, maxCount={MaxCount})",
                selectedUids.Count, query.LatestOnly, query.MaxCount);

            if (selectedUids.Count == 0)
            {
                _logger.LogInformation("MailCapability IMAP: no messages selected — nothing to download");
                await client.DisconnectAsync(true, ct);
                return new MailAttachmentDownloadBatchResult();
            }

            var summaries = await folder.FetchAsync(
                selectedUids,
                MessageSummaryItems.UniqueId
                | MessageSummaryItems.Envelope
                | MessageSummaryItems.InternalDate
                | MessageSummaryItems.BodyStructure,
                ct);

            var summaryByUid = summaries.ToDictionary(s => s.UniqueId);

            var selectedMessages  = new List<MailMessage>();
            var results           = new List<MailAttachmentDownloadResult>();
            var processedCount    = 0;
            var totalDownloaded   = 0;

            foreach (var uid in selectedUids)
            {
                if (!query.LatestOnly && processedCount >= query.MaxCount)
                    break;

                if (!summaryByUid.TryGetValue(uid, out var summary))
                    continue;

                var subject    = summary.Envelope?.Subject ?? "(no subject)";
                var fromAddr   = summary.Envelope?.From?.Mailboxes.FirstOrDefault()?.Address ?? "";
                var receivedAt = GetMessageDate(summary);
                var uidText    = uid.Id.ToString();

                progress.SelectedSubject     = subject;
                progress.SelectedFrom        = fromAddr;
                progress.SelectedMessageUid  = uidText;
                progress.SelectedDate        = receivedAt == DateTime.MinValue ? null : receivedAt;

                progress.AttachmentNames.Clear();
                progress.AttachmentSizes.Clear();
                foreach (var att in summary.Attachments)
                {
                    var name = att.FileName ?? "attachment";
                    progress.AttachmentNames.Add(name);
                    progress.AttachmentSizes.Add(FormatAttachmentSize(att));
                }

                var selectedMessage = new MailMessage
                {
                    MessageId  = uidText,
                    Subject    = subject,
                    From       = fromAddr,
                    ReceivedAt = receivedAt == DateTime.MinValue ? null : receivedAt,
                    Folder     = query.Folder,
                    AttachmentFileNames = progress.AttachmentNames.ToList(),
                };
                selectedMessages.Add(selectedMessage);

                _logger.LogInformation(
                    "MailCapability IMAP: selected email — UID={Uid}, Subject='{Subject}', Date='{Date}', From='{From}', attachmentNames=[{Names}], attachmentSizes=[{Sizes}]",
                    uidText,
                    subject,
                    progress.SelectedDate?.ToString("O") ?? "(unknown)",
                    fromAddr,
                    string.Join(", ", progress.AttachmentNames),
                    string.Join(", ", progress.AttachmentSizes));

                _logger.LogDebug("MailCapability IMAP: fetching full message UID {Uid}", uid.Id);
                var mimeMsg = await folder.GetMessageAsync(uid, ct);

                var attachmentParts = mimeMsg.Attachments
                    .OfType<MimePart>()
                    .Where(p => p.Content != null)
                    .ToList();

                if (query.HasAttachments == true && attachmentParts.Count == 0)
                {
                    _logger.LogInformation(
                        "MailCapability IMAP: message UID {Uid} ('{Subject}') has no attachments — metadata retained, skipping download",
                        uid.Id, subject);
                    continue;
                }

                var mailMsg = new MailMessage
                {
                    MessageId           = uidText,
                    Subject             = subject,
                    From                = fromAddr,
                    ReceivedAt          = receivedAt == DateTime.MinValue ? null : receivedAt,
                    Folder              = query.Folder,
                    AttachmentFileNames = attachmentParts.Select(p => p.FileName ?? "attachment").ToList()
                };

                var artifacts           = new List<ArtifactReference>();
                var downloadedInMessage = 0;

                foreach (var part in attachmentParts)
                {
                    if (query.MaxAttachmentCount.HasValue
                        && query.MaxAttachmentCount.Value <= 0)
                    {
                        _logger.LogInformation(
                            "MailCapability IMAP: maxAttachmentCount={Max} — skipping attachment downloads for '{Subject}'",
                            query.MaxAttachmentCount.Value, subject);
                        break;
                    }

                    if (query.MaxAttachmentCount.HasValue
                        && downloadedInMessage >= query.MaxAttachmentCount.Value)
                    {
                        _logger.LogInformation(
                            "MailCapability IMAP: maxAttachmentCount={Max} reached for message '{Subject}' — skipping remaining attachments",
                            query.MaxAttachmentCount.Value, subject);
                        break;
                    }

                    var fileName = part.FileName ?? "attachment";
                    if (!AttachmentMatchesFilters(fileName, query, out var skipReason))
                    {
                        _logger.LogInformation(
                            "MailCapability IMAP: skipped attachment '{FileName}' — {Reason}",
                            fileName, skipReason);
                        continue;
                    }

                    progress.DownloadingAttachment = fileName;
                    _logger.LogInformation(
                        "MailCapability IMAP: downloading attachment '{FileName}' from '{Subject}'",
                        fileName, subject);

                    var contentType  = part.ContentType.MimeType;
                    var artifactId   = Guid.NewGuid();
                    var artifactName = string.IsNullOrEmpty(artifactPrefix)
                        ? SanitizeFileName(fileName)
                        : $"{artifactPrefix}_{SanitizeFileName(fileName)}";

                    using var ms = new MemoryStream();
                    await part.Content!.DecodeToAsync(ms, ct);
                    ms.Position = 0;
                    var sizeBytes = ms.Length;

                    var storagePath = await _store.SaveAsync(artifactId, artifactName, ms, ct);

                    progress.LastDownloadedAttachment = fileName;
                    progress.DownloadingAttachment    = null;

                    artifacts.Add(new ArtifactReference
                    {
                        Id          = artifactId,
                        Name        = artifactName,
                        ContentType = contentType,
                        StoragePath = storagePath,
                        SizeBytes   = sizeBytes,
                        Metadata    = new Dictionary<string, string>
                        {
                            ["mailSubject"]        = subject,
                            ["from"]               = fromAddr,
                            ["receivedAt"]         = (progress.SelectedDate ?? receivedAt).ToString("O"),
                            ["attachmentFileName"] = fileName,
                            ["contentType"]        = contentType,
                            ["messageId"]          = uidText
                        }
                    });

                    downloadedInMessage++;
                    totalDownloaded++;

                    _logger.LogInformation(
                        "MailCapability IMAP: downloaded attachment '{FileName}' ({Size} bytes) from '{Subject}' → artifact '{ArtifactName}'",
                        fileName, sizeBytes, subject, artifactName);
                }

                if (artifacts.Count == 0)
                {
                    _logger.LogInformation(
                        "MailCapability IMAP: message UID {Uid} ('{Subject}') — no attachments matched filters; metadata retained",
                        uid.Id, subject);
                    continue;
                }

                processedCount++;
                results.Add(new MailAttachmentDownloadResult { Message = mailMsg, Artifacts = artifacts });
            }

            _logger.LogInformation("MailCapability IMAP: disconnecting after attachment download");
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "MailCapability IMAP: download completed — {SelectedCount} selected message(s), {MsgCount} with attachments, {ArtCount} attachment(s)",
                selectedMessages.Count, results.Count, totalDownloaded);

            return new MailAttachmentDownloadBatchResult
            {
                SelectedMessages = selectedMessages,
                DownloadResults  = results,
            };
        }
        catch (OperationCanceledException) when (IsOurTimeout(timeoutCts, cancellationToken))
        {
            var timeoutSec = _imapTimeoutMs / 1_000;
            var message    = BuildAttachmentDownloadTimeoutMessage(query, progress, timeoutSec);
            throw new MailImapTimeoutException(message, progress.ToDiagnostics(timeoutSec));
        }
    }

    private static string BuildAttachmentDownloadTimeoutMessage(
        MailQuery query, MailDownloadProgress progress, int timeoutSeconds)
    {
        var attachment = progress.DownloadingAttachment ?? "(unknown)";
        var subject    = progress.SelectedSubject ?? "(unknown)";
        return
            $"IMAP attachment download timed out after {timeoutSeconds}s " +
            $"(folder='{query.Folder}', subject='{subject}', attachment='{attachment}'). " +
            $"Increase Mail:Imap:TimeoutSeconds (currently {timeoutSeconds}s).";
    }

    private static string FormatAttachmentSize(BodyPartBasic part)
    {
        if (part.Octets > 0)
            return $"{part.Octets} bytes";
        var dispositionSize = part.ContentDisposition?.Size;
        if (dispositionSize is > 0)
            return $"{dispositionSize.Value} bytes";
        return "unknown";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GET MESSAGE BODY
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<string> GetMessageBodyAsync(
        string messageId,
        string folder,
        string bodyType,
        CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        if (!uint.TryParse(messageId, out var rawUid))
            throw new InvalidOperationException($"MailCapability: invalid messageId '{messageId}'.");

        var normalizedType = bodyType.Trim().ToLowerInvariant();
        if (normalizedType is not ("text" or "html"))
            throw new ArgumentException("bodyType must be 'text' or 'html'.", nameof(bodyType));

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_imapTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = await ConnectImapAsync(ct);

            _logger.LogInformation(
                "MailCapability IMAP: fetching {BodyType} body for message {Id} in '{Folder}'",
                normalizedType, messageId, folder);

            var mailFolder = await OpenFolderAsync(client, folder, FolderAccess.ReadOnly, ct);
            var mimeMsg    = await mailFolder.GetMessageAsync(new UniqueId(rawUid), ct);

            string body;
            if (normalizedType == "html")
            {
                body = mimeMsg.HtmlBody ?? mimeMsg.TextBody ?? string.Empty;
            }
            else
            {
                body = mimeMsg.TextBody ?? string.Empty;
                if (string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(mimeMsg.HtmlBody))
                    body = HtmlToPlainText(mimeMsg.HtmlBody);
            }

            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "MailCapability IMAP: fetched {BodyType} body ({Length} chars) for message {Id}",
                normalizedType, body.Length, messageId);

            return body;
        }
        catch (OperationCanceledException) when (IsOurTimeout(timeoutCts, cancellationToken))
        {
            throw new TimeoutException(
                $"IMAP get-body timed out after {_imapTimeoutMs / 1_000}s " +
                $"(messageId={messageId}, folder='{folder}'). " +
                $"Increase Mail:Imap:TimeoutSeconds (currently {_imapTimeoutMs / 1_000}s).");
        }
    }

    private static string HtmlToPlainText(string html)
    {
        var text = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</p\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"[ \t]+\n", "\n").Trim();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MARK AS READ
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<bool> MarkAsReadAsync(
        string messageId,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default)
        => await MarkReadAsync(messageId, isRead: true, sourceFolder, cancellationToken);

    public Task<bool> MoveAsync(
        string messageId,
        string targetFolder,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default)
        => MoveToFolderAsync(messageId, targetFolder, sourceFolder, cancellationToken);

    public async Task<bool> MarkReadAsync(
        string messageId,
        bool isRead,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        if (!uint.TryParse(messageId, out var rawUid))
        {
            _logger.LogWarning("MailCapability.MarkReadAsync: invalid messageId '{Id}'", messageId);
            return false;
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_imapTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = await ConnectImapAsync(ct);

            _logger.LogInformation(
                "MailCapability IMAP: opening folder '{Folder}' to mark message {Id} as {State}",
                sourceFolder, messageId, isRead ? "read" : "unread");
            var folder = await OpenFolderAsync(client, sourceFolder, FolderAccess.ReadWrite, ct);

            var uid = new UniqueId(rawUid);
            var action = isRead ? StoreAction.Add : StoreAction.Remove;
            await folder.StoreAsync(
                uid,
                new StoreFlagsRequest(action, MessageFlags.Seen) { Silent = true },
                ct);

            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "MailCapability IMAP: message {Id} in '{Folder}' marked as {State}",
                messageId, sourceFolder, isRead ? "read" : "unread");
            return true;
        }
        catch (OperationCanceledException) when (IsOurTimeout(timeoutCts, cancellationToken))
        {
            throw new TimeoutException(
                $"IMAP mark-read timed out after {_imapTimeoutMs / 1_000}s " +
                $"(messageId={messageId}, folder='{sourceFolder}'). " +
                $"Increase Mail:Imap:TimeoutSeconds (currently {_imapTimeoutMs / 1_000}s).");
        }
    }

    public async Task<bool> DeleteAsync(
        string messageId,
        string sourceFolder,
        bool permanent,
        CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        if (!uint.TryParse(messageId, out var rawUid))
        {
            _logger.LogWarning("MailCapability.DeleteAsync: invalid messageId '{Id}'", messageId);
            return false;
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_imapTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = await ConnectImapAsync(ct);

            _logger.LogInformation(
                "MailCapability IMAP: deleting message {Id} from '{Folder}' (permanent={Permanent})",
                messageId, sourceFolder, permanent);
            var folder = await OpenFolderAsync(client, sourceFolder, FolderAccess.ReadWrite, ct);
            var uid    = new UniqueId(rawUid);

            if (permanent)
            {
                await folder.AddFlagsAsync(uid, MessageFlags.Deleted, true, ct);
                await folder.ExpungeAsync(ct);
            }
            else
            {
                var trash = await ResolveTrashFolderAsync(client, ct);
                await folder.MoveToAsync(uid, trash, ct);
            }

            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "MailCapability IMAP: message {Id} deleted from '{Folder}' (permanent={Permanent})",
                messageId, sourceFolder, permanent);
            return true;
        }
        catch (OperationCanceledException) when (IsOurTimeout(timeoutCts, cancellationToken))
        {
            throw new TimeoutException(
                $"IMAP delete timed out after {_imapTimeoutMs / 1_000}s " +
                $"(messageId={messageId}, folder='{sourceFolder}'). " +
                $"Increase Mail:Imap:TimeoutSeconds (currently {_imapTimeoutMs / 1_000}s).");
        }
    }

    public async Task<MailReplyResult> ReplyAsync(
        MailReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        var original = await FetchMimeMessageAsync(
            request.MessageId, request.SourceFolder, cancellationToken);

        var smtpFrom = _settings.Smtp?.From
            ?? throw new InvalidOperationException(
                "MailCapability: sender address not found. Set Mail:Smtp:From in appsettings.json.");

        var reply = BuildReplyMessage(original, request, MailboxAddress.Parse(smtpFrom));
        await SendMimeMessageAsync(reply, cancellationToken);

        var conversationId = original.MessageId
            ?? original.References.LastOrDefault()
            ?? request.MessageId;

        return new MailReplyResult
        {
            ReplyMessageId       = reply.MessageId ?? string.Empty,
            ReplyConversationId  = conversationId,
        };
    }

    public async Task<MailForwardResult> ForwardAsync(
        MailForwardRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        var original = await FetchMimeMessageAsync(
            request.MessageId, request.SourceFolder, cancellationToken);

        var smtpFrom = _settings.Smtp?.From
            ?? throw new InvalidOperationException(
                "MailCapability: sender address not found. Set Mail:Smtp:From in appsettings.json.");

        var forward = await BuildForwardMessageAsync(original, request, MailboxAddress.Parse(smtpFrom), cancellationToken);
        await SendMimeMessageAsync(forward, cancellationToken);

        return new MailForwardResult
        {
            ForwardMessageId = forward.MessageId ?? string.Empty,
        };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MOVE TO FOLDER
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<bool> MoveToFolderAsync(
        string messageId,
        string targetFolder,
        string sourceFolder = "INBOX",
        CancellationToken cancellationToken = default)
    {
        EnsureImapConfigured();

        if (!uint.TryParse(messageId, out var rawUid))
        {
            _logger.LogWarning("MailCapability.MoveToFolderAsync: invalid messageId '{Id}'", messageId);
            return false;
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_imapTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = await ConnectImapAsync(ct);

            _logger.LogInformation(
                "MailCapability IMAP: opening source folder '{Src}'", sourceFolder);
            var src  = await OpenFolderAsync(client, sourceFolder, FolderAccess.ReadWrite, ct);
            var dest = await client.GetFolderAsync(targetFolder, ct);

            var uid = new UniqueId(rawUid);
            _logger.LogInformation(
                "MailCapability IMAP: moving message {Id} → '{Dest}'", messageId, targetFolder);
            await src.MoveToAsync(uid, dest, ct);

            _logger.LogInformation("MailCapability IMAP: disconnecting");
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "MailCapability IMAP: message {Id} moved '{Src}' → '{Dest}'",
                messageId, sourceFolder, targetFolder);
            return true;
        }
        catch (OperationCanceledException) when (IsOurTimeout(timeoutCts, cancellationToken))
        {
            throw new TimeoutException(
                $"IMAP move timed out after {_imapTimeoutMs / 1_000}s " +
                $"(messageId={messageId}, source='{sourceFolder}', target='{targetFolder}'). " +
                $"Increase Mail:Imap:TimeoutSeconds (currently {_imapTimeoutMs / 1_000}s).");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates and returns a connected + authenticated ImapClient.
    /// Sets both the socket-level Timeout and logs each step.
    /// Disposes the client on any connection/auth failure to prevent resource leaks.
    /// </summary>
    private async Task<ImapClient> ConnectImapAsync(CancellationToken ct)
    {
        var imap = _settings.Imap ?? new ImapSettings();

        var sslOpts = imap.Port == 993
            ? SecureSocketOptions.SslOnConnect
            : imap.Port == 143
                ? SecureSocketOptions.StartTlsWhenAvailable
                : (imap.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable);

        var client = new ImapClient();
        try
        {
            // Socket-level read/write timeout (independent of the CancellationToken).
            // Prevents the TCP connection itself from hanging without a response.
            client.Timeout = _imapTimeoutMs;
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

            _logger.LogInformation(
                "MailCapability IMAP: Connecting to {Host}:{Port} (ssl={Ssl}, timeout={Timeout}s)",
                imap.Host, imap.Port, sslOpts, _imapTimeoutMs / 1_000);

            await client.ConnectAsync(imap.Host!, imap.Port, sslOpts, ct);

            _logger.LogInformation(
                "MailCapability IMAP: Connected successfully to {Host}", imap.Host);

            _logger.LogInformation(
                "MailCapability IMAP: Authenticating as '{User}'...", imap.Username);

            await client.AuthenticateAsync(imap.Username!, imap.Password!, ct);

            _logger.LogInformation(
                "MailCapability IMAP: Authenticated successfully as '{User}'", imap.Username);

            return client;
        }
        catch
        {
            // Ensure the partially-connected client is cleaned up before re-throwing.
            client.Dispose();
            throw;
        }
    }

    private static async Task<IMailFolder> OpenFolderAsync(
        ImapClient client, string folderName, FolderAccess access, CancellationToken ct)
    {
        var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox
            : await client.GetFolderAsync(folderName, ct);

        await folder.OpenAsync(access, ct);
        return folder;
    }

    private static async Task<IList<UniqueId>> SearchAsync(
        IMailFolder folder, MailQuery query, CancellationToken ct)
    {
        SearchQuery searchQuery = SearchQuery.All;

        if (query.UnreadOnly)
            searchQuery = SearchQuery.And(searchQuery, SearchQuery.NotSeen);

        if (query.ReceivedAfter.HasValue)
            searchQuery = SearchQuery.And(
                searchQuery, SearchQuery.DeliveredAfter(query.ReceivedAfter.Value));

        if (!string.IsNullOrEmpty(query.SubjectContains))
            searchQuery = SearchQuery.And(
                searchQuery, SearchQuery.SubjectContains(query.SubjectContains));

        if (!string.IsNullOrEmpty(query.FromContains))
            searchQuery = SearchQuery.And(
                searchQuery, SearchQuery.FromContains(query.FromContains));

        return await folder.SearchAsync(searchQuery, ct);
    }

    /// <summary>
    /// Sorts matched UIDs by received date and applies latestOnly / maxCount limits.
    /// </summary>
    private async Task<IList<UniqueId>> SelectMessageUidsAsync(
        IMailFolder folder,
        IList<UniqueId> uids,
        MailQuery query,
        CancellationToken ct)
    {
        if (uids.Count == 0) return uids;

        var summaries = await folder.FetchAsync(
            uids,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate,
            ct);

        var newestFirst = !string.Equals(query.SortOrder, "oldest", StringComparison.OrdinalIgnoreCase);

        var ordered = newestFirst
            ? summaries.OrderByDescending(GetMessageDate).ToList()
            : summaries.OrderBy(GetMessageDate).ToList();

        var limit = query.LatestOnly ? 1 : Math.Max(1, query.MaxCount);
        return ordered.Take(limit).Select(s => s.UniqueId).ToList();
    }

    private static DateTime GetMessageDate(IMessageSummary summary)
        => summary.Envelope?.Date?.UtcDateTime
           ?? summary.InternalDate?.UtcDateTime
           ?? DateTime.MinValue;

    private static bool AttachmentMatchesFilters(
        string fileName,
        MailQuery query,
        out string skipReason)
    {
        if (!string.IsNullOrEmpty(query.AttachmentNameContains)
            && fileName.IndexOf(query.AttachmentNameContains, StringComparison.OrdinalIgnoreCase) < 0)
        {
            skipReason = $"name does not contain '{query.AttachmentNameContains}'";
            return false;
        }

        if (!string.IsNullOrEmpty(query.AttachmentPattern)
            && !MatchesWildcardPattern(fileName, query.AttachmentPattern))
        {
            skipReason = $"does not match pattern '{query.AttachmentPattern}'";
            return false;
        }

        skipReason = string.Empty;
        return true;
    }

    /// <summary>Simple glob match: * = any sequence, ? = single char.</summary>
    private static bool MatchesWildcardPattern(string fileName, string pattern)
    {
        var regex = "^"
            + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".")
            + "$";
        return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase);
    }

    private void EnsureImapConfigured()
    {
        var imap = _settings.Imap ?? new ImapSettings();
        if (string.IsNullOrWhiteSpace(imap.Host))
            throw new InvalidOperationException(
                "MailCapability: IMAP host is not configured. Set Mail:Imap:Host in appsettings.json.");
        if (string.IsNullOrWhiteSpace(imap.Username) || string.IsNullOrWhiteSpace(imap.Password))
            throw new InvalidOperationException(
                "MailCapability: IMAP credentials (Username/Password) are not configured.");
    }

    /// <summary>
    /// True when our timeout CTS fired and the caller did NOT request cancellation.
    /// Distinguishes "we timed out" from "caller cancelled" so we rethrow as TimeoutException.
    /// </summary>
    private static bool IsOurTimeout(
        CancellationTokenSource timeoutCts, CancellationToken callerToken)
        => timeoutCts.IsCancellationRequested && !callerToken.IsCancellationRequested;

    private async Task<MimeMessage> FetchMimeMessageAsync(
        string messageId,
        string folderName,
        CancellationToken cancellationToken)
    {
        if (!uint.TryParse(messageId, out var rawUid))
            throw new InvalidOperationException($"MailCapability: invalid messageId '{messageId}'.");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_imapTimeoutMs));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        try
        {
            using var client = await ConnectImapAsync(ct);
            var folder   = await OpenFolderAsync(client, folderName, FolderAccess.ReadOnly, ct);
            var mimeMsg  = await folder.GetMessageAsync(new UniqueId(rawUid), ct);
            await client.DisconnectAsync(true, ct);
            return mimeMsg;
        }
        catch (OperationCanceledException) when (IsOurTimeout(timeoutCts, cancellationToken))
        {
            throw new TimeoutException(
                $"IMAP fetch timed out after {_imapTimeoutMs / 1_000}s " +
                $"(messageId={messageId}, folder='{folderName}').");
        }
    }

    private async Task SendMimeMessageAsync(MimeMessage mimeMsg, CancellationToken cancellationToken)
    {
        var smtp = _settings.Smtp ?? new SmtpSettings();

        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException(
                "MailCapability: SMTP host is not configured. Set Mail:Smtp:Host in appsettings.json.");

        if (string.IsNullOrWhiteSpace(smtp.Username) || string.IsNullOrWhiteSpace(smtp.Password))
            throw new InvalidOperationException(
                "MailCapability: SMTP credentials (Username/Password) are not configured.");

        if (string.IsNullOrEmpty(mimeMsg.MessageId))
            mimeMsg.MessageId = MimeUtils.GenerateMessageId("alterone.local");

        _logger.LogInformation(
            "MailCapability SMTP: sending message subject '{Subject}' via {Host}:{Port}",
            mimeMsg.Subject, smtp.Host, smtp.Port);

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (_, _, _, _) => true;

        var sslOpts = smtp.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : smtp.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        await client.ConnectAsync(smtp.Host, smtp.Port, sslOpts, cancellationToken);
        await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        await client.SendAsync(mimeMsg, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private async Task<MimeMessage> BuildForwardMessageAsync(
        MimeMessage original,
        MailForwardRequest request,
        MailboxAddress from,
        CancellationToken cancellationToken)
    {
        var forward = new MimeMessage();
        forward.From.Add(from);

        foreach (var to in request.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            forward.To.Add(MailboxAddress.Parse(to));

        foreach (var cc in request.Cc)
            forward.Cc.Add(MailboxAddress.Parse(cc));

        foreach (var bcc in request.Bcc)
            forward.Bcc.Add(MailboxAddress.Parse(bcc));

        forward.Subject = EnsureForwardSubject(original.Subject ?? "(no subject)");

        var builder = new BodyBuilder();
        if (request.IsHtml)
            builder.HtmlBody = request.Body;
        else
            builder.TextBody = request.Body;

        if (request.IncludeAttachments)
        {
            foreach (var attachment in original.Attachments.OfType<MimePart>())
            {
                if (attachment.Content is null)
                    continue;

                using var ms = new MemoryStream();
                await attachment.Content.DecodeToAsync(ms, cancellationToken);
                builder.Attachments.Add(
                    attachment.FileName ?? "attachment",
                    ms.ToArray(),
                    ContentType.Parse(attachment.ContentType.MimeType));
            }
        }

        forward.Body = builder.ToMessageBody();
        return forward;
    }

    private MimeMessage BuildReplyMessage(
        MimeMessage original,
        MailReplyRequest request,
        MailboxAddress from)
    {
        var reply = new MimeMessage();
        reply.From.Add(from);

        var smtpAddress = from.Address;
        if (request.ReplyAll)
        {
            AddReplyRecipients(reply.To, original.From, smtpAddress);
            AddReplyRecipients(reply.To, original.To, smtpAddress);
            AddReplyRecipients(reply.To, original.ReplyTo, smtpAddress);
            AddReplyRecipients(reply.Cc, original.Cc, smtpAddress);
        }
        else
        {
            var replyTo = original.ReplyTo.Mailboxes.FirstOrDefault()
                ?? original.From.Mailboxes.FirstOrDefault();
            if (replyTo is not null)
                reply.To.Add(replyTo);
        }

        if (reply.To.Count == 0)
            throw new InvalidOperationException("MailCapability: cannot determine reply recipient.");

        reply.Subject = EnsureReplySubject(original.Subject ?? "(no subject)");

        if (!string.IsNullOrEmpty(original.MessageId))
        {
            reply.InReplyTo = original.MessageId;
            foreach (var reference in original.References)
                reply.References.Add(reference);
            reply.References.Add(original.MessageId);
        }

        var builder = new BodyBuilder();
        var bodyText = request.Body;

        if (request.IncludeOriginalBody)
        {
            var originalBody = original.TextBody;
            if (string.IsNullOrWhiteSpace(originalBody) && !string.IsNullOrWhiteSpace(original.HtmlBody))
                originalBody = HtmlToPlainText(original.HtmlBody);

            if (!string.IsNullOrWhiteSpace(originalBody))
            {
                var fromLine = original.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown";
                var dateLine = original.Date.ToString("R");
                bodyText += $"\n\n--- Original Message ---\nFrom: {fromLine}\nDate: {dateLine}\nSubject: {original.Subject}\n\n{originalBody}";
            }
        }

        if (request.IsHtml)
            builder.HtmlBody = bodyText;
        else
            builder.TextBody = bodyText;

        foreach (var artRef in request.Attachments)
        {
            var bytes = _store.ReadAllBytesAsync(artRef.StoragePath, CancellationToken.None)
                .GetAwaiter().GetResult();
            builder.Attachments.Add(artRef.Name, bytes, ContentType.Parse(artRef.ContentType));
        }

        reply.Body = builder.ToMessageBody();
        return reply;
    }

    private static void AddReplyRecipients(
        InternetAddressList target,
        InternetAddressList source,
        string excludeAddress)
    {
        foreach (var mailbox in source.Mailboxes)
        {
            if (string.Equals(mailbox.Address, excludeAddress, StringComparison.OrdinalIgnoreCase))
                continue;

            if (target.Any(m => m is MailboxAddress mb
                && string.Equals(mb.Address, mailbox.Address, StringComparison.OrdinalIgnoreCase)))
                continue;

            target.Add(mailbox);
        }
    }

    private static string EnsureReplySubject(string subject)
    {
        var trimmed = subject.Trim();
        return trimmed.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"Re: {trimmed}";
    }

    private static string EnsureForwardSubject(string subject)
    {
        var trimmed = subject.Trim();
        if (trimmed.StartsWith("FW:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return $"FW: {trimmed}";
    }

    private static async Task<IMailFolder> ResolveTrashFolderAsync(ImapClient client, CancellationToken ct)
    {
        try
        {
            var trash = client.GetFolder(SpecialFolder.Trash);
            if (trash is not null)
                return trash;
        }
        catch
        {
            // fall through to name-based lookup
        }

        foreach (var name in new[] { "Deleted Items", "Trash", "[Gmail]/Trash" })
        {
            try
            {
                return await client.GetFolderAsync(name, ct);
            }
            catch
            {
                // try next name
            }
        }

        throw new InvalidOperationException(
            "MailCapability: could not locate a trash/deleted-items folder on the mail server.");
    }

    private async Task<MimeMessage> BuildMimeMessageAsync(
        Application.Capabilities.Mail.MailMessage message,
        string from,
        CancellationToken ct)
    {
        var mimeMsg = new MimeMessage();
        mimeMsg.From.Add(MailboxAddress.Parse(from));

        foreach (var to in message.To.Split(',', StringSplitOptions.RemoveEmptyEntries))
            mimeMsg.To.Add(MailboxAddress.Parse(to.Trim()));

        foreach (var cc  in message.Cc)  mimeMsg.Cc.Add(MailboxAddress.Parse(cc));
        foreach (var bcc in message.Bcc) mimeMsg.Bcc.Add(MailboxAddress.Parse(bcc));

        mimeMsg.Subject = message.Subject;

        var builder = new BodyBuilder();
        if (message.IsHtml) builder.HtmlBody = message.Body;
        else                builder.TextBody  = message.Body;

        foreach (var artRef in message.Attachments)
        {
            var bytes = await _store.ReadAllBytesAsync(artRef.StoragePath, ct);
            builder.Attachments.Add(artRef.Name, bytes, ContentType.Parse(artRef.ContentType));
        }

        mimeMsg.Body = builder.ToMessageBody();
        return mimeMsg;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 100 ? name[..100] : name;
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Settings (internal — not exposed to Application layer)
// ──────────────────────────────────────────────────────────────────────────────

internal sealed class MailSettings
{
    public SmtpSettings? Smtp { get; set; }
    public ImapSettings? Imap { get; set; }
}

internal sealed class SmtpSettings
{
    public string? Host     { get; set; }
    public int     Port     { get; set; } = 587;
    public bool    UseSsl   { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? From     { get; set; }
}

internal sealed class ImapSettings
{
    public string? Host           { get; set; }
    public int     Port           { get; set; } = 993;
    public bool    UseSsl         { get; set; } = true;
    public string? Username       { get; set; }
    public string? Password       { get; set; }

    /// <summary>
    /// Hard wall-clock timeout for the entire IMAP session.
    /// If any operation (connect, auth, search, fetch, disconnect) takes longer
    /// than this many seconds, a TimeoutException is raised and the job fails/retries.
    /// Default: 120 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}
