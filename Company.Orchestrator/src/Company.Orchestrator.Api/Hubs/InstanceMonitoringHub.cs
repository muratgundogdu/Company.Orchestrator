using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Company.Orchestrator.Api.Hubs;

[Authorize]
public sealed class InstanceMonitoringHub : Hub
{
    private readonly OrchestratorDbContext _db;
    private readonly ICurrentUser _currentUser;

    public InstanceMonitoringHub(OrchestratorDbContext db, ICurrentUser currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task JoinInstance(Guid processInstanceId)
    {
        if (!_currentUser.HasPermission(Permissions.WorkflowView))
            throw new HubException("Forbidden");

        var exists = await _db.ProcessInstances
            .AsNoTracking()
            .AnyAsync(pi => pi.Id == processInstanceId);

        if (!exists)
            throw new HubException("Process instance not found");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            Application.Monitoring.InstanceMonitoringGroups.ForInstance(processInstanceId));
    }

    public Task LeaveInstance(Guid processInstanceId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            Application.Monitoring.InstanceMonitoringGroups.ForInstance(processInstanceId));
}
