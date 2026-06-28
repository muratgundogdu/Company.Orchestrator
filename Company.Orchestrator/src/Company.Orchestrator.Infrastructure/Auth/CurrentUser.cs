using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace Company.Orchestrator.Infrastructure.Auth;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Username =>
        Principal?.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
        ?? Principal?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyList<string> Permissions =>
        Principal?.FindAll("permission").Select(c => c.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        ?? [];

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        ?? [];

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
        || Roles.Contains(DefaultRoles.Admin, StringComparer.OrdinalIgnoreCase);
}
