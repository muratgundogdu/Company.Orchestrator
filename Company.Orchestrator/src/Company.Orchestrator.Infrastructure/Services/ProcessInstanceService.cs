using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Job;
using Company.Orchestrator.Application.DTOs.ProcessInstance;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Audit;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Services;

public class ProcessInstanceService : IProcessInstanceService
{
    private readonly OrchestratorDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessDefinitionRepository _definitionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<ProcessInstanceService> _logger;

    public ProcessInstanceService(
        OrchestratorDbContext context,
        IUnitOfWork unitOfWork,
        IProcessDefinitionRepository definitionRepository,
        ICurrentUser currentUser,
        IAuditService audit,
        ILogger<ProcessInstanceService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _definitionRepository = definitionRepository;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PagedResult<ProcessInstanceDto>> GetAllAsync(
        int page, int pageSize, Guid? definitionId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ProcessInstances
            .Include(x => x.ProcessDefinition)
            .Include(x => x.ProcessVersion)
            .AsQueryable();

        if (definitionId.HasValue)
            query = query.Where(x => x.ProcessDefinitionId == definitionId.Value);

        query = query.OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<ProcessInstanceDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProcessInstanceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ProcessInstances
            .Include(x => x.ProcessDefinition)
            .Include(x => x.ProcessVersion)
            .Include(x => x.StepInstances)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapToDtoWithSteps(entity);
    }

    public async Task<ProcessInstanceDto> StartAsync(StartProcessRequest request, CancellationToken cancellationToken = default)
    {
        var publishedVersion = await _definitionRepository.GetPublishedVersionAsync(
            request.ProcessDefinitionId, cancellationToken);

        if (publishedVersion is null)
            throw new InvalidOperationException(
                $"No published version found for ProcessDefinition {request.ProcessDefinitionId}");

        var instance = new ProcessInstance
        {
            ProcessDefinitionId = request.ProcessDefinitionId,
            ProcessVersionId = publishedVersion.Id,
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString(),
            InputData = request.InputData,
            TriggeredBy = request.TriggeredBy ?? "Api",
            Status = ProcessStatus.Pending
        };

        _context.ProcessInstances.Add(instance);

        var job = new Job
        {
            ProcessInstanceId = instance.Id,
            Status = JobStatus.Pending,
            MaxAttempts = 3,
            ScheduledAt = DateTime.UtcNow
        };

        _context.Jobs.Add(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started ProcessInstance {InstanceId} for definition {DefinitionId}, Job {JobId} queued",
            instance.Id, request.ProcessDefinitionId, job.Id);

        var definition = await _definitionRepository.GetByIdAsync(request.ProcessDefinitionId, cancellationToken);
        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.WorkflowExecuted,
            Category   = AuditCategories.Workflow,
            EntityType = "ProcessInstance",
            EntityId   = instance.Id.ToString(),
            EntityName = definition?.Name,
            Action     = "Workflow executed",
            Details    = new { jobId = job.Id, definitionId = request.ProcessDefinitionId },
        }), cancellationToken);

        return MapToDto(instance);
    }

    public async Task<bool> CancelAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _context.ProcessInstances
            .Include(x => x.Jobs)
            .FirstOrDefaultAsync(x => x.Id == instanceId, cancellationToken);

        if (instance is null) return false;

        if (instance.Status is ProcessStatus.Success or ProcessStatus.Failed or ProcessStatus.Cancelled)
            return false;

        instance.Status = ProcessStatus.Cancelled;
        instance.CompletedAt = DateTime.UtcNow;

        foreach (var job in instance.Jobs.Where(j => j.Status is JobStatus.Pending or JobStatus.Retrying))
        {
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ProcessInstanceDto MapToDto(ProcessInstance entity) => new()
    {
        Id = entity.Id,
        ProcessDefinitionId = entity.ProcessDefinitionId,
        ProcessDefinitionName = entity.ProcessDefinition?.Name ?? "",
        ProcessVersionId = entity.ProcessVersionId,
        VersionNumber = entity.ProcessVersion?.VersionNumber ?? 0,
        Status = entity.Status,
        CorrelationId = entity.CorrelationId,
        InputData = entity.InputData,
        OutputData = entity.OutputData,
        ErrorMessage = entity.ErrorMessage,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        TriggeredBy = entity.TriggeredBy,
        CreatedAt = entity.CreatedAt
    };

    public async Task<IReadOnlyList<JobLogDto>> GetLogsAsync(
        Guid instanceId, CancellationToken cancellationToken = default)
    {
        var logs = await _context.JobLogs
            .Where(l => l.Job.ProcessInstanceId == instanceId)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new JobLogDto
            {
                Id              = l.Id,
                JobId           = l.JobId,
                StepInstanceId  = l.StepInstanceId,
                Level           = l.Level,
                Message         = l.Message,
                Details         = l.Details,
                Exception       = l.Exception,
                CreatedAt       = l.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return logs;
    }

    private static ProcessInstanceDto MapToDtoWithSteps(ProcessInstance entity)
    {
        var dto = MapToDto(entity);
        dto.Steps = entity.StepInstances?.Select(s => new StepInstanceDto
        {
            Id            = s.Id,
            StepId        = s.StepId,
            StepName      = s.StepName,
            StepType      = s.StepType,
            Status        = s.Status,
            ErrorMessage  = s.ErrorMessage,
            StartedAt     = s.StartedAt,
            CompletedAt   = s.CompletedAt,
            DurationMs    = s.DurationMs,
            AttemptNumber = s.AttemptNumber,
        }).ToList() ?? new List<StepInstanceDto>();
        return dto;
    }
}
