using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Job;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Audit;
using Company.Orchestrator.Infrastructure.Persistence;using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Services;

public class JobService : IJobService
{
    private readonly OrchestratorDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<JobService> _logger;

    public JobService(
        OrchestratorDbContext context,
        ICurrentUser currentUser,
        IAuditService audit,
        ILogger<JobService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PagedResult<JobDto>> GetAllAsync(
        int page, int pageSize, Guid? instanceId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Jobs.AsQueryable();

        if (instanceId.HasValue)
            query = query.Where(j => j.ProcessInstanceId == instanceId.Value);

        query = query.OrderByDescending(j => j.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<JobDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<JobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs.FindAsync(new object[] { id }, cancellationToken);
        return job is null ? null : MapToDto(job);
    }

    public async Task<bool> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs.FindAsync(new object[] { jobId }, cancellationToken);
        if (job is null || job.Status != JobStatus.Failed) return false;

        job.Status = JobStatus.Pending;
        job.AttemptCount = 0;
        job.ErrorMessage = null;
        job.LockedAt = null;
        job.WorkerInstanceId = null;
        job.NextRetryAt = null;
        job.ScheduledAt = DateTime.UtcNow;
        job.CancelRequestedAtUtc = null;
        job.CancelledAtUtc = null;
        job.CancelReason = null;
        job.CancelledBy = null;
        job.CompletedAt = null;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Job {JobId} queued for retry", jobId);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.JobRetried,
            Category   = AuditCategories.Job,
            EntityType = "Job",
            EntityId   = jobId.ToString(),
            Action     = "Job retried",
        }), cancellationToken);

        return true;
    }

    public async Task<CancelJobResponse?> CancelAsync(
        Guid jobId,
        CancelJobRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs
            .Include(j => j.ProcessInstance)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
            return null;

        var instance = job.ProcessInstance
            ?? throw new InvalidOperationException($"Job {jobId} has no process instance.");

        if (job.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Cancelled)
        {
            throw new JobCancellationException(
                $"Cannot cancel job in {FormatStatus(job.Status)} status.");
        }

        if (job.Status == JobStatus.Cancelling)
        {
            return new CancelJobResponse
            {
                JobId  = job.Id,
                Status = FormatStatus(job.Status),
            };
        }

        var now         = DateTime.UtcNow;
        var reason      = string.IsNullOrWhiteSpace(request.Reason)
            ? "User requested cancellation"
            : request.Reason.Trim();
        var cancelledBy = string.IsNullOrWhiteSpace(request.CancelledBy)
            ? "Admin"
            : request.CancelledBy.Trim();

        job.CancelReason = reason;
        job.CancelledBy  = cancelledBy;

        if (job.Status is JobStatus.Pending or JobStatus.Retrying)
        {
            job.Status         = JobStatus.Cancelled;
            job.CancelledAtUtc = now;
            job.CompletedAt    = now;
            job.LockedAt       = null;
            job.WorkerInstanceId = null;
            job.ErrorMessage   = null;

            if (instance.Status is not (
                ProcessStatus.Success or ProcessStatus.Failed or ProcessStatus.Cancelled))
            {
                instance.Status      = ProcessStatus.Cancelled;
                instance.CompletedAt = now;
                instance.ErrorMessage = null;
            }

            await AddJobLogAsync(job.Id, "Information", "Cancellation requested", cancellationToken,
                details: reason);
            await AddJobLogAsync(job.Id, "Information", "Job cancelled before execution", cancellationToken);

            _logger.LogInformation("Job {JobId} cancelled while pending", jobId);
        }
        else if (job.Status == JobStatus.Running)
        {
            job.Status                = JobStatus.Cancelling;
            job.CancelRequestedAtUtc  = now;

            await AddJobLogAsync(job.Id, "Information", "Cancellation requested", cancellationToken,
                details: reason);

            _logger.LogInformation("Job {JobId} cancellation requested while running", jobId);
        }
        else
        {
            throw new JobCancellationException(
                $"Cannot cancel job in {FormatStatus(job.Status)} status.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.JobCancelled,
            Category   = AuditCategories.Job,
            EntityType = "Job",
            EntityId   = job.Id.ToString(),
            Action     = "Job cancelled",
            Details    = new { reason, cancelledBy, status = FormatStatus(job.Status) },
        }), cancellationToken);

        return new CancelJobResponse
        {
            JobId  = job.Id,
            Status = FormatStatus(job.Status),
        };
    }

    private async Task AddJobLogAsync(
        Guid jobId,
        string level,
        string message,
        CancellationToken cancellationToken,
        string? details = null)
    {
        _context.JobLogs.Add(new JobLog
        {
            JobId   = jobId,
            Level   = level,
            Message = message,
            Details = details,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string FormatStatus(JobStatus status) => status switch
    {
        JobStatus.Pending    => "Pending",
        JobStatus.Running    => "Running",
        JobStatus.Success    => "Completed",
        JobStatus.Failed     => "Failed",
        JobStatus.Retrying   => "Retrying",
        JobStatus.Cancelled  => "Cancelled",
        JobStatus.Cancelling => "Cancelling",
        _                    => status.ToString(),
    };

    private static JobDto MapToDto(Job job) => new()
    {
        Id                   = job.Id,
        ProcessInstanceId    = job.ProcessInstanceId,
        Status               = job.Status,
        AttemptCount         = job.AttemptCount,
        MaxAttempts          = job.MaxAttempts,
        ScheduledAt          = job.ScheduledAt,
        StartedAt            = job.StartedAt,
        CompletedAt          = job.CompletedAt,
        ErrorMessage         = job.ErrorMessage,
        CancelRequestedAtUtc = job.CancelRequestedAtUtc,
        CancelledAtUtc       = job.CancelledAtUtc,
        CancelReason         = job.CancelReason,
        CancelledBy          = job.CancelledBy,
        CreatedAt            = job.CreatedAt,
    };
}
