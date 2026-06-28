using Microsoft.Extensions.DependencyInjection;

namespace Company.Orchestrator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
