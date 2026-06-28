namespace Company.Orchestrator.Application.DTOs.Worker;

public class WorkerListItemDto
{
    public string WorkerId { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastHeartbeatUtc { get; set; }
    public int RunningJobCount { get; set; }
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsageMb { get; set; }
}

public sealed class WorkerDetailDto : WorkerListItemDto
{
    public DateTime StartedAtUtc { get; set; }
    public int ProcessId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class WorkerSummaryDto
{
    public int Total { get; set; }
    public int Online { get; set; }
    public int Warning { get; set; }
    public int Offline { get; set; }
    public int RunningJobs { get; set; }
}
