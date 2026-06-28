using Microsoft.AspNetCore.Authorization;

namespace Company.Orchestrator.Api.Authorization;

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) => Policy = permission;
}
