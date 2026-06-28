using Company.Orchestrator.Application.Artifacts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Artifacts;

/// <summary>
/// IArtifactStore backed by the local file system.
/// Suitable for development and single-server deployments.
/// Replace with AzureBlobArtifactStore / S3ArtifactStore for multi-server setups.
///
/// Storage layout:
///   {RootPath}/{year}/{month}/{artifactId}_{safeName}
/// </summary>
public sealed class LocalFileArtifactStore : IArtifactStore
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileArtifactStore> _logger;

    public LocalFileArtifactStore(IConfiguration configuration, ILogger<LocalFileArtifactStore> logger)
    {
        _logger = logger;
        _rootPath = configuration["ArtifactStore:RootPath"]
                    ?? Path.Combine(AppContext.BaseDirectory, "artifacts");
        Directory.CreateDirectory(_rootPath);
        _logger.LogInformation("LocalFileArtifactStore root: {Root}", _rootPath);
    }

    public async Task<string> SaveAsync(Guid artifactId, string name, Stream content, CancellationToken cancellationToken = default)
    {
        var relativePath = BuildRelativePath(artifactId, name);
        var fullPath = Path.Combine(_rootPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(fileStream, cancellationToken);

        _logger.LogDebug("Artifact {ArtifactId} saved to {Path}", artifactId, relativePath);
        return relativePath;
    }

    public async Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(storagePath);
        EnsureExists(fullPath, storagePath);
        await Task.CompletedTask; // satisfy async contract
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
    }

    public async Task<byte[]> ReadAllBytesAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(storagePath);
        EnsureExists(fullPath, storagePath);
        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("Artifact deleted at {Path}", storagePath);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(Resolve(storagePath)));

    public Task<long> GetSizeAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(storagePath);
        EnsureExists(fullPath, storagePath);
        return Task.FromResult(new FileInfo(fullPath).Length);
    }

    // ---- Helpers ----

    private static string BuildRelativePath(Guid artifactId, string name)
    {
        var now = DateTime.UtcNow;
        var safeName = SanitiseFileName(name);
        return Path.Combine(now.Year.ToString(), now.Month.ToString("00"),
            $"{artifactId:N}_{safeName}");
    }

    private string Resolve(string storagePath) => Path.Combine(_rootPath, storagePath);

    private static void EnsureExists(string fullPath, string storagePath)
    {
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Artifact content not found at '{storagePath}'.", fullPath);
    }

    private static string SanitiseFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return safe.Length > 80 ? safe[..80] : safe;
    }
}
