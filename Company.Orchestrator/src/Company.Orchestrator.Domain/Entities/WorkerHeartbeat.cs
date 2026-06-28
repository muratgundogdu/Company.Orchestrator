using Company.Orchestrator.Domain.Common;
using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Domain.Entities;

public class WorkerHeartbeat : BaseEntity
{
    public string WorkerId { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public WorkerStatus Status { get; set; } = WorkerStatus.Offline;
    public DateTime LastHeartbeatUtc { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public int RunningJobCount { get; set; }
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsageMb { get; set; }
    public int ProcessId { get; set; }
    public string? MetadataJson { get; set; }
}
