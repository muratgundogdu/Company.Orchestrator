using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.DTOs.Audit;
using Company.Orchestrator.Application.DTOs.Common;

namespace Company.Orchestrator.Application.Services;

public interface IAuditService
{
    Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);

    Task WriteSuccessAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);

    Task WriteFailureAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<AuditLogListItemDto>> QueryAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken = default);

    Task<AuditLogDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AuditSummaryDto> GetSummaryAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken = default);
}
