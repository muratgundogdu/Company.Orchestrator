namespace Company.Orchestrator.Application.Capabilities.Folder;

/// <summary>
/// Metadata returned by ISharedFolderCapability.ListFilesAsync.
/// Rich alternative to a plain string path: includes size, timestamps, and extension.
/// </summary>
public sealed record FileEntry
{
    /// <summary>File name with extension (no directory part).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Absolute path including directory and file name.</summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>Directory containing the file (no trailing separator).</summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>Extension in lower-case, including the dot, e.g. ".xlsx".</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>UTC timestamp of the last write.</summary>
    public DateTime LastModified { get; init; }

    /// <summary>UTC timestamp of file creation.</summary>
    public DateTime CreatedAt { get; init; }

    public bool IsReadOnly { get; init; }

    public override string ToString() => $"{Name} ({SizeBytes:N0} bytes, {LastModified:u})";
}
