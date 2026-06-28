using System.Text;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Company.Orchestrator.Infrastructure;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAlterOneAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<Application.Common.Interfaces.ICurrentUser, CurrentUser>();
        services.AddSingleton<Application.Common.Interfaces.IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<Application.Common.Interfaces.IJwtTokenService, JwtTokenService>();
        services.AddScoped<Application.Services.IAuditService, Audit.AuditService>();
        services.AddScoped<Application.Common.Interfaces.IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<Application.Services.IAuthService, AuthService>();
        services.AddScoped<Application.Services.IUserService, UserService>();
        services.AddScoped<Application.Services.IRoleService, RoleService>();
        services.AddScoped<AuthDataSeeder>();

        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditAuthorizationMiddlewareResultHandler>();

        var key = configuration["Jwt:Key"] ?? "AlterOne-Dev-Secret-Key-Min-32-Chars-Long!";
        var issuer = configuration["Jwt:Issuer"] ?? "AlterOne";
        var audience = configuration["Jwt:Audience"] ?? "AlterOne";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = issuer,
                    ValidAudience            = audience,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                };
            });

        return services;
    }
}
