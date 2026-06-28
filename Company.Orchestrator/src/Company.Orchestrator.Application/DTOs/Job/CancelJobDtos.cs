namespace Company.Orchestrator.Application.DTOs.Job;

public sealed class CancelJobRequest
{
    public string? Reason { get; set; }
    public string? CancelledBy { get; set; }
}

public sealed class CancelJobResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class JobCancellationException : Exception
{
    public JobCancellationException(string message) : base(message) { }
}
