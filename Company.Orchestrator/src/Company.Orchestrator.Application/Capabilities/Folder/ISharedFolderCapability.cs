using Company.Orchestrator.Application.Artifacts;

namespace Company.Orchestrator.Application.Capabilities.Folder;

/// <summary>
/// Capability for direct file system operations, including Windows UNC share paths.
///
/// Design goals:
///   1. Support all common CRUD operations on files and directories.
///   2. Integrate with IArtifactStore: read → artifact, write ← artifact.
///   3. UNC paths work natively on Windows via System.IO — no special handling required.
///   4. Credential impersonation slot reserved for future implementation (UseCredentials).
///   5. Intentionally separate from IFileCapability to avoid interface pollution and to allow
///      independent evolution (e.g. impersonation, connection pools, retries).
///
/// Usage:
///   var folder = context.GetCapability&lt;ISharedFolderCapability&gt;();
///   var artifact = await folder.ReadFileAsync(@"\\server\share\reports\data.xlsx", ct);
/// </summary>
public interface ISharedFolderCapability : ICapability
{
    // ------------------------------------------------------------------ //
    // File read / write (artifact-integrated)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Reads a file from <paramref name="sourcePath"/> (local or UNC) and stores it
    /// in the artifact store. Returns an ArtifactReference with the stored content.
    /// </summary>
    Task<ArtifactReference> ReadFileAsync(
        string sourcePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the content of <paramref name="artifact"/> to <paramref name="destinationPath"/>.
    /// Creates any missing parent directories automatically.
    /// </summary>
    Task WriteFileAsync(
        ArtifactReference artifact,
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------ //
    // File operations (path-to-path, no artifact store involvement)
    // ------------------------------------------------------------------ //

    /// <summary>Copies a file. Creates the destination directory if it is missing.</summary>
    Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves (renames) a file. Creates the destination directory if it is missing.
    /// On cross-device moves this falls back to copy + delete.
    /// </summary>
    Task MoveFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a file. Silently succeeds if the file does not exist.</summary>
    Task DeleteFileAsync(
        string path, CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------ //
    // Existence checks
    // ------------------------------------------------------------------ //

    /// <summary>Returns true if a file exists at <paramref name="path"/>.</summary>
    Task<bool> FileExistsAsync(
        string path, CancellationToken cancellationToken = default);

    /// <summary>Returns true if a directory exists at <paramref name="path"/>.</summary>
    Task<bool> DirectoryExistsAsync(
        string path, CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------ //
    // Directory management
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Creates the directory at <paramref name="path"/> and any missing parents.
    /// No-op if the directory already exists.
    /// </summary>
    Task CreateDirectoryAsync(
        string path, CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------ //
    // Directory listing
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Lists files in <paramref name="folderPath"/> matching <paramref name="pattern"/>.
    /// Returns rich <see cref="FileEntry"/> objects including size and timestamps.
    /// Returns an empty list if the folder does not exist.
    /// </summary>
    Task<IReadOnlyList<FileEntry>> ListFilesAsync(
        string folderPath,
        string pattern = "*",
        bool recursive = false,
        CancellationToken cancellationToken = default);

    // ------------------------------------------------------------------ //
    // Future: credential impersonation (no-op until implemented)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Configures optional Windows credentials for UNC share access.
    /// Currently accepted but not applied — the service identity is used.
    /// Call before any file operation when impersonation will eventually be required.
    /// </summary>
    void UseCredentials(FolderCredentials? credentials);
}
