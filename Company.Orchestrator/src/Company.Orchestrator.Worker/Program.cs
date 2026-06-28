using Company.Orchestrator.Application;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Infrastructure;
using Company.Orchestrator.Infrastructure.Audit;
using Company.Orchestrator.Infrastructure.Persistence;
using Company.Orchestrator.Infrastructure.Security;
using Company.Orchestrator.Worker;
using Company.Orchestrator.Worker.Workers;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Company.Orchestrator.Worker");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/worker-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<Company.Orchestrator.Application.Common.Interfaces.ICurrentUser, WorkerCurrentUser>();
    builder.Services.AddScoped<IAuditService, AuditService>();

    Log.Information(
        "Data Protection key ring: {KeysPath} (ApplicationName={ApplicationName})",
        DataProtectionKeyPath.Resolve(builder.Configuration),
        DataProtectionKeyPath.ApplicationName);

    builder.Services.AddHostedService<JobPollerWorker>();
    builder.Services.AddHostedService<FolderWatcherWorker>();
    builder.Services.AddHostedService<ScheduledTriggerWorker>();
    builder.Services.AddHostedService<WorkerHeartbeatBackgroundService>();
    builder.Services.AddHostedService<WorkerHeartbeatCleanupBackgroundService>();

    var host = builder.Build();

    // Apply migrations on startup
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Worker: database migration completed");
    }

    await host.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
