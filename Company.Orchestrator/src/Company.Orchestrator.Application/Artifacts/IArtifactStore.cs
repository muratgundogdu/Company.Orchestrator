namespace Company.Orchestrator.Application.Artifacts;

/// <summary>
/// Content-addressable binary store for artifact payloads.
/// Implementations: LocalFileArtifactStore, AzureBlobArtifactStore, S3ArtifactStore …
/// This interface is infrastructure-level — step handlers never call it directly.
/// Capabilities call it to persist/load content; the engine calls it for cleanup.
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Saves binary content under the given artifactId.
    /// Returns the opaque storage path to embed in ArtifactReference.StoragePath.
    /// </summary>
    Task<string> SaveAsync(Guid artifactId, string name, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens a read stream over the stored content. Caller must dispose the stream.</summary>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Returns all bytes for the given storage path.</summary>
    Task<byte[]> ReadAllBytesAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes the content at the given storage path.</summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Returns the total size in bytes of the stored content.</summary>
    Task<long> GetSizeAsync(string storagePath, CancellationToken cancellationToken = default);
}
