using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities.File;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Capabilities.File;

/// <summary>
/// Production-ready FileCapability backed by LocalFileArtifactStore.
/// Supports read, write, create from bytes/stream, list, exists, delete.
/// </summary>
public sealed class FileCapabilityImpl : IFileCapability
{
    private readonly IArtifactStore _store;
    private readonly ILogger<FileCapabilityImpl> _logger;

    public string CapabilityName => "File";

    public FileCapabilityImpl(IArtifactStore store, ILogger<FileCapabilityImpl> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<ArtifactReference> ReadFileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!System.IO.File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found: {sourcePath}");

        var name = Path.GetFileName(sourcePath);
        var id = Guid.NewGuid();
        var contentType = ResolveContentType(name);
        long size;

        await using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true))
        {
            size = fs.Length;
            var storagePath = await _store.SaveAsync(id, name, fs, cancellationToken);

            _logger.LogInformation("FileCapability: read '{Source}' → artifact {Id}", sourcePath, id);

            return new ArtifactReference
            {
                Id = id,
                Name = name,
                ContentType = contentType,
                StoragePath = storagePath,
                SizeBytes = size,
                Metadata = new Dictionary<string, string> { ["originalPath"] = sourcePath }
            };
        }
    }

    public async Task WriteFileAsync(ArtifactReference artifact, string destinationPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var content = await _store.ReadAllBytesAsync(artifact.StoragePath, cancellationToken);
        await System.IO.File.WriteAllBytesAsync(destinationPath, content, cancellationToken);
        _logger.LogInformation("FileCapability: wrote artifact {Id} → '{Dest}'", artifact.Id, destinationPath);
    }

    public async Task<ArtifactReference> CreateFromBytesAsync(
        string name, byte[] content, string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        using var ms = new MemoryStream(content);
        var storagePath = await _store.SaveAsync(id, name, ms, cancellationToken);
        return new ArtifactReference
        {
            Id = id, Name = name, ContentType = contentType,
            StoragePath = storagePath, SizeBytes = content.Length
        };
    }

    public async Task<ArtifactReference> CreateFromStreamAsync(
        string name, Stream content, string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var storagePath = await _store.SaveAsync(id, name, content, cancellationToken);
        var size = await _store.GetSizeAsync(storagePath, cancellationToken);
        return new ArtifactReference
        {
            Id = id, Name = name, ContentType = contentType,
            StoragePath = storagePath, SizeBytes = size
        };
    }

    public Task<byte[]> ReadBytesAsync(ArtifactReference artifact, CancellationToken cancellationToken = default)
        => _store.ReadAllBytesAsync(artifact.StoragePath, cancellationToken);

    public Task<Stream> OpenReadAsync(ArtifactReference artifact, CancellationToken cancellationToken = default)
        => _store.OpenReadAsync(artifact.StoragePath, cancellationToken);

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(System.IO.File.Exists(path));

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string directoryPath, string pattern = "*", CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            return Task.FromResult<IReadOnlyList<string>>(new List<string>());

        var files = Directory.GetFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly);
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private static string ResolveContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
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
        _       => "application/octet-stream"
    };
}
