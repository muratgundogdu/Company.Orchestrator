using System.Text.Json;
using Company.Orchestrator.Application.DTOs.ProcessInstance;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Application.Triggers;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Worker.Workers;

/// <summary>
/// Background service that polls active FolderWatcher triggers and starts a ProcessInstance
/// for each new file detected in the watched folder.
///
/// DEDUPLICATION:
///   EventKey = "{absoluteFilePath}|{fileSizeBytes}|{lastWriteTimeUtc:O}"
///   A TriggerEvent is checked/inserted only AFTER the file is moved so that a failed move
///   leaves no record in the DB and the next poll can cleanly retry the same file.
///
/// FILE LIFECYCLE:
///   1. File detected in FolderPath
///   2. (optional) Moved to ProcessingFolder — BEFORE creating TriggerEvent
///      If the move fails we abort with no DB write, allowing the next cycle to retry.
///   3. TriggerEvent created (Pending → Processing)
///   4. ProcessInstance started → TriggerEvent = Completed / Failed
///   5. ProcessedFolder / ErrorFolder moves are handled by the workflow itself (folder.move-file)
/// </summary>
public sealed class FolderWatcherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FolderWatcherWorker> _logger;
    private readonly int _pollingIntervalMs;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public FolderWatcherWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<FolderWatcherWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
        var seconds      = configuration.GetValue<int>("FolderWatcher:PollingIntervalSeconds", 15);
        _pollingIntervalMs = Math.Max(5, seconds) * 1_000;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "FolderWatcherWorker started (polling every {Interval}s)",
            _pollingIntervalMs / 1_000);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAllTriggersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FolderWatcherWorker: unhandled exception in scan loop");
            }

            await Task.Delay(_pollingIntervalMs, stoppingToken);
        }

        _logger.LogInformation("FolderWatcherWorker stopped");
    }

    // ------------------------------------------------------------------ //
    // Main scan loop
    // ------------------------------------------------------------------ //

    private async Task ScanAllTriggersAsync(CancellationToken cancellationToken)
    {
        using var scope          = _serviceProvider.CreateScope();
        var triggerRepo          = scope.ServiceProvider.GetRequiredService<ITriggerRepository>();
        var eventRepo            = scope.ServiceProvider.GetRequiredService<ITriggerEventRepository>();
        var processInstanceSvc   = scope.ServiceProvider.GetRequiredService<IProcessInstanceService>();
        var dbContext            = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();

        var triggers = await triggerRepo.GetActiveFolderWatchersAsync(cancellationToken);

        if (triggers.Count == 0)
        {
            _logger.LogDebug("FolderWatcherWorker: no active FolderWatcher triggers found");
            return;
        }

        _logger.LogInformation(
            "FolderWatcherWorker: scanning {Count} active trigger(s)", triggers.Count);

        foreach (var trigger in triggers)
            await ProcessTriggerAsync(
                trigger, triggerRepo, eventRepo, processInstanceSvc, dbContext, cancellationToken);
    }

    private async Task ProcessTriggerAsync(
        Trigger trigger,
        ITriggerRepository triggerRepo,
        ITriggerEventRepository eventRepo,
        IProcessInstanceService processInstanceSvc,
        OrchestratorDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trigger.ConfigJson))
        {
            _logger.LogWarning(
                "FolderWatcherWorker: trigger '{Name}' ({Id}) has no ConfigJson — skipping",
                trigger.Name, trigger.Id);
            return;
        }

        FolderWatcherConfig config;
        try
        {
            config = JsonSerializer.Deserialize<FolderWatcherConfig>(trigger.ConfigJson, JsonOptions)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FolderWatcherWorker: trigger '{Name}' has invalid ConfigJson", trigger.Name);
            return;
        }

        if (!Directory.Exists(config.FolderPath))
        {
            _logger.LogWarning(
                "FolderWatcherWorker: trigger '{Name}' — folder not found: '{Path}'",
                trigger.Name, config.FolderPath);
            return;
        }

        _logger.LogInformation(
            "FolderWatcherWorker: scanning '{Folder}' (pattern='{Pattern}') for trigger '{Name}'",
            config.FolderPath, config.Pattern, trigger.Name);

        var files = Directory.GetFiles(config.FolderPath, config.Pattern, SearchOption.TopDirectoryOnly);

        _logger.LogInformation(
            "FolderWatcherWorker: found {Count} file(s) in '{Folder}'", files.Length, config.FolderPath);

        foreach (var filePath in files)
        {
            await ProcessFileAsync(
                trigger, config, filePath, eventRepo, processInstanceSvc, dbContext, cancellationToken);
        }

        // Update LastTriggeredAt for this trigger
        trigger.LastTriggeredAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ //
    // Per-file processing
    // ------------------------------------------------------------------ //

    private async Task ProcessFileAsync(
        Trigger trigger,
        FolderWatcherConfig config,
        string filePath,
        ITriggerEventRepository eventRepo,
        IProcessInstanceService processInstanceSvc,
        OrchestratorDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // ── 1. Read metadata ──────────────────────────────────────────────
        FileInfo info;
        try { info = new FileInfo(filePath); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "FolderWatcherWorker: cannot read file metadata for '{Path}'", filePath);
            return;
        }

        // ── 2. Deduplication check (all statuses) ─────────────────────────
        // EventKey is always based on the ORIGINAL source path so that the same
        // physical file produces the same key regardless of later moves.
        var eventKey = BuildEventKey(filePath, info);

        if (await eventRepo.EventKeyExistsAsync(trigger.Id, eventKey, cancellationToken))
        {
            _logger.LogDebug(
                "FolderWatcherWorker: duplicate skipped — '{File}' (key={Key})",
                info.Name, eventKey);
            return;
        }

        _logger.LogInformation(
            "FolderWatcherWorker: new file detected — '{File}' in trigger '{Trigger}'",
            info.Name, trigger.Name);

        // ── 3. Move to Processing folder BEFORE writing to DB ─────────────
        // If the move fails we abort without creating a TriggerEvent, which
        // lets the next polling cycle retry the same file cleanly.
        var originalFilePath = Path.GetFullPath(filePath);
        var activeFilePath     = originalFilePath;
        var movedToProcessing  = false;

        if (config.MoveToProcessingFolder && !string.IsNullOrWhiteSpace(config.ProcessingFolder))
        {
            try
            {
                Directory.CreateDirectory(config.ProcessingFolder);

                var destPath = Path.Combine(config.ProcessingFolder, info.Name);

                // Avoid silent overwrite of a same-named file stuck in Processing
                if (File.Exists(destPath))
                    destPath = Path.Combine(
                        config.ProcessingFolder,
                        $"{Path.GetFileNameWithoutExtension(info.Name)}_{Guid.NewGuid():N}{info.Extension}");

                File.Move(filePath, destPath);
                activeFilePath    = Path.GetFullPath(destPath);
                movedToProcessing = true;

                _logger.LogInformation(
                    "FolderWatcher moved file: original='{Original}', processing='{Processing}'",
                    originalFilePath, activeFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "FolderWatcherWorker: failed to move '{File}' to processing folder — will retry next cycle",
                    info.Name);

                // Do NOT create a TriggerEvent here.  The file stays in the input folder
                // and the next poll will find it with the same EventKey and try again.
                return;
            }
        }

        // ── 4. Create TriggerEvent ────────────────────────────────────────
        var triggerEvent = new TriggerEvent
        {
            TriggerId = trigger.Id,
            EventKey  = eventKey,           // original-path key for deduplication
            FilePath  = activeFilePath,     // current (post-move) path stored for audit
            FileName  = info.Name,
            Status    = TriggerEventStatus.Pending
        };

        await eventRepo.AddAsync(triggerEvent, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            // Race condition: another worker beat us to it in the same cycle.
            _logger.LogWarning(
                "FolderWatcherWorker: duplicate skipped (race condition) — '{File}'", info.Name);
            return;
        }

        _logger.LogInformation(
            "FolderWatcherWorker: TriggerEvent {EventId} created for '{File}'",
            triggerEvent.Id, info.Name);

        // ── 5. Start ProcessInstance ──────────────────────────────────────
        // Primary file path variables always reference the active (post-move) location.
        var inputData = BuildInputData(trigger, originalFilePath, activeFilePath, info);

        _logger.LogInformation(
            "Workflow input variables: triggerFilePath='{TriggerFilePath}', triggerOriginalFilePath='{OriginalPath}', triggerFileName='{FileName}', triggerDirectory='{Directory}', triggerProcessingPath='{ProcessingPath}', movedToProcessing={Moved}",
            activeFilePath,
            originalFilePath,
            info.Name,
            Path.GetDirectoryName(originalFilePath) ?? string.Empty,
            activeFilePath,
            movedToProcessing);

        try
        {
            triggerEvent.Status = TriggerEventStatus.Processing;
            await dbContext.SaveChangesAsync(cancellationToken);

            var instance = await processInstanceSvc.StartAsync(new StartProcessRequest
            {
                ProcessDefinitionId = config.ProcessDefinitionId,
                InputData           = inputData,
                TriggeredBy         = $"FolderWatcher:{trigger.Name}",
                CorrelationId       = $"fw-{trigger.Id:N}-{triggerEvent.Id:N}"
            }, cancellationToken);

            triggerEvent.ProcessInstanceId = instance.Id;
            triggerEvent.Status            = TriggerEventStatus.Completed;
            triggerEvent.CompletedAt       = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "FolderWatcherWorker: ProcessInstance {InstanceId} started for '{File}' — TriggerEvent {EventId} Completed",
                instance.Id, info.Name, triggerEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FolderWatcherWorker: failed to start ProcessInstance for '{File}'", info.Name);

            triggerEvent.Status       = TriggerEventStatus.Failed;
            triggerEvent.ErrorMessage = ex.Message;
            triggerEvent.CompletedAt  = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private static string BuildEventKey(string filePath, FileInfo info) =>
        $"{Path.GetFullPath(filePath)}|{info.Length}|{info.LastWriteTimeUtc:O}";

    private static string BuildInputData(
        Trigger trigger,
        string originalFilePath,
        string activeFilePath,
        FileInfo info)
    {
        var originalDirectory = Path.GetDirectoryName(originalFilePath) ?? string.Empty;

        var triggerData = new Dictionary<string, string>
        {
            ["triggerFilePath"]         = activeFilePath,
            ["triggerProcessingPath"]   = activeFilePath,
            ["triggerOriginalFilePath"] = originalFilePath,
            ["triggerFileName"]         = info.Name,
            ["triggerDirectory"]        = originalDirectory,
            ["triggerFolder"]           = originalDirectory,
            ["triggerType"]             = "FolderWatcher",
            ["triggerName"]             = trigger.Name,
            ["triggerId"]               = trigger.Id.ToString("N"),
        };

        var json = JsonSerializer.Serialize(triggerData);

        if (string.IsNullOrWhiteSpace(trigger.DefaultInputData))
            return json;

        return MergeInputData(trigger.DefaultInputData, json);
    }

    /// <summary>
    /// Merges trigger defaults with runtime trigger data. Runtime values win on key conflicts
    /// so FolderWatcher file paths always reflect the post-move location.
    /// </summary>
    private static string MergeInputData(string defaultInputData, string triggerInputData)
    {
        try
        {
            var defaults = JsonSerializer.Deserialize<Dictionary<string, object>>(defaultInputData)
                           ?? new Dictionary<string, object>();
            var runtime = JsonSerializer.Deserialize<Dictionary<string, object>>(triggerInputData)
                          ?? new Dictionary<string, object>();

            foreach (var kv in runtime)
                defaults[kv.Key] = kv.Value;

            return JsonSerializer.Serialize(defaults);
        }
        catch
        {
            return triggerInputData;
        }
    }

    /// <summary>
    /// Returns true when the DbUpdateException is caused by a unique-index violation
    /// (SQL Server error 2601 = duplicate key row; 2627 = unique constraint violation).
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx)
            return sqlEx.Number is 2601 or 2627;
        return false;
    }
}
