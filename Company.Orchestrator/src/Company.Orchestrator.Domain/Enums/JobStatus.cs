namespace Company.Orchestrator.Domain.Enums;

public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Success = 2,
    Failed = 3,
    Retrying = 4,
    Cancelled = 5,
    Cancelling = 6
}
