using Company.Orchestrator.Infrastructure.Capabilities.Mail;
using Xunit;

namespace Company.Orchestrator.Infrastructure.Tests.Capabilities.Mail;

public sealed class MailAttachmentArtifactNamesTests
{
  private const string XlsxMime =
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

  [Fact]
  public void BuildArtifactName_WithExistingExtension_DoesNotDuplicateExtension()
  {
    const string originalFileName = "urun-fiyat-listesi.xlsx";
    const string artifactPrefix   = "mail-file";

    var result = MailAttachmentArtifactNames.BuildArtifactName(
        originalFileName,
        artifactPrefix,
        XlsxMime);

    Assert.Equal("mail-file_urun-fiyat-listesi.xlsx", result);
  }

  [Fact]
  public void BuildArtifactName_WithoutExtension_AddsExtensionFromContentType()
  {
    var result = MailAttachmentArtifactNames.BuildArtifactName(
        "urun-fiyat-listesi",
        "mail-file",
        XlsxMime);

    Assert.Equal("mail-file_urun-fiyat-listesi.xlsx", result);
  }

  [Fact]
  public void BuildArtifactName_WithPathPrefix_UsesFileNameOnly()
  {
    var result = MailAttachmentArtifactNames.BuildArtifactName(
        @"C:\fakepath\urun-fiyat-listesi.xlsx",
        "mail-file",
        XlsxMime);

    Assert.Equal("mail-file_urun-fiyat-listesi.xlsx", result);
  }
}
