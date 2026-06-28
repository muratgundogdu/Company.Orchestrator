using Company.Orchestrator.Domain.Entities;

namespace Company.Orchestrator.Application.Artifacts;

/// <summary>
/// Lightweight, serialisable pointer to a stored artifact.
/// Declared as a record so callers can use `with` expressions to produce renamed copies:
///   var renamed = original with { Name = "invoice-pdf" };
/// Does NOT contain binary content — content lives in IArtifactStore.
/// </summary>
public sealed record ArtifactReference
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public string StoragePath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>Creates an ArtifactReference from the domain entity (used after DB read).</summary>
    public static ArtifactReference FromEntity(Artifact entity)
    {
        var meta = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(entity.Metadata))
        {
            try
            {
                var parsed = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, string>>(entity.Metadata);
                if (parsed is not null) meta = parsed;
            }
            catch { /* ignore malformed metadata */ }
        }

        return new ArtifactReference
        {
            Id = entity.Id,
            Name = entity.Name,
            ContentType = entity.ContentType,
            StoragePath = entity.StoragePath,
            SizeBytes = entity.SizeBytes,
            Metadata = meta
        };
    }

    /// <summary>Converts this reference into the domain entity for persistence.</summary>
    public Artifact ToEntity(Guid processInstanceId, Guid? stepInstanceId = null, bool isPersistent = true)
    {
        var metaJson = Metadata.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(Metadata)
            : null;

        return new Artifact
        {
            Id = Id,
            ProcessInstanceId = processInstanceId,
            StepInstanceId = stepInstanceId,
            Name = Name,
            ContentType = ContentType,
            StoragePath = StoragePath,
            SizeBytes = SizeBytes,
            Metadata = metaJson,
            IsPersistent = isPersistent
        };
    }

    // Override ToString so the record's compiler-generated version is replaced with a readable one.
    public override string ToString() => $"Artifact[{Id:N}] '{Name}' ({ContentType}, {SizeBytes} bytes)";
}
