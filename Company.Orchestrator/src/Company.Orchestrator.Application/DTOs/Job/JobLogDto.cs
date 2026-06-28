namespace Company.Orchestrator.Application.DTOs.Job;

public class JobLogDto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid? StepInstanceId { get; set; }
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Exception { get; set; }
    public DateTime CreatedAt { get; set; }
}
