using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;

namespace Company.Orchestrator.Infrastructure.Auth;

/// <summary>
/// Backward-compatible adapter for legacy IAuditLogWriter call sites.
/// </summary>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly IAuditService _audit;

    public AuditLogWriter(IAuditService audit)
    {
        _audit = audit;
    }

    public Task WriteAsync(
        string entityName,
        string entityId,
        string action,
        string? performedBy,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var category = entityName switch
        {
            "User" => AuditCategories.UserManagement,
            "Role" => AuditCategories.RoleManagement,
            _      => AuditCategories.System,
        };

        var eventType = action switch
        {
            "Login"           => AuditEventTypes.UserLogin,
            "Create"          when entityName == "User" => AuditEventTypes.UserCreated,
            "Update"          when entityName == "User" => AuditEventTypes.UserUpdated,
            "Delete"          when entityName == "User" => AuditEventTypes.UserDeleted,
            "AssignRoles"     => AuditEventTypes.UserUpdated,
            "UpdatePermissions" => AuditEventTypes.PermissionChanged,
            _                 => action,
        };

        return _audit.WriteSuccessAsync(new AuditWriteRequest
        {
            EventType  = eventType,
            Category   = category,
            EntityType = entityName,
            EntityId   = entityId,
            Action     = action,
            Username   = performedBy,
            DetailsJson = oldValues is null && newValues is null
                ? null
                : $"{{\"oldValues\":{JsonQuote(oldValues)},\"newValues\":{JsonQuote(newValues)}}}",
            IpAddress  = ipAddress,
            UserAgent  = userAgent,
        }, cancellationToken);
    }

    private static string JsonQuote(string? value) =>
        value is null ? "null" : $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
