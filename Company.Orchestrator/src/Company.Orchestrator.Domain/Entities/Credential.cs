using Company.Orchestrator.Domain.Common;

namespace Company.Orchestrator.Domain.Entities;

public class Credential : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? CreatedByUserId { get; set; }
    /// <summary>Optional comma-separated role names allowed to use this credential.</summary>
    public string? AllowedRoles { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
}
