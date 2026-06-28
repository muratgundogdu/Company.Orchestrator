using Company.Orchestrator.Application.Artifacts;

namespace Company.Orchestrator.Application.Capabilities.File;

/// <summary>
/// Capability for file system operations.
/// Implementations translate between local/remote file paths and the ArtifactStore.
/// </summary>
public interface IFileCapability : ICapability
{
    /// <summary>Reads a file from the given path and returns an ArtifactReference pointing to the stored content.</summary>
    Task<ArtifactReference> ReadFileAsync(string sourcePath, CancellationToken cancellationToken = default);

    /// <summary>Writes artifact content to the specified destination path on the file system.</summary>
    Task WriteFileAsync(ArtifactReference artifact, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>Creates an artifact from raw bytes without touching the file system.</summary>
    Task<ArtifactReference> CreateFromBytesAsync(
        string name,
        byte[] content,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default);

    /// <summary>Creates an artifact from a stream.</summary>
    Task<ArtifactReference> CreateFromStreamAsync(
        string name,
        Stream content,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default);

    /// <summary>Reads the binary content of an existing artifact.</summary>
    Task<byte[]> ReadBytesAsync(ArtifactReference artifact, CancellationToken cancellationToken = default);

    /// <summary>Opens a readable stream over the artifact content.</summary>
    Task<Stream> OpenReadAsync(ArtifactReference artifact, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListFilesAsync(string directoryPath, string pattern = "*", CancellationToken cancellationToken = default);
}
