namespace Company.Orchestrator.Application.DTOs.Dashboard;

public sealed class DashboardKpiDto
{
    public DashboardRangeDto Range { get; set; } = new();
    public DashboardJobStatsDto Jobs { get; set; } = new();
    public DashboardInstanceStatsDto Instances { get; set; } = new();
    public DashboardWorkerStatsDto Workers { get; set; } = new();
    public IReadOnlyList<TopWorkflowDto> TopWorkflows { get; set; } = Array.Empty<TopWorkflowDto>();
    public IReadOnlyList<FailingWorkflowDto> FailingWorkflows { get; set; } = Array.Empty<FailingWorkflowDto>();
    public IReadOnlyList<RecentFailureDto> RecentFailures { get; set; } = Array.Empty<RecentFailureDto>();
    public IReadOnlyList<ThroughputHourDto> ThroughputByHour { get; set; } = Array.Empty<ThroughputHourDto>();
}

public sealed class DashboardRangeDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public sealed class DashboardJobStatsDto
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Cancelled { get; set; }
    public int Running { get; set; }
    public int Pending { get; set; }
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public double AverageDurationSeconds { get; set; }
}

public sealed class DashboardInstanceStatsDto
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Cancelled { get; set; }
}

public sealed class DashboardWorkerStatsDto
{
    public int Total { get; set; }
    public int Online { get; set; }
    public int Warning { get; set; }
    public int Offline { get; set; }
    public int RunningJobs { get; set; }
}

public sealed class TopWorkflowDto
{
    public Guid ProcessDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RunCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public double AverageDurationSeconds { get; set; }
}

public sealed class FailingWorkflowDto
{
    public Guid ProcessDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FailedCount { get; set; }
    public DateTime? LastFailedAtUtc { get; set; }
}

public sealed class RecentFailureDto
{
    public Guid JobId { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime? FailedAtUtc { get; set; }
    public string? Error { get; set; }
}

public sealed class ThroughputHourDto
{
    public DateTime HourUtc { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Cancelled { get; set; }
}
