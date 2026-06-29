namespace Company.Orchestrator.Application.Monitoring;

public static class InstanceMonitoringEventNames
{
    public const string StepStarted   = "instance.step.started";
    public const string StepCompleted = "instance.step.completed";
    public const string StepFailed    = "instance.step.failed";
    public const string InstanceCompleted = "instance.completed";
}

public static class InstanceMonitoringGroups
{
    public static string ForInstance(Guid processInstanceId) => $"instance-{processInstanceId}";
}
