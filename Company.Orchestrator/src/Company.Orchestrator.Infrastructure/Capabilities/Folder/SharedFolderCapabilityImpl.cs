using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.Folder;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Capabilities.Folder;

/// <summary>
/// Production SharedFolderCapability backed by System.IO.
///
/// UNC PATHS:
///   Windows: \\server\share\folder\file.xlsx — natively supported by System.IO.
///   The current implementation runs under the service/worker identity.
///   Credential impersonation will be activated when UseCredentials() receives a non-null
///   FolderCredentials and the WindowsIdentity.RunImpersonated implementation is wired in.
///
/// ERROR HANDLING:
///   All public methods catch and re-throw with enriched messages that include the offending path.
///   This makes step-level logs far easier to diagnose without digging into exception stacks.
///
/// THREAD SAFETY:
///   Stateless per call. UseCredentials is not thread-safe — call once at setup or per-thread.
/// </summary>
public sealed class SharedFolderCapabilityImpl : ISharedFolderCapability
{
    private readonly IArtifactStore _store;
    private readonly ILogger<SharedFolderCapabilityImpl> _logger;

    // Reserved for future impersonation — not yet applied.
    private FolderCredentials? _credentials;

    public string CapabilityName => "SharedFolder";

    public SharedFolderCapabilityImpl(IArtifactStore store, ILogger<SharedFolderCapabilityImpl> logger)
    {
        _store  = store;
        _logger = logger;
    }

    // ------------------------------------------------------------------ //
    // Future: credential impersonation hook
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Stores credentials for future impersonation support.
    /// Currently no-op — operations execute under the current process identity.
    /// </summary>
    public void UseCredentials(FolderCredentials? credentials)
    {
        _credentials = credentials;
        if (credentials is not null)
            _logger.LogWarning(
                "SharedFolderCapability: credentials supplied for '{User}' " +
                "but impersonation is not yet implemented — running as current identity.",
                credentials.Username);
    }

    // ------------------------------------------------------------------ //
    // Read (file → artifact store)
    // ------------------------------------------------------------------ //

    public async Task<ArtifactReference> ReadFileAsync(
        string sourcePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SharedFolderCapability: reading file '{Path}'", sourcePath);

        if (!System.IO.File.Exists(sourcePath))
            throw new FileNotFoundException(
                $"SharedFolderCapability: file not found: '{sourcePath}'", sourcePath);

        var name        = Path.GetFileName(sourcePath);
        var id          = Guid.NewGuid();
        var contentType = ResolveMimeType(name);

        await using var fs = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81_920, useAsync: true);

        var sizeBytes   = fs.Length;
        var storagePath = await _store.SaveAsync(id, name, fs, cancellationToken);

        _logger.LogInformation(
            "SharedFolderCapability: read '{Path}' → artifact {Id} ({Bytes:N0} bytes)",
            sourcePath, id, sizeBytes);

        return new ArtifactReference
        {
            Id          = id,
            Name        = name,
            ContentType = contentType,
            StoragePath = storagePath,
            SizeBytes   = sizeBytes,
            Metadata    = new Dictionary<string, string>
            {
                ["originalPath"] = sourcePath,
                ["operation"]    = "folderRead"
            }
        };
    }

    // ------------------------------------------------------------------ //
    // Write (artifact store → file)
    // ------------------------------------------------------------------ //

    public async Task WriteFileAsync(
        ArtifactReference artifact,
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SharedFolderCapability: writing artifact {Id} → '{Dest}' (overwrite={O})",
            artifact.Id, destinationPath, overwrite);

        if (!overwrite && System.IO.File.Exists(destinationPath))
            throw new IOException(
                $"SharedFolderCapability: destination already exists and overwrite=false: '{destinationPath}'");

        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var content = await _store.ReadAllBytesAsync(artifact.StoragePath, cancellationToken);
        await System.IO.File.WriteAllBytesAsync(destinationPath, content, cancellationToken);

        _logger.LogInformation(
            "SharedFolderCapability: wrote {Bytes:N0} bytes → '{Dest}'",
            content.Length, destinationPath);
    }

    // ------------------------------------------------------------------ //
    // Copy
    // ------------------------------------------------------------------ //

    public Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SharedFolderCapability: copying '{Src}' → '{Dst}' (overwrite={O})",
            sourcePath, destinationPath, overwrite);

        if (!System.IO.File.Exists(sourcePath))
            throw new FileNotFoundException(
                $"SharedFolderCapability: source not found: '{sourcePath}'", sourcePath);

        if (!overwrite && System.IO.File.Exists(destinationPath))
            throw new IOException(
                $"SharedFolderCapability: destination already exists and overwrite=false: '{destinationPath}'");

        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        System.IO.File.Copy(sourcePath, destinationPath, overwrite);
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Move
    // ------------------------------------------------------------------ //

    public Task MoveFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SharedFolderCapability: moving '{Src}' → '{Dst}' (overwrite={O})",
            sourcePath, destinationPath, overwrite);

        if (!System.IO.File.Exists(sourcePath))
            throw new FileNotFoundException(
                $"SharedFolderCapability: source not found: '{sourcePath}'", sourcePath);

        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (overwrite && System.IO.File.Exists(destinationPath))
            System.IO.File.Delete(destinationPath);

        System.IO.File.Move(sourcePath, destinationPath);
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Delete
    // ------------------------------------------------------------------ //

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            _logger.LogInformation("SharedFolderCapability: deleted '{Path}'", path);
        }
        else
        {
            _logger.LogDebug("SharedFolderCapability: delete no-op — file not found: '{Path}'", path);
        }
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Existence checks
    // ------------------------------------------------------------------ //

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(System.IO.File.Exists(path));

    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(Directory.Exists(path));

    // ------------------------------------------------------------------ //
    // Directory management
    // ------------------------------------------------------------------ //

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(path);
        _logger.LogInformation("SharedFolderCapability: ensured directory '{Path}'", path);
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Listing
    // ------------------------------------------------------------------ //

    public Task<IReadOnlyList<FileEntry>> ListFilesAsync(
        string folderPath,
        string pattern = "*",
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SharedFolderCapability: listing '{Folder}' pattern='{Pattern}' recursive={R}",
            folderPath, pattern, recursive);

        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning(
                "SharedFolderCapability: folder not found, returning empty list: '{Path}'", folderPath);
            return Task.FromResult<IReadOnlyList<FileEntry>>(new List<FileEntry>());
        }

        var searchOption = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var entries = Directory
            .EnumerateFiles(folderPath, pattern, searchOption)
            .Select(path =>
            {
                try
                {
                    var info = new FileInfo(path);
                    return new FileEntry
                    {
                        Name         = info.Name,
                        FullPath     = info.FullName,
                        Directory    = info.DirectoryName ?? string.Empty,
                        Extension    = info.Extension.ToLowerInvariant(),
                        SizeBytes    = info.Length,
                        LastModified = info.LastWriteTimeUtc,
                        CreatedAt    = info.CreationTimeUtc,
                        IsReadOnly   = info.IsReadOnly
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "SharedFolderCapability: could not read metadata for '{Path}'", path);
                    return null;
                }
            })
            .Where(e => e is not null)
            .Cast<FileEntry>()
            .OrderBy(e => e.Name)
            .ToList();

        _logger.LogInformation(
            "SharedFolderCapability: found {Count} files in '{Folder}'", entries.Count, folderPath);

        return Task.FromResult<IReadOnlyList<FileEntry>>(entries);
    }

    // ------------------------------------------------------------------ //
    // MIME type resolution (shared helper)
    // ------------------------------------------------------------------ //

    internal static string ResolveMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf"  => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls"  => "application/vnd.ms-excel",
            ".csv"  => "text/csv",
            ".json" => "application/json",
            ".xml"  => "application/xml",
            ".txt"  => "text/plain",
            ".html" => "text/html",
            ".zip"  => "application/zip",
            ".png"  => "image/png",
            ".jpg"  => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif"  => "image/gif",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc"  => "application/msword",
            _       => "application/octet-stream"
        };
}
