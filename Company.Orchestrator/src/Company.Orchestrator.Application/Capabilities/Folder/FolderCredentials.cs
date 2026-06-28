namespace Company.Orchestrator.Application.Capabilities.Folder;

/// <summary>
/// Optional Windows credentials for accessing UNC shares that require authentication.
///
/// CURRENT STATUS: The SharedFolderCapabilityImpl accepts this object via UseCredentials()
/// but does NOT yet perform impersonation — it runs under the current service/user identity.
///
/// FUTURE IMPLEMENTATION:
///   Use WindowsIdentity.RunImpersonated() or LogonUser P/Invoke to impersonate the supplied
///   account before each file system operation, then revert afterward.
///   The interface slot is reserved so callers can supply credentials today without a
///   breaking change when impersonation is activated.
///
/// SECURITY NOTE:
///   Do NOT store Password in plain text in workflow definitions.
///   Use a secret-reference format (e.g. "vault:my-unc-password") and resolve via a
///   secrets provider before calling UseCredentials().
/// </summary>
public sealed class FolderCredentials
{
    /// <summary>Windows domain (e.g. "CORP"). Null for local accounts.</summary>
    public string? Domain { get; init; }

    /// <summary>Windows username (e.g. "svc_workflow").</summary>
    public string? Username { get; init; }

    /// <summary>
    /// Password. Treat as a secret — do not log.
    /// Prefer resolving from a secrets store rather than embedding in workflow JSON.
    /// </summary>
    public string? Password { get; init; }
}
