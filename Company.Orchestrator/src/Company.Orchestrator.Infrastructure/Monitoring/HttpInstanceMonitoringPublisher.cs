using System.Net.Http.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Monitoring;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.Monitoring;

/// <summary>
/// Relays monitoring events from the Worker process to the API internal publish endpoint.
/// </summary>
public sealed class HttpInstanceMonitoringPublisher : IInstanceMonitoringPublisher
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpInstanceMonitoringPublisher> _logger;
    private readonly string? _apiKey;

    public HttpInstanceMonitoringPublisher(
        HttpClient http,
        IConfiguration configuration,
        ILogger<HttpInstanceMonitoringPublisher> logger)
    {
        _http   = http;
        _logger = logger;
        _apiKey = configuration["InstanceMonitoring:InternalApiKey"];

        var baseUrl = configuration["InstanceMonitoring:ApiBaseUrl"]?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _http.BaseAddress = new Uri(baseUrl);
    }

    public Task PublishStepStartedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => PublishAsync(
            InstanceMonitoringEventNames.StepStarted,
            InstanceMonitoringPayloadMapper.ToStepStarted(step),
            cancellationToken);

    public Task PublishEnvelopeAsync(InstanceMonitoringEnvelope envelope, CancellationToken cancellationToken = default)
        => PublishAsync(envelope.EventName, envelope.Payload, cancellationToken);

    public Task PublishStepCompletedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => PublishAsync(
            InstanceMonitoringEventNames.StepCompleted,
            InstanceMonitoringPayloadMapper.ToStepCompleted(step),
            cancellationToken);

    public Task PublishStepFailedAsync(ProcessStepInstance step, CancellationToken cancellationToken = default)
        => PublishAsync(
            InstanceMonitoringEventNames.StepFailed,
            InstanceMonitoringPayloadMapper.ToStepFailed(step),
            cancellationToken);

    public Task PublishInstanceCompletedAsync(
        ProcessInstance instance,
        ProcessStatus status,
        CancellationToken cancellationToken = default)
        => PublishAsync(
            InstanceMonitoringEventNames.InstanceCompleted,
            InstanceMonitoringPayloadMapper.ToInstanceCompleted(instance, status),
            cancellationToken);

    private async Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
            return;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/internal/instance-monitoring/publish")
            {
                Content = JsonContent.Create(new InstanceMonitoringEnvelope(eventName, payload)),
            };

            if (!string.IsNullOrWhiteSpace(_apiKey))
                request.Headers.Add("X-Instance-Monitoring-Key", _apiKey);

            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Instance monitoring relay failed ({Status}) for event {Event}",
                    response.StatusCode, eventName);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Instance monitoring relay error for event {Event}", eventName);
        }
    }
}
