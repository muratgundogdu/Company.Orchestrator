using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Common.Exceptions;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.DTOs.Common;
using Company.Orchestrator.Application.DTOs.Credential;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Infrastructure.Audit;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Services;

public sealed class CredentialService : ICredentialService
{
    private readonly OrchestratorDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICredentialRepository _repository;
    private readonly ISecretProtector _protector;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<CredentialService> _logger;

    public CredentialService(
        OrchestratorDbContext context,
        IUnitOfWork unitOfWork,
        ICredentialRepository repository,
        ISecretProtector protector,
        ICurrentUser currentUser,
        IAuditService audit,
        ILogger<CredentialService> logger)
    {
        _context     = context;
        _unitOfWork  = unitOfWork;
        _repository  = repository;
        _protector   = protector;
        _currentUser = currentUser;
        _audit       = audit;
        _logger      = logger;
    }

    public async Task<PagedResult<CredentialDto>> GetAllAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Credentials.OrderBy(x => x.Name);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CredentialDto>
        {
            Items      = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<CredentialDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<CredentialDto> CreateAsync(
        CreateCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        ValidateMetadata(name, request.Type);

        if (string.IsNullOrWhiteSpace(request.SecretValue))
            throw new FieldValidationException("Secret value is required.", "secretValue");

        if (await _repository.NameExistsAsync(name, cancellationToken: cancellationToken))
            throw new FieldValidationException($"Credential name '{name}' already exists.", "name");

        var entity = new Credential
        {
            Name            = name,
            Type            = request.Type.Trim(),
            Description     = request.Description?.Trim(),
            CreatedBy       = _currentUser.Username ?? request.CreatedBy?.Trim(),
            CreatedByUserId = _currentUser.UserId,
            EncryptedValue  = _protector.Protect(request.SecretValue),
            IsActive        = true,
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Credential created: {CredentialName} ({CredentialType})", entity.Name, entity.Type);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.CredentialCreated,
            Category   = AuditCategories.Credential,
            EntityType = "Credential",
            EntityId   = entity.Id.ToString(),
            EntityName = entity.Name,
            Action     = "Credential created",
            Details    = new { entity.Type },
        }), cancellationToken);

        return MapToDto(entity);
    }

    public async Task<CredentialDto?> UpdateAsync(
        Guid id,
        UpdateCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var name = request.Name.Trim();
        ValidateMetadata(name, request.Type);

        if (await _repository.NameExistsAsync(name, id, cancellationToken))
            throw new FieldValidationException($"Credential name '{name}' already exists.", "name");

        entity.Name        = name;
        entity.Type        = request.Type.Trim();
        entity.Description = request.Description?.Trim();
        entity.IsActive    = request.IsActive;
        entity.ExpiresAt   = request.ExpiresAt;

        if (!string.IsNullOrWhiteSpace(request.SecretValue))
            entity.EncryptedValue = _protector.Protect(request.SecretValue);

        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Credential updated: {CredentialName} ({CredentialType})", entity.Name, entity.Type);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.CredentialUpdated,
            Category   = AuditCategories.Credential,
            EntityType = "Credential",
            EntityId   = entity.Id.ToString(),
            EntityName = entity.Name,
            Action     = "Credential updated",
            Details    = new { entity.Type, entity.IsActive },
        }), cancellationToken);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return false;

        _repository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Credential deleted: {CredentialName}", entity.Name);

        await _audit.WriteSuccessAsync(AuditService.FromCurrentUser(_currentUser, new AuditWriteRequest
        {
            EventType  = AuditEventTypes.CredentialDeleted,
            Category   = AuditCategories.Credential,
            EntityType = "Credential",
            EntityId   = entity.Id.ToString(),
            EntityName = entity.Name,
            Action     = "Credential deleted",
        }), cancellationToken);

        return true;
    }

    public Task<CredentialTestResponse> TestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CredentialTestResponse
        {
            Success = true,
            Message = "Credential test connection is not implemented yet.",
        });
    }

    private static void ValidateMetadata(string name, string type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new FieldValidationException("Name is required.", "name");

        if (string.IsNullOrWhiteSpace(type))
            throw new FieldValidationException("Type is required.", "type");

        if (!CredentialTypes.All.Contains(type.Trim(), StringComparer.Ordinal))
        {
            throw new FieldValidationException(
                $"Type must be one of: {string.Join(", ", CredentialTypes.All)}.",
                "type");
        }
    }

    private static CredentialDto MapToDto(Credential entity) => new()
    {
        Id          = entity.Id,
        Name        = entity.Name,
        Type        = entity.Type,
        Description = entity.Description,
        CreatedBy   = entity.CreatedBy,
        IsActive    = entity.IsActive,
        ExpiresAt   = entity.ExpiresAt,
        CreatedAt   = entity.CreatedAt,
        UpdatedAt   = entity.UpdatedAt,
    };
}
