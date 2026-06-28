using ArtifactEntity = Company.Orchestrator.Domain.Entities.Artifact;

namespace Company.Orchestrator.Application.DTOs.Artifact;

public sealed class ArtifactDto
{
    public Guid Id { get; init; }
    public Guid ProcessInstanceId { get; init; }
    public Guid? StepInstanceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public bool IsPersistent { get; init; }
    public DateTime CreatedAt { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>Convenience link for direct download.</summary>
    public string DownloadUrl { get; init; } = string.Empty;

    public static ArtifactDto FromEntity(ArtifactEntity a, string? baseUrl = null)
    {
        Dictionary<string, string>? meta = null;
        if (!string.IsNullOrEmpty(a.Metadata))
        {
            try { meta = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(a.Metadata); }
            catch { /* ignore */ }
        }

        return new ArtifactDto
        {
            Id                = a.Id,
            ProcessInstanceId = a.ProcessInstanceId,
            StepInstanceId    = a.StepInstanceId,
            Name              = a.Name,
            ContentType       = a.ContentType,
            SizeBytes         = a.SizeBytes,
            IsPersistent      = a.IsPersistent,
            CreatedAt         = a.CreatedAt,
            Metadata          = meta,
            DownloadUrl       = $"{baseUrl?.TrimEnd('/')}/api/artifacts/{a.Id}/download"
        };
    }
}
