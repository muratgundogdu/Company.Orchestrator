using Company.Orchestrator.Application.DTOs.Dashboard;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Company.Orchestrator.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private const int TopWorkflowLimit = 10;
    private const int RecentFailureLimit = 10;

    private readonly OrchestratorDbContext _context;

    public DashboardService(OrchestratorDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardKpiDto> GetKpiAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var toUtcValue = toUtc ?? DateTime.UtcNow;
        var fromUtcValue = fromUtc ?? toUtcValue.AddHours(-24);
        if (fromUtcValue >= toUtcValue)
            fromUtcValue = toUtcValue.AddHours(-24);

        var jobRows = await (
            from j in _context.Jobs.AsNoTracking()
            join pi in _context.ProcessInstances.AsNoTracking() on j.ProcessInstanceId equals pi.Id
            join pd in _context.ProcessDefinitions.AsNoTracking() on pi.ProcessDefinitionId equals pd.Id
            where j.CreatedAt >= fromUtcValue && j.CreatedAt < toUtcValue
            select new JobRow(
                j.Id,
                j.ProcessInstanceId,
                j.Status,
                j.StartedAt,
                j.CompletedAt,
                j.CancelledAtUtc,
                j.ErrorMessage,
                pi.ErrorMessage,
                pd.Id,
                pd.Name)
        ).ToListAsync(cancellationToken);

        var instanceRows = await _context.ProcessInstances.AsNoTracking()
            .Where(i => i.CreatedAt >= fromUtcValue && i.CreatedAt < toUtcValue)
            .Select(i => i.Status)
            .ToListAsync(cancellationToken);

        var workers = await _context.WorkerHeartbeats.AsNoTracking().ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var jobStats = BuildJobStats(jobRows);
        var instanceStats = BuildInstanceStats(instanceRows);
        var workerStats = BuildWorkerStats(workers, now);
        var topWorkflows = BuildTopWorkflows(jobRows);
        var failingWorkflows = BuildFailingWorkflows(jobRows);
        var throughput = BuildThroughput(jobRows, fromUtcValue, toUtcValue);
        var recentFailures = await BuildRecentFailuresAsync(jobRows, cancellationToken);

        return new DashboardKpiDto
        {
            Range = new DashboardRangeDto { FromUtc = fromUtcValue, ToUtc = toUtcValue },
            Jobs = jobStats,
            Instances = instanceStats,
            Workers = workerStats,
            TopWorkflows = topWorkflows,
            FailingWorkflows = failingWorkflows,
            RecentFailures = recentFailures,
            ThroughputByHour = throughput,
        };
    }

    private static DashboardJobStatsDto BuildJobStats(IReadOnlyList<JobRow> jobs)
    {
        var total = jobs.Count;
        var succeeded = jobs.Count(j => j.Status == JobStatus.Success);
        var failed = jobs.Count(j => j.Status == JobStatus.Failed);
        var cancelled = jobs.Count(j => j.Status == JobStatus.Cancelled);
        var running = jobs.Count(j => j.Status is JobStatus.Running or JobStatus.Cancelling or JobStatus.Retrying);
        var pending = jobs.Count(j => j.Status == JobStatus.Pending);

        var completedDurations = jobs
            .Where(j => j.StartedAt.HasValue && j.CompletedAt.HasValue)
            .Where(j => j.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Cancelled)
            .Select(j => (j.CompletedAt!.Value - j.StartedAt!.Value).TotalSeconds)
            .ToList();

        return new DashboardJobStatsDto
        {
            Total = total,
            Succeeded = succeeded,
            Failed = failed,
            Cancelled = cancelled,
            Running = running,
            Pending = pending,
            SuccessRate = total > 0 ? Math.Round(succeeded * 100.0 / total, 1) : 0,
            FailureRate = total > 0 ? Math.Round(failed * 100.0 / total, 1) : 0,
            AverageDurationSeconds = completedDurations.Count > 0
                ? Math.Round(completedDurations.Average(), 1)
                : 0,
        };
    }

    private static DashboardInstanceStatsDto BuildInstanceStats(IReadOnlyList<ProcessStatus> statuses)
    {
        return new DashboardInstanceStatsDto
        {
            Total = statuses.Count,
            Succeeded = statuses.Count(s => s == ProcessStatus.Success),
            Failed = statuses.Count(s => s == ProcessStatus.Failed),
            Cancelled = statuses.Count(s => s == ProcessStatus.Cancelled),
        };
    }

    private static DashboardWorkerStatsDto BuildWorkerStats(
        IReadOnlyList<Domain.Entities.WorkerHeartbeat> workers,
        DateTime now)
    {
        var stats = new DashboardWorkerStatsDto
        {
            Total = workers.Count,
            RunningJobs = workers.Sum(w => w.RunningJobCount),
        };

        foreach (var worker in workers)
        {
            switch (WorkerStatusCalculator.Calculate(worker.LastHeartbeatUtc, now))
            {
                case WorkerStatus.Online: stats.Online++; break;
                case WorkerStatus.Warning: stats.Warning++; break;
                case WorkerStatus.Offline: stats.Offline++; break;
            }
        }

        return stats;
    }

    private static IReadOnlyList<TopWorkflowDto> BuildTopWorkflows(IReadOnlyList<JobRow> jobs)
    {
        return jobs
            .GroupBy(j => new { j.ProcessDefinitionId, j.ProcessName })
            .Select(g =>
            {
                var durations = g
                    .Where(j => j.StartedAt.HasValue && j.CompletedAt.HasValue)
                    .Where(j => j.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Cancelled)
                    .Select(j => (j.CompletedAt!.Value - j.StartedAt!.Value).TotalSeconds)
                    .ToList();

                return new TopWorkflowDto
                {
                    ProcessDefinitionId = g.Key.ProcessDefinitionId,
                    Name = g.Key.ProcessName,
                    RunCount = g.Count(),
                    SuccessCount = g.Count(j => j.Status == JobStatus.Success),
                    FailedCount = g.Count(j => j.Status == JobStatus.Failed),
                    AverageDurationSeconds = durations.Count > 0
                        ? Math.Round(durations.Average(), 1)
                        : 0,
                };
            })
            .OrderByDescending(w => w.RunCount)
            .Take(TopWorkflowLimit)
            .ToList();
    }

    private static IReadOnlyList<FailingWorkflowDto> BuildFailingWorkflows(IReadOnlyList<JobRow> jobs)
    {
        return jobs
            .Where(j => j.Status == JobStatus.Failed)
            .GroupBy(j => new { j.ProcessDefinitionId, j.ProcessName })
            .Select(g => new FailingWorkflowDto
            {
                ProcessDefinitionId = g.Key.ProcessDefinitionId,
                Name = g.Key.ProcessName,
                FailedCount = g.Count(),
                LastFailedAtUtc = g
                    .Select(j => j.CompletedAt ?? j.CancelledAtUtc)
                    .Where(d => d.HasValue)
                    .Max(),
            })
            .OrderByDescending(w => w.FailedCount)
            .ThenByDescending(w => w.LastFailedAtUtc)
            .Take(TopWorkflowLimit)
            .ToList();
    }

    private static IReadOnlyList<ThroughputHourDto> BuildThroughput(
        IReadOnlyList<JobRow> jobs,
        DateTime from,
        DateTime to)
    {
        var startHour = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0, DateTimeKind.Utc);
        var endHour = new DateTime(to.Year, to.Month, to.Day, to.Hour, 0, 0, DateTimeKind.Utc);

        var buckets = new Dictionary<DateTime, ThroughputHourDto>();
        for (var hour = startHour; hour <= endHour; hour = hour.AddHours(1))
        {
            buckets[hour] = new ThroughputHourDto { HourUtc = hour };
        }

        foreach (var job in jobs)
        {
            DateTime? eventTime = job.Status switch
            {
                JobStatus.Success => job.CompletedAt,
                JobStatus.Failed => job.CompletedAt,
                JobStatus.Cancelled => job.CancelledAtUtc ?? job.CompletedAt,
                _ => null,
            };

            if (!eventTime.HasValue) continue;

            var bucketHour = new DateTime(
                eventTime.Value.Year, eventTime.Value.Month, eventTime.Value.Day,
                eventTime.Value.Hour, 0, 0, DateTimeKind.Utc);

            if (!buckets.TryGetValue(bucketHour, out var bucket)) continue;

            switch (job.Status)
            {
                case JobStatus.Success: bucket.Succeeded++; break;
                case JobStatus.Failed: bucket.Failed++; break;
                case JobStatus.Cancelled: bucket.Cancelled++; break;
            }
        }

        return buckets.Values.OrderBy(b => b.HourUtc).ToList();
    }

    private async Task<IReadOnlyList<RecentFailureDto>> BuildRecentFailuresAsync(
        IReadOnlyList<JobRow> jobs,
        CancellationToken cancellationToken)
    {
        var failedJobs = jobs
            .Where(j => j.Status == JobStatus.Failed)
            .OrderByDescending(j => j.CompletedAt ?? DateTime.MinValue)
            .Take(RecentFailureLimit)
            .ToList();

        if (failedJobs.Count == 0)
            return Array.Empty<RecentFailureDto>();

        var jobIds = failedJobs.Select(j => j.JobId).ToList();
        var errorLogs = await _context.JobLogs.AsNoTracking()
            .Where(l => jobIds.Contains(l.JobId))
            .Where(l => l.Level == "Error" || l.Level == "Critical" || l.Level == "Fatal")
            .GroupBy(l => l.JobId)
            .Select(g => new { JobId = g.Key, Message = g.OrderByDescending(l => l.CreatedAt).First().Message })
            .ToDictionaryAsync(x => x.JobId, x => x.Message, cancellationToken);

        return failedJobs.Select(j => new RecentFailureDto
        {
            JobId = j.JobId,
            ProcessInstanceId = j.ProcessInstanceId,
            ProcessName = j.ProcessName,
            FailedAtUtc = j.CompletedAt,
            Error = ResolveError(j, errorLogs),
        }).ToList();
    }

    private static string? ResolveError(JobRow job, IReadOnlyDictionary<Guid, string> errorLogs)
    {
        if (!string.IsNullOrWhiteSpace(job.ErrorMessage))
            return job.ErrorMessage.Trim();

        if (!string.IsNullOrWhiteSpace(job.InstanceErrorMessage))
            return job.InstanceErrorMessage.Trim();

        return errorLogs.TryGetValue(job.JobId, out var logMessage) ? logMessage : null;
    }

    private sealed record JobRow(
        Guid JobId,
        Guid ProcessInstanceId,
        JobStatus Status,
        DateTime? StartedAt,
        DateTime? CompletedAt,
        DateTime? CancelledAtUtc,
        string? ErrorMessage,
        string? InstanceErrorMessage,
        Guid ProcessDefinitionId,
        string ProcessName);
}
