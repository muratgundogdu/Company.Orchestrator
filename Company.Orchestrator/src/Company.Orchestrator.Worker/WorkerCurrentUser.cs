using Company.Orchestrator.Application.Common.Interfaces;

namespace Company.Orchestrator.Worker;

/// <summary>
/// Non-HTTP identity for background worker processes. Acts as a fixed system principal
/// with permissions required for credential resolution and job execution.
/// </summary>
public sealed class WorkerCurrentUser : Company.Orchestrator.Application.Common.Interfaces.ICurrentUser
{
    public static readonly Guid SystemUserId = new("00000000-0000-0000-0000-000000000001");

    private static readonly IReadOnlyList<string> SystemPermissions =
    [
        Domain.Constants.Permissions.CredentialUse,
    ];

    private static readonly IReadOnlyList<string> SystemRoles = ["System"];

    public Guid? UserId => SystemUserId;

    public string? Username => "system";

    public bool IsAuthenticated => true;

    public IReadOnlyList<string> Permissions => SystemPermissions;

    public IReadOnlyList<string> Roles => SystemRoles;

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
