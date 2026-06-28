using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Company.Orchestrator.Infrastructure.Persistence;

/// <summary>
/// Used by EF Core tools to create migrations.
/// Run: dotnet ef migrations add InitialCreate --project src/Company.Orchestrator.Infrastructure --startup-project src/Company.Orchestrator.Api
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrchestratorDbContext>
{
    public OrchestratorDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Company.Orchestrator.Api"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<OrchestratorDbContext>();
        optionsBuilder.UseSqlServer(
            configuration.GetConnectionString("DefaultConnection"),
            sql => sql.MigrationsAssembly(typeof(OrchestratorDbContext).Assembly.FullName));

        return new OrchestratorDbContext(optionsBuilder.Options);
    }
}
