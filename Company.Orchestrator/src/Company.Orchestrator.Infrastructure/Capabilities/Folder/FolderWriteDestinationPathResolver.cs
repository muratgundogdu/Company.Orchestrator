namespace Company.Orchestrator.Infrastructure.Capabilities.Folder;

/// <summary>
/// Resolves folder.write-file destination paths.
/// When <paramref name="destinationPath"/> is a directory, combines it with the artifact file name.
/// </summary>
public static class FolderWriteDestinationPathResolver
{
    public static string Resolve(string destinationPath, string artifactFileName)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path must not be empty.", nameof(destinationPath));

        if (string.IsNullOrWhiteSpace(artifactFileName))
            throw new ArgumentException("Artifact file name must not be empty.", nameof(artifactFileName));

        var trimmed = destinationPath.Trim();

        if (!IsDirectoryPath(trimmed))
            return trimmed;

        var directory = trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(directory, artifactFileName);
    }

    internal static bool IsDirectoryPath(string path)
    {
        if (path.Length == 0)
            return false;

        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            return true;

        if (System.IO.Directory.Exists(path))
            return true;

        if (System.IO.File.Exists(path))
            return false;

        var fileName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(fileName))
            return true;

        return !Path.HasExtension(fileName);
    }
}
