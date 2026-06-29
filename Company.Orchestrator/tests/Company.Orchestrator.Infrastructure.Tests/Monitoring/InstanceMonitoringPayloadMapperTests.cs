using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Monitoring;
using Xunit;

namespace Company.Orchestrator.Infrastructure.Tests.Monitoring;

public sealed class InstanceMonitoringPayloadMapperTests
{
    [Fact]
    public void ToStepStarted_MapsRunningPayload()
    {
        var startedAt = new DateTime(2026, 6, 29, 10, 0, 0, DateTimeKind.Utc);
        var step = new ProcessStepInstance
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = Guid.NewGuid(),
            StepId = "read-excel",
            StepName = "Read Excel",
            StartedAt = startedAt,
        };

        var payload = InstanceMonitoringPayloadMapper.ToStepStarted(step);

        Assert.Equal(step.ProcessInstanceId, payload.ProcessInstanceId);
        Assert.Equal(step.Id, payload.StepInstanceId);
        Assert.Equal("read-excel", payload.StepKey);
        Assert.Equal("Read Excel", payload.StepName);
        Assert.Equal("Running", payload.Status);
        Assert.Equal(startedAt, payload.StartedAt);
    }

    [Fact]
    public void ToStepCompleted_MapsSuccessFields()
    {
        var completedAt = new DateTime(2026, 6, 29, 10, 1, 0, DateTimeKind.Utc);
        var step = new ProcessStepInstance
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = Guid.NewGuid(),
            StepId = "transform",
            StepName = "Transform",
            CompletedAt = completedAt,
            DurationMs = 1500,
        };

        var payload = InstanceMonitoringPayloadMapper.ToStepCompleted(step);

        Assert.Equal("Completed", payload.Status);
        Assert.Equal(completedAt, payload.CompletedAt);
        Assert.Equal(1500, payload.DurationMs);
    }

    [Fact]
    public void ToStepFailed_IncludesErrorMessage()
    {
        var step = new ProcessStepInstance
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = Guid.NewGuid(),
            StepId = "mail-send",
            StepName = "Send Mail",
            ErrorMessage = "SMTP timeout",
            CompletedAt = DateTime.UtcNow,
        };

        var payload = InstanceMonitoringPayloadMapper.ToStepFailed(step);

        Assert.Equal("Failed", payload.Status);
        Assert.Equal("SMTP timeout", payload.ErrorMessage);
    }

    [Theory]
    [InlineData(ProcessStatus.Success, "Success")]
    [InlineData(ProcessStatus.Failed, "Failed")]
    [InlineData(ProcessStatus.Cancelled, "Cancelled")]
    [InlineData(ProcessStatus.Running, "Running")]
    public void ToInstanceCompleted_MapsStatus(ProcessStatus status, string expected)
    {
        var startedAt = new DateTime(2026, 6, 29, 9, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2026, 6, 29, 9, 5, 0, DateTimeKind.Utc);
        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };

        var payload = InstanceMonitoringPayloadMapper.ToInstanceCompleted(instance, status);

        Assert.Equal(instance.Id, payload.ProcessInstanceId);
        Assert.Equal(expected, payload.Status);
        Assert.Equal(completedAt, payload.CompletedAt);
        Assert.Equal(300_000, payload.DurationMs);
    }
}
