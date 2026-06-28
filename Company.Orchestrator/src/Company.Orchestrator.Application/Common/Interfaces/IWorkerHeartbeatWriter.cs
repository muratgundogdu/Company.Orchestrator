namespace Company.Orchestrator.Application.Common.Interfaces;

public sealed class WorkerHeartbeatPayload
{
    public required string WorkerId { get; init; }
    public required string WorkerName { get; init; }
    public required string MachineName { get; init; }
    public required string Version { get; init; }
    public required int ProcessId { get; init; }
    public required int RunningJobCount { get; init; }
    public double? CpuUsagePercent { get; init; }
    public double? MemoryUsageMb { get; init; }
    public string? MetadataJson { get; init; }
}

public interface IWorkerHeartbeatWriter
{
    Task RecordHeartbeatAsync(WorkerHeartbeatPayload payload, CancellationToken cancellationToken = default);
    Task<int> CleanupStaleAsync(int retentionDays, CancellationToken cancellationToken = default);
}
