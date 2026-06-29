using Company.Orchestrator.Infrastructure.Capabilities.Folder;
using Xunit;

namespace Company.Orchestrator.Infrastructure.Tests.Capabilities.Folder;

public sealed class FolderWriteDestinationPathResolverTests
{
    [Fact]
    public void Resolve_DirectoryPath_AppendsArtifactFileName()
    {
        var result = FolderWriteDestinationPathResolver.Resolve(
            @"C:\Output",
            "transformed-excel-final.xlsx");

        Assert.Equal(
            Path.Combine(@"C:\Output", "transformed-excel-final.xlsx"),
            result);
    }

    [Fact]
    public void Resolve_DirectoryPathWithTrailingSeparator_AppendsArtifactFileName()
    {
        var result = FolderWriteDestinationPathResolver.Resolve(
            @"C:\Output\",
            "transformed-excel-final.xlsx");

        Assert.Equal(
            Path.Combine(@"C:\Output", "transformed-excel-final.xlsx"),
            result);
    }

    [Fact]
    public void Resolve_FullFilePath_KeepsDestinationAsIs()
    {
        var result = FolderWriteDestinationPathResolver.Resolve(
            @"C:\Output\Kur_Hesaplama.xlsx",
            "transformed-excel-final.xlsx");

        Assert.Equal(@"C:\Output\Kur_Hesaplama.xlsx", result);
    }

    [Fact]
    public void Resolve_ExistingDirectory_AppendsArtifactFileName()
    {
        var directory = Path.Combine(Path.GetTempPath(), "alterone-folder-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var result = FolderWriteDestinationPathResolver.Resolve(
                directory,
                "report.xlsx");

            Assert.Equal(Path.Combine(directory, "report.xlsx"), result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
