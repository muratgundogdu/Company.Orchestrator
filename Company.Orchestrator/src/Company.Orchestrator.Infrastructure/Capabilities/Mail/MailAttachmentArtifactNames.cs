using MimeKit;

namespace Company.Orchestrator.Infrastructure.Capabilities.Mail;

/// <summary>
/// Central naming rules for mail attachment artifacts (mail.read-attachments).
/// </summary>
public static class MailAttachmentArtifactNames
{
    /// <summary>
    /// Resolves the original attachment file name from a MIME part.
    /// </summary>
    public static string ResolveOriginalFileName(MimePart part)
    {
        var fileName = part.FileName
            ?? part.ContentDisposition?.FileName
            ?? part.ContentType?.Name;

        if (string.IsNullOrWhiteSpace(fileName))
            return "attachment";

        return Path.GetFileName(fileName);
    }

    /// <summary>
    /// Builds the workflow artifact name: optional prefix + sanitized base file name.
    /// Extension from content type is added only when the original name has none.
    /// </summary>
    public static string BuildArtifactName(
        string originalFileName,
        string? artifactPrefix,
        string? contentType)
    {
        var fileName = SanitizeFileName(Path.GetFileName(originalFileName));
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "attachment";

        fileName = EnsureExtensionIfMissing(fileName, contentType);

        var prefix = artifactPrefix?.Trim();
        return string.IsNullOrEmpty(prefix)
            ? fileName
            : $"{prefix}_{fileName}";
    }

    internal static string EnsureExtensionIfMissing(string fileName, string? contentType)
    {
        string? mimeExt = null;
        if (!string.IsNullOrWhiteSpace(contentType)
            && MimeTypes.TryGetExtension(contentType, out var ext)
            && !string.IsNullOrEmpty(ext))
        {
            mimeExt = ext;
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return fileName;
        }

        if (Path.HasExtension(fileName))
            return fileName;

        if (!string.IsNullOrEmpty(mimeExt))
            return fileName + mimeExt;

        return fileName + ".bin";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 100 ? name[..100] : name;
    }
}
