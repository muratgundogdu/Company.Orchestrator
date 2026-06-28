namespace Company.Orchestrator.Domain.Enums;

public enum TriggerEventStatus
{
    Pending    = 0,
    Processing = 1,
    Completed  = 2,
    Failed     = 3,
    Skipped    = 4
}
