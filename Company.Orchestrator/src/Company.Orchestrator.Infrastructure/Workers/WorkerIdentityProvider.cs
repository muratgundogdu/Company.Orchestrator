using System.Reflection;
using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Workers;

internal sealed class WorkerIdentityFile
{
    public string WorkerId { get; set; } = string.Empty;
}

/// <summary>
/// Loads or creates a persistent worker identity stored in %ProgramData%\AlterOne\worker.json.
/// </summary>
public sealed class WorkerIdentityProvider : IWorkerIdentityProvider
{
    private readonly string _workerId;
    private readonly string _workerName;
    private readonly string _version;

    public WorkerIdentityProvider(IConfiguration configuration, ILogger<WorkerIdentityProvider> logger)
    {
        _workerName = configuration["Worker:Name"]?.Trim()
            ?? Environment.MachineName;

        _version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetName().Version?.ToString(3) ?? "1.0.0";

        var configuredId = configuration["Worker:Id"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredId))
        {
            _workerId = configuredId;
            logger.LogInformation("Worker identity loaded from configuration: {WorkerId}", _workerId);
            return;
        }

        var filePath = ResolveWorkerIdentityPath(configuration);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        if (File.Exists(filePath))
        {
            var existing = JsonSerializer.Deserialize<WorkerIdentityFile>(
                File.ReadAllText(filePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!string.IsNullOrWhiteSpace(existing?.WorkerId))
            {
                _workerId = existing.WorkerId.Trim();
                logger.LogInformation("Worker identity loaded from {Path}: {WorkerId}", filePath, _workerId);
                return;
            }
        }

        _workerId = $"worker-{Environment.MachineName.ToLowerInvariant()}-{Guid.NewGuid():N}";
        if (_workerId.Length > 100)
            _workerId = _workerId[..100];

        var payload = new WorkerIdentityFile { WorkerId = _workerId };
        File.WriteAllText(filePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        logger.LogInformation("Worker registered with new identity {WorkerId} at {Path}", _workerId, filePath);
    }

    public string WorkerId => _workerId;
    public string WorkerName => _workerName;
    public string Version => _version;

    public static string ResolveWorkerIdentityPath(IConfiguration configuration)
    {
        var configured = configuration["Worker:IdentityPath"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AlterOne",
            "worker.json");
    }
}
