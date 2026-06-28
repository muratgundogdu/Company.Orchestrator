namespace Company.Orchestrator.Application.DTOs.Credential;

public sealed class CredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CreateCredentialRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SecretValue { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
}

public sealed class UpdateCredentialRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>When null or empty, the existing secret is kept.</summary>
    public string? SecretValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
}

public sealed class CredentialTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
