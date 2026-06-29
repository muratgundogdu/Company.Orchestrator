using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Infrastructure.Capabilities.Folder;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Company.Orchestrator.Infrastructure.Tests.Capabilities.Folder;

public sealed class SharedFolderCapabilityWriteTests : IDisposable
{
    private readonly string _root;
    private readonly InMemoryArtifactStore _store = new();

    public SharedFolderCapabilityWriteTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "alterone-folder-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task WriteFileAsync_WithResolvedDirectoryPath_WritesArtifactFileName()
    {
        var outputDir = Path.Combine(_root, "Output");
        var artifact  = await CreateArtifactAsync("transformed-excel-final.xlsx", [1, 2, 3, 4]);
        var destPath  = FolderWriteDestinationPathResolver.Resolve(outputDir, artifact.Name);
        var capability = CreateCapability();

        await capability.WriteFileAsync(artifact, destPath, overwrite: true);

        var writtenPath = Path.Combine(outputDir, "transformed-excel-final.xlsx");
        Assert.True(File.Exists(writtenPath));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(writtenPath));
    }

    [Fact]
    public async Task WriteFileAsync_WithResolvedFullFilePath_WritesExactDestination()
    {
        var outputDir = Path.Combine(_root, "Output");
        var artifact  = await CreateArtifactAsync("transformed-excel-final.xlsx", [9, 8, 7]);
        var destPath  = FolderWriteDestinationPathResolver.Resolve(
            Path.Combine(outputDir, "Kur_Hesaplama.xlsx"),
            artifact.Name);
        var capability = CreateCapability();

        await capability.WriteFileAsync(artifact, destPath, overwrite: true);

        var writtenPath = Path.Combine(outputDir, "Kur_Hesaplama.xlsx");
        Assert.True(File.Exists(writtenPath));
        Assert.False(File.Exists(Path.Combine(outputDir, "transformed-excel-final.xlsx")));
        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(writtenPath));
    }

    [Fact]
    public async Task WriteFileAsync_DirectoryPath_ThrowsBeforeWriting()
    {
        var outputDir  = Path.Combine(_root, "OutputOnly");
        var artifact   = await CreateArtifactAsync("report.xlsx", [1]);
        var capability = CreateCapability();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => capability.WriteFileAsync(artifact, outputDir, overwrite: true));

        Assert.Contains("directory", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(outputDir));
    }

    private SharedFolderCapabilityImpl CreateCapability() =>
        new(_store, NullLogger<SharedFolderCapabilityImpl>.Instance);

    private async Task<ArtifactReference> CreateArtifactAsync(string name, byte[] content)
    {
        var id = Guid.NewGuid();
        await using var stream = new MemoryStream(content);
        var storagePath = await _store.SaveAsync(id, name, stream);
        return new ArtifactReference
        {
            Id          = id,
            Name        = name,
            StoragePath = storagePath,
            SizeBytes   = content.Length,
        };
    }

    private sealed class InMemoryArtifactStore : IArtifactStore
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public Task<string> SaveAsync(
            Guid artifactId, string name, Stream content, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            var path = artifactId.ToString("N");
            _files[path] = ms.ToArray();
            return Task.FromResult(path);
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(_files[storagePath]));

        public Task<byte[]> ReadAllBytesAsync(string storagePath, CancellationToken cancellationToken = default)
            => Task.FromResult(_files[storagePath]);

        public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
        {
            _files.Remove(storagePath);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
            => Task.FromResult(_files.ContainsKey(storagePath));

        public Task<long> GetSizeAsync(string storagePath, CancellationToken cancellationToken = default)
            => Task.FromResult((long)_files[storagePath].Length);
    }
}
