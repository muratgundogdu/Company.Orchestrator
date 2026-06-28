using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Infrastructure.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Company.Orchestrator.Infrastructure.Auth;

public sealed class AuditAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var audit = context.RequestServices.GetService<IAuditService>();
            var currentUser = context.RequestServices.GetService<ICurrentUser>();

            if (audit is not null)
            {
                var request = AuditService.FromCurrentUser(currentUser, new AuditWriteRequest
                {
                    EventType  = AuditEventTypes.AccessDenied,
                    Category   = AuditCategories.Authorization,
                    Severity   = AuditSeverity.Warning,
                    EntityType = "Endpoint",
                    EntityId   = context.Request.Path.Value ?? "/",
                    Action     = "Access denied",
                    Success    = false,
                    IpAddress  = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent  = context.Request.Headers.UserAgent.ToString(),
                    Details    = new
                    {
                        method = context.Request.Method,
                        path   = context.Request.Path.Value,
                    },
                });

                await audit.WriteFailureAsync(request);
            }
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
