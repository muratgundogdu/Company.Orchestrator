using System.Text.Json;

using Company.Orchestrator.Application.Capabilities.Mail;

using Company.Orchestrator.Application.Common.Interfaces;

using Company.Orchestrator.Application.Models;

using Company.Orchestrator.Infrastructure.Capabilities.Mail;

using Microsoft.Extensions.Logging;



namespace Company.Orchestrator.Infrastructure.StepHandlers;



/// <summary>

/// Searches an IMAP mailbox for messages and downloads matching attachments

/// into the artifact store.

/// </summary>

public sealed class MailReadAttachmentsStepHandler : IStepHandler

{

    private readonly ILogger<MailReadAttachmentsStepHandler> _logger;

    public string HandlerType => "mail.read-attachments";



    public MailReadAttachmentsStepHandler(ILogger<MailReadAttachmentsStepHandler> logger)

    {

        _logger = logger;

    }



    public async Task<StepResult> ExecuteAsync(

        WorkflowContext context, CancellationToken cancellationToken = default)

    {

        var config         = context.StepDefinition.Config;

        var artifactPrefix = config.GetValueOrDefault("artifactPrefix")?.ToString() ?? "mail-file";

        var outputVar      = config.GetValueOrDefault("outputVariable")?.ToString() ?? "mailArtifacts";

        var markAsRead     = MailReceiveStepHandler.ParseBool(

            config.GetValueOrDefault("markAsReadAfterProcessing"), defaultValue: false);



        var query = MailReceiveStepHandler.BuildQuery(config);

        query.HasAttachments ??= true;



        // mail.read-attachments defaults differ from mail.receive

        if (!config.ContainsKey("latestOnly"))

            query.LatestOnly = true;

        if (!config.ContainsKey("maxCount"))

            query.MaxCount = 1;

        if (!config.ContainsKey("sortOrder"))

            query.SortOrder = "newest";



        var diagnostics = BuildDiagnostics(query);

        var output      = new Dictionary<string, object>();



        _logger.LogInformation(

            "MailReadAttachments: folder='{Folder}', subjectContains='{Subject}', fromContains='{From}', " +

            "unreadOnly={UnreadOnly}, latestOnly={LatestOnly}, maxCount={MaxCount}, sortOrder={SortOrder}, " +

            "attachmentNameContains='{AttachName}', attachmentPattern='{AttachPattern}', maxAttachmentCount={MaxAttach}",

            query.Folder,

            query.SubjectContains ?? "(any)",

            query.FromContains ?? "(any)",

            query.UnreadOnly,

            query.LatestOnly,

            query.MaxCount,

            query.SortOrder,

            query.AttachmentNameContains ?? "(any)",

            query.AttachmentPattern ?? "(any)",

            query.MaxAttachmentCount?.ToString() ?? "(all)");



        try

        {

            var mail  = context.GetCapability<IMailCapability>();

            var batch = await mail.DownloadAttachmentsAsync(query, artifactPrefix, cancellationToken);



            // Write selected message metadata immediately — independent of attachment download outcome.

            var primaryMessage = batch.SelectedMessages.FirstOrDefault();

            WriteSelectedMessageMetadata(output, primaryMessage, query.Folder);



            var results = batch.DownloadResults;



            _logger.LogInformation(

                "MailReadAttachments: selected {SelectedCount} email(s), downloaded attachments for {DownloadCount} message(s), total attachments={AttachCount}",

                batch.SelectedMessages.Count,

                results.Count,

                results.Sum(r => r.Artifacts.Count));



            foreach (var result in results)

            {

                _logger.LogInformation(

                    "MailReadAttachments: message — Subject='{Subject}', From='{From}', Date='{Date}', Attachments=[{Files}]",

                    result.Message.Subject,

                    result.Message.From,

                    result.Message.ReceivedAt?.ToString("O") ?? "(unknown)",

                    string.Join(", ", result.Artifacts.Select(a =>

                        a.Metadata?.GetValueOrDefault("attachmentFileName") ?? a.Name)));

            }



            var allArtifacts  = results.SelectMany(r => r.Artifacts).ToList();

            var artifactNames = allArtifacts.Select(a => a.Name).ToList();



            foreach (var artifact in allArtifacts)

            {

                _logger.LogInformation(

                    "MailReadAttachments: artifact saved — Name='{Name}', ContentType='{ContentType}', SizeBytes={SizeBytes}",

                    artifact.Name, artifact.ContentType, artifact.SizeBytes);

            }



            var markTarget = primaryMessage ?? results.FirstOrDefault()?.Message;

            if (markAsRead && markTarget is not null && !string.IsNullOrEmpty(markTarget.MessageId))

            {

                var marked = await mail.MarkAsReadAsync(

                    markTarget.MessageId, query.Folder, cancellationToken);

                _logger.LogInformation(

                    "MailReadAttachments: markAsReadAfterProcessing messageId={Id} success={Success}",

                    markTarget.MessageId, marked);

            }



            foreach (var artifact in allArtifacts)

                context.Artifacts[artifact.Name] = artifact;



            var json      = JsonSerializer.Serialize(artifactNames);

            var firstName = artifactNames.Count > 0 ? artifactNames[0] : string.Empty;



            output[outputVar]            = json;

            output[$"{outputVar}_count"] = allArtifacts.Count;

            output[$"{outputVar}_first"] = firstName;



            for (var i = 0; i < artifactNames.Count; i++)

                output[$"{outputVar}_{i}"] = artifactNames[i];



            _logger.LogInformation(

                "MailReadAttachments: StepResult output variables ({Count}): [{Keys}]",

                output.Count,

                string.Join(", ", output.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)));



            return StepResult.Ok(

                output: output,

                artifacts: allArtifacts,

                outputData: $"Selected {batch.SelectedMessages.Count} message(s); downloaded {allArtifacts.Count} attachment(s) from {results.Count} message(s) in '{query.Folder}'");

        }

        catch (MailImapTimeoutException ex)

        {

            WriteSelectedMessageMetadataFromDiagnostics(output, ex.Diagnostics, query.Folder);



            _logger.LogError(ex,

                "MailReadAttachments: IMAP timeout — subject='{Subject}', attachment='{Attachment}', timeout={Timeout}s",

                ex.Diagnostics.GetValueOrDefault("mailRead_selectedEmailSubject"),

                ex.Diagnostics.GetValueOrDefault("mailRead_downloadingAttachment"),

                ex.Diagnostics.GetValueOrDefault("mailRead_timeoutSeconds"));



            var merged = MergeDiagnostics(diagnostics, ex.Diagnostics);
            foreach (var (key, value) in output)
                merged[key] = value;

            return StepResult.Fail(ex.Message, merged);

        }

        catch (TimeoutException ex)

        {

            _logger.LogError(ex, "MailReadAttachments: IMAP timeout");

            return StepResult.Fail(ex.Message, diagnostics);

        }

    }



    private void WriteSelectedMessageMetadata(

        Dictionary<string, object> output,

        MailMessage? message,

        string defaultFolder)

    {

        if (message is null || string.IsNullOrWhiteSpace(message.MessageId))

        {

            _logger.LogWarning(

                "MailReadAttachments: no selected message metadata to write (message={MessageState})",

                message is null ? "null" : "missing MessageId");

            return;

        }



        var uid     = message.MessageId.Trim();

        var folder  = string.IsNullOrWhiteSpace(message.Folder) ? defaultFolder : message.Folder;

        var subject = message.Subject ?? string.Empty;

        var from    = message.From ?? string.Empty;



        output["selectedMessageId"]     = uid;

        output["selectedMessageUid"]    = uid;

        output["selectedMessageFolder"] = folder;

        output["selectedEmailSubject"]  = subject;

        output["selectedEmailFrom"]     = from;



        _logger.LogInformation("MailReadAttachments selected message metadata written:");

        _logger.LogInformation("MailReadAttachments: selectedMessageId={SelectedMessageId}", uid);

        _logger.LogInformation("MailReadAttachments: selectedMessageUid={SelectedMessageUid}", uid);

        _logger.LogInformation("MailReadAttachments: selectedMessageFolder={SelectedMessageFolder}", folder);

        _logger.LogInformation("MailReadAttachments: selectedEmailSubject={SelectedEmailSubject}", subject);

        _logger.LogInformation("MailReadAttachments: selectedEmailFrom={SelectedEmailFrom}", from);

    }



    private void WriteSelectedMessageMetadataFromDiagnostics(

        Dictionary<string, object> output,

        IReadOnlyDictionary<string, object> diagnostics,

        string defaultFolder)

    {

        var uid = diagnostics.GetValueOrDefault("mailRead_selectedMessageUid")?.ToString()

               ?? diagnostics.GetValueOrDefault("mailRead_selectedMessageId")?.ToString();



        if (string.IsNullOrWhiteSpace(uid) || uid == "(unknown)")

            return;



        WriteSelectedMessageMetadata(

            output,

            new MailMessage

            {

                MessageId = uid.Trim(),

                Folder    = diagnostics.GetValueOrDefault("mailRead_selectedMessageFolder")?.ToString() ?? defaultFolder,

                Subject   = diagnostics.GetValueOrDefault("mailRead_selectedEmailSubject")?.ToString() ?? string.Empty,

                From      = diagnostics.GetValueOrDefault("mailRead_selectedEmailFrom")?.ToString() ?? string.Empty,

            },

            defaultFolder);

    }



    internal static Dictionary<string, object> BuildDiagnostics(MailQuery query) =>

        new(StringComparer.OrdinalIgnoreCase)

        {

            ["mailRead_folder"]               = query.Folder,

            ["mailRead_subjectContains"]      = query.SubjectContains ?? "(any)",

            ["mailRead_fromContains"]         = query.FromContains ?? "(any)",

            ["mailRead_unreadOnly"]           = query.UnreadOnly,

            ["mailRead_latestOnly"]           = query.LatestOnly,

            ["mailRead_maxCount"]             = query.MaxCount,

            ["mailRead_sortOrder"]            = query.SortOrder,

            ["mailRead_attachmentPattern"]    = query.AttachmentPattern ?? "(any)",

            ["mailRead_attachmentNameContains"] = query.AttachmentNameContains ?? "(any)",

            ["mailRead_maxAttachmentCount"]   = query.MaxAttachmentCount?.ToString() ?? "(all)",

        };



    private static Dictionary<string, object> MergeDiagnostics(

        Dictionary<string, object> baseline,

        IReadOnlyDictionary<string, object> runtime)

    {

        var merged = new Dictionary<string, object>(baseline, StringComparer.OrdinalIgnoreCase);

        foreach (var (k, v) in runtime)

            merged[k] = v;

        return merged;

    }

}


