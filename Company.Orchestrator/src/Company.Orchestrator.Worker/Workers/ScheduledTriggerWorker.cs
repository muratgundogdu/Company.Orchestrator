using System.Text.Json;
using Cronos;
using Company.Orchestrator.Application.DTOs.ProcessInstance;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Application.Triggers;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Worker.Workers;

/// <summary>
/// Background service that fires active Scheduled triggers whose NextScheduledAt has elapsed.
///
/// TIME ZONE POLICY — LOCAL TIME FROM THE USER'S PERSPECTIVE:
///   Cron expressions represent local server time (e.g. "30 8 * * *" = 08:30 Turkey/local).
///   Cronos requires a UTC base instant; we pass TimeZoneInfo.Local so cron fields stay local.
///   NextScheduledAt and LastTriggeredAt are stored as local wall-clock (DateTimeKind.Unspecified)
///   and compared against DateTime.Now so due-checks stay consistent with the DB convention.
///
/// Poll cycle:
///   1. Load all active Scheduled triggers.
///   2. If NextScheduledAt is null, compute it from CronExpression (local) and persist.
///   3. If NextScheduledAt <= DateTime.Now (local), fire the trigger:
///      a. Create TriggerEvent (Pending → Processing).
///      b. Call ProcessInstanceService.StartAsync.
///      c. Mark TriggerEvent Completed/Failed.
///      d. Advance NextScheduledAt to the next local occurrence.
/// </summary>
public sealed class ScheduledTriggerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledTriggerWorker> _logger;
    private const int PollingIntervalSeconds = 30;

    public ScheduledTriggerWorker(
        IServiceProvider serviceProvider,
        ILogger<ScheduledTriggerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tz = TimeZoneInfo.Local;
        _logger.LogInformation(
            "ScheduledTriggerWorker started (polling every {Interval}s) | " +
            "LOCAL TIME SCHEDULING | TimeZone='{Tz}' UtcOffset={Offset}",
            PollingIntervalSeconds,
            tz.DisplayName,
            tz.GetUtcOffset(DateTime.Now));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await ProcessDueTriggers(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ScheduledTriggerWorker: unhandled error in poll cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollingIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessDueTriggers(IServiceProvider services, CancellationToken ct)
    {
        // ── All scheduling logic uses local time ──────────────────────────────
        var now = DateTime.Now;   // DateTimeKind.Local — matches DB reads (Unspecified, same raw ticks)

        _logger.LogInformation(
            "ScheduledTriggerWorker using local time scheduling | LocalNow={Now:yyyy-MM-dd HH:mm:ss}",
            now);

        var repo  = services.GetRequiredService<ITriggerRepository>();
        var dbCtx = services.GetRequiredService<OrchestratorDbContext>();

        var triggers = await repo.GetActiveScheduledTriggersAsync(ct);

        _logger.LogInformation(
            "ScheduledTriggerWorker: found {Count} active scheduled trigger(s)", triggers.Count);

        foreach (var trigger in triggers)
        {
            _logger.LogInformation(
                "ScheduledTriggerWorker: checking trigger '{Name}' ({Id}) | " +
                "Cron='{Cron}' | NextScheduledAt={Next:yyyy-MM-dd HH:mm:ss} | LocalNow={Now:yyyy-MM-dd HH:mm:ss}",
                trigger.Name, trigger.Id,
                trigger.CronExpression,
                trigger.NextScheduledAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(null)",
                now);

            try
            {
                // ── First encounter: compute and store NextScheduledAt ─────────
                if (trigger.NextScheduledAt is null)
                {
                    var next = ComputeNextOccurrence(trigger.CronExpression!, now);
                    _logger.LogInformation(
                        "ScheduledTriggerWorker: trigger '{Name}' — NextScheduledAt was null, " +
                        "computed next local occurrence = {Next:yyyy-MM-dd HH:mm:ss} (will fire next cycle)",
                        trigger.Name,
                        next?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(cron parse failed — check expression)");

                    trigger.NextScheduledAt = next;
                    await dbCtx.SaveChangesAsync(ct);
                    continue;
                }

                // ── Due check: both values are local/Unspecified, ticks match ──
                var dueAt = trigger.NextScheduledAt.Value;
                var isDue = dueAt <= now;

                _logger.LogInformation(
                    "ScheduledTriggerWorker: due-check '{Name}' | " +
                    "NextScheduledAt={Due:yyyy-MM-dd HH:mm:ss} <= LocalNow={Now:yyyy-MM-dd HH:mm:ss} → {IsDue}",
                    trigger.Name,
                    dueAt, now, isDue);

                if (!isDue)
                {
                    _logger.LogInformation(
                        "ScheduledTriggerWorker: trigger '{Name}' not yet due — " +
                        "{Minutes:F1} minute(s) remaining",
                        trigger.Name,
                        (dueAt - now).TotalMinutes);
                    continue;
                }

                _logger.LogInformation(
                    "ScheduledTriggerWorker: trigger '{Name}' IS DUE — entering execution block",
                    trigger.Name);

                await FireTrigger(services, dbCtx, trigger, now, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "ScheduledTriggerWorker: error processing trigger '{Name}' ({Id})",
                    trigger.Name, trigger.Id);
            }
        }
    }

    private async Task FireTrigger(
        IServiceProvider services,
        OrchestratorDbContext dbCtx,
        Domain.Entities.Trigger trigger,
        DateTime localNow,
        CancellationToken ct)
    {
        var processInstanceSvc = services.GetRequiredService<IProcessInstanceService>();

        _logger.LogInformation(
            "ScheduledTriggerWorker: firing trigger '{Name}' ({Id}) | " +
            "WasScheduledFor={ScheduledAt:yyyy-MM-dd HH:mm:ss} | FiredAt={Now:yyyy-MM-dd HH:mm:ss}",
            trigger.Name, trigger.Id, trigger.NextScheduledAt, localNow);

        // ── Create TriggerEvent ───────────────────────────────────────────────
        var triggerEvent = new TriggerEvent
        {
            TriggerId = trigger.Id,
            EventKey  = $"scheduled:{trigger.Id:N}:{localNow:O}",
            Status    = TriggerEventStatus.Processing,
        };

        dbCtx.TriggerEvents.Add(triggerEvent);

        // Advance schedule before saving (local time)
        trigger.LastTriggeredAt = localNow;
        trigger.NextScheduledAt = ComputeNextOccurrence(trigger.CronExpression!, localNow);

        _logger.LogInformation(
            "ScheduledTriggerWorker: trigger '{Name}' — next occurrence after firing = {Next:yyyy-MM-dd HH:mm:ss}",
            trigger.Name,
            trigger.NextScheduledAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(none)");

        await dbCtx.SaveChangesAsync(ct);

        // ── Start ProcessInstance ─────────────────────────────────────────────
        try
        {
            var inputData = BuildInputData(trigger, localNow);

            var instance = await processInstanceSvc.StartAsync(new StartProcessRequest
            {
                ProcessDefinitionId = trigger.ProcessDefinitionId,
                InputData           = string.IsNullOrWhiteSpace(trigger.DefaultInputData)
                                        ? inputData
                                        : MergeInputData(trigger.DefaultInputData, inputData),
                TriggeredBy         = $"Scheduled:{trigger.Name}",
                CorrelationId       = $"sched-{trigger.Id:N}-{triggerEvent.Id:N}"
            }, ct);

            triggerEvent.ProcessInstanceId = instance.Id;
            triggerEvent.Status            = TriggerEventStatus.Completed;
            triggerEvent.CompletedAt       = DateTime.Now;
            await dbCtx.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ScheduledTriggerWorker: ProcessInstance {InstanceId} started — TriggerEvent {EventId} Completed",
                instance.Id, triggerEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ScheduledTriggerWorker: failed to start ProcessInstance for trigger '{Name}'",
                trigger.Name);

            triggerEvent.Status       = TriggerEventStatus.Failed;
            triggerEvent.ErrorMessage = ex.Message;
            triggerEvent.CompletedAt  = DateTime.Now;
            await dbCtx.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Computes the next occurrence of <paramref name="cronExpression"/> after <paramref name="after"/>.
    /// Cron fields are interpreted in <see cref="TimeZoneInfo.Local"/>; the returned value is local
    /// wall-clock time suitable for storing in NextScheduledAt.
    /// </summary>
    private DateTime? ComputeNextOccurrence(string cronExpression, DateTime after)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, CronFormat.Standard);

            // Cronos TimeZoneInfo overload requires fromUtc with Kind = Utc.
            var afterLocal = after.Kind switch
            {
                DateTimeKind.Utc     => TimeZoneInfo.ConvertTimeFromUtc(after, TimeZoneInfo.Local),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(after, DateTimeKind.Local),
                _                    => after,
            };
            var baseUtc = afterLocal.ToUniversalTime();

            var computedUtc = cron.GetNextOccurrence(baseUtc, TimeZoneInfo.Local);
            if (computedUtc is null)
            {
                _logger.LogInformation(
                    "ScheduledTriggerWorker: cron '{Cron}' | baseUtc={BaseUtc:yyyy-MM-dd HH:mm:ss} | " +
                    "computedUtc=(none) | computedLocal=(none) | stored NextScheduledAt=(none)",
                    cronExpression, baseUtc);
                return null;
            }

            var utcInstant = computedUtc.Value.Kind == DateTimeKind.Utc
                ? computedUtc.Value
                : DateTime.SpecifyKind(computedUtc.Value, DateTimeKind.Utc);

            var computedLocal = TimeZoneInfo.ConvertTimeFromUtc(utcInstant, TimeZoneInfo.Local);
            var stored        = DateTime.SpecifyKind(computedLocal, DateTimeKind.Unspecified);

            _logger.LogInformation(
                "ScheduledTriggerWorker: cron '{Cron}' | baseUtc={BaseUtc:yyyy-MM-dd HH:mm:ss} | " +
                "computedUtc={ComputedUtc:yyyy-MM-dd HH:mm:ss} | computedLocal={ComputedLocal:yyyy-MM-dd HH:mm:ss} | " +
                "stored NextScheduledAt={Stored:yyyy-MM-dd HH:mm:ss}",
                cronExpression, baseUtc, utcInstant, computedLocal, stored);

            return stored;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ScheduledTriggerWorker: failed to parse cron expression '{Cron}'", cronExpression);
            return null;
        }
    }

    private static string BuildInputData(Domain.Entities.Trigger trigger, DateTime firedAt)
    {
        var data = new Dictionary<string, string>
        {
            ["triggerType"]   = "Scheduled",
            ["triggerName"]   = trigger.Name,
            ["triggerId"]     = trigger.Id.ToString("N"),
            ["firedAtLocal"]  = firedAt.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        return JsonSerializer.Serialize(data);
    }

    private static string MergeInputData(string defaultInputData, string scheduledInputData)
    {
        try
        {
            var defaults  = JsonSerializer.Deserialize<Dictionary<string, object>>(defaultInputData)
                            ?? new Dictionary<string, object>();
            var scheduled = JsonSerializer.Deserialize<Dictionary<string, object>>(scheduledInputData)
                            ?? new Dictionary<string, object>();

            foreach (var kv in scheduled)
                defaults[kv.Key] = kv.Value;

            return JsonSerializer.Serialize(defaults);
        }
        catch
        {
            return defaultInputData;
        }
    }
}
