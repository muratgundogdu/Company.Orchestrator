using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Exceptions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.ProcessDefinition;
using Company.Orchestrator.Application.DTOs.ProcessVersion;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Audit;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Services;

public class ProcessDefinitionService : IProcessDefinitionService
{
    private readonly OrchestratorDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessDefinitionRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<ProcessDefinitionService> _logger;

    public ProcessDefinitionService(
        OrchestratorDbContext context,
        IUnitOfWork unitOfWork,
        IProcessDefinitionRepository repository,
        ICurrentUser currentUser,
        IAuditService audit,
        ILogger<ProcessDefinitionService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _repository = repository;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PagedResult<ProcessDefinitionDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ProcessDefinitions
            .Include(x => x.Versions)
            .OrderBy(x => x.Name);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<ProcessDefinitionDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProcessDefinitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWithVersionsAsync(id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<ProcessDefinitionDto> CreateAsync(CreateProcessDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        var nameTaken = await _context.ProcessDefinitions
            .AnyAsync(x => x.Name == name, cancellationToken);
        if (nameTaken)
        {
            throw new FieldValidationException(
                "name",
                "A process definition with this name already exists.");
        }

        var entity = new ProcessDefinition
        {
            Name = name,
            Description = request.Description,
            Category = request.Category
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created ProcessDefinition {Id} '{Name}'", entity.Id, entity.Name);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.WorkflowCreated,
            Category   = AuditCategories.Workflow,
            EntityType = "ProcessDefinition",
            EntityId   = entity.Id.ToString(),
            EntityName = entity.Name,
            Action     = "Workflow created",
        }), cancellationToken);

        return MapToDto(entity);
    }

    public async Task<ProcessDefinitionDto?> UpdateAsync(Guid id, UpdateProcessDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;

        if (request.Name is not null) entity.Name = request.Name;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.Category is not null) entity.Category = request.Category;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;

        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.WorkflowUpdated,
            Category   = AuditCategories.Workflow,
            EntityType = "ProcessDefinition",
            EntityId   = entity.Id.ToString(),
            EntityName = entity.Name,
            Action     = "Workflow updated",
        }), cancellationToken);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null) return false;

        _repository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.WorkflowDeleted,
            Category   = AuditCategories.Workflow,
            EntityType = "ProcessDefinition",
            EntityId   = entity.Id.ToString(),
            EntityName = entity.Name,
            Action     = "Workflow deleted",
        }), cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<ProcessVersionDto>> GetVersionsAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        var versions = await _context.ProcessVersions
            .Where(v => v.ProcessDefinitionId == definitionId && !v.IsDeleted)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        return versions.Select(MapVersionToDto).ToList();
    }

    public async Task<ProcessVersionDto> CreateVersionAsync(Guid definitionId, CreateProcessVersionRequest request, CancellationToken cancellationToken = default)
    {
        var maxVersion = await _context.ProcessVersions
            .Where(v => v.ProcessDefinitionId == definitionId && !v.IsDeleted)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken) ?? 0;

        var version = new ProcessVersion
        {
            ProcessDefinitionId = definitionId,
            VersionNumber = maxVersion + 1,
            JsonDefinition = request.JsonDefinition,
            ChangeNotes = request.ChangeNotes,
            Status = VersionStatus.Draft
        };

        _context.ProcessVersions.Add(version);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created ProcessVersion {Id} v{Version} for definition {DefinitionId}",
            version.Id, version.VersionNumber, definitionId);

        var definition = await _repository.GetByIdAsync(definitionId, cancellationToken);
        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.WorkflowUpdated,
            Category   = AuditCategories.Workflow,
            EntityType = "ProcessDefinition",
            EntityId   = definitionId.ToString(),
            EntityName = definition?.Name,
            Action     = "Workflow version saved",
            Details    = new { versionId = version.Id, version.VersionNumber },
        }), cancellationToken);

        return MapVersionToDto(version);
    }

    public async Task<ProcessVersionDto?> PublishVersionAsync(Guid definitionId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await _context.ProcessVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.ProcessDefinitionId == definitionId && !v.IsDeleted, cancellationToken);

        if (version is null) return null;

        // Deprecate previously published versions
        var previouslyPublished = await _context.ProcessVersions
            .Where(v => v.ProcessDefinitionId == definitionId
                     && v.Status == VersionStatus.Published
                     && v.Id != versionId
                     && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var prev in previouslyPublished)
            prev.Status = VersionStatus.Deprecated;

        version.Status = VersionStatus.Published;
        version.PublishedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Published ProcessVersion {Id} v{Version}", version.Id, version.VersionNumber);
        return MapVersionToDto(version);
    }

    private static ProcessDefinitionDto MapToDto(ProcessDefinition entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        Category = entity.Category,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
        VersionCount = entity.Versions?.Count ?? 0,
        LatestVersionNumber = entity.Versions?.Any() == true ? entity.Versions.Max(v => v.VersionNumber) : null
    };

    private static ProcessVersionDto MapVersionToDto(ProcessVersion entity) => new()
    {
        Id = entity.Id,
        ProcessDefinitionId = entity.ProcessDefinitionId,
        VersionNumber = entity.VersionNumber,
        JsonDefinition = entity.JsonDefinition,
        Status = entity.Status,
        ChangeNotes = entity.ChangeNotes,
        PublishedAt = entity.PublishedAt,
        CreatedAt = entity.CreatedAt
    };
}
