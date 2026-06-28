using Company.Orchestrator.Application.DTOs.Dashboard;

namespace Company.Orchestrator.Application.Services;

public interface IDashboardService
{
    Task<DashboardKpiDto> GetKpiAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);
}
