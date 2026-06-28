using Company.Orchestrator.Domain.Enums;

namespace Company.Orchestrator.Infrastructure.Workers;

public static class WorkerStatusCalculator
{
    public const int OnlineThresholdSeconds = 60;
    public const int OfflineThresholdSeconds = 180;

    public static WorkerStatus Calculate(DateTime lastHeartbeatUtc, DateTime utcNow)
    {
        var ageSeconds = (utcNow - lastHeartbeatUtc).TotalSeconds;

        if (ageSeconds < OnlineThresholdSeconds)
            return WorkerStatus.Online;

        if (ageSeconds <= OfflineThresholdSeconds)
            return WorkerStatus.Warning;

        return WorkerStatus.Offline;
    }

    public static string ToDisplayName(WorkerStatus status) => status switch
    {
        WorkerStatus.Online  => "Online",
        WorkerStatus.Warning => "Warning",
        WorkerStatus.Offline => "Offline",
        _                    => status.ToString(),
    };
}
