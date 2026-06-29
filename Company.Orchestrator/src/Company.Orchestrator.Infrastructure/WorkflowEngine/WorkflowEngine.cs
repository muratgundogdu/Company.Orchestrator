using System.Text;
using System.Text.Json;
using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Audit;
using Company.Orchestrator.Application.Capabilities;
using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Common;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Company.Orchestrator.Domain.Enums;
using Company.Orchestrator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.WorkflowEngine;

/// <summary>
/// Core orchestration loop.
/// The engine knows only: ICapabilityRegistry, IStepHandler, IArtifactRepository.
/// It has NO knowledge of File, Mail, Browser, Excel, or any other capability.
/// Adding a new capability requires zero changes here.
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly OrchestratorDbContext _context;
    private readonly IEnumerable<IStepHandler> _stepHandlers;
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IArtifactStore _artifactStore;
    private readonly IAuditService _audit;
    private readonly IInstanceMonitoringPublisher _monitoring;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        OrchestratorDbContext context,
        IEnumerable<IStepHandler> stepHandlers,
        ICapabilityRegistry capabilityRegistry,
        IArtifactRepository artifactRepository,
        IArtifactStore artifactStore,
        IAuditService audit,
        IInstanceMonitoringPublisher monitoring,
        ILogger<WorkflowEngine> logger)
    {
        _context = context;
        _stepHandlers = stepHandlers;
        _capabilityRegistry = capabilityRegistry;
        _artifactRepository = artifactRepository;
        _artifactStore = artifactStore;
        _audit = audit;
        _monitoring = monitoring;
        _logger = logger;
    }

    public async Task ExecuteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs
            .Include(j => j.ProcessInstance)
                .ThenInclude(pi => pi!.ProcessVersion)
            .Include(j => j.ProcessInstance)
                .ThenInclude(pi => pi!.ProcessDefinition)
            .Include(j => j.ProcessInstance)
                .ThenInclude(pi => pi!.StepInstances)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            _logger.LogWarning("Job {JobId} not found", jobId);
            return;
        }

        var instance = job.ProcessInstance
            ?? throw new InvalidOperationException($"Job {jobId} has no process instance.");

        if (job.Status == JobStatus.Cancelled)
        {
            _logger.LogInformation("Job {JobId} is already cancelled — skipping execution", jobId);
            return;
        }

        if (job.Status == JobStatus.Cancelling)
        {
            await CancelJobAsync(
                job,
                instance,
                stepInstance: null,
                "Workflow cancelled before execution started",
                cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Starting workflow execution for Job {JobId}, ProcessInstance {InstanceId} [capabilities: {Caps}]",
            jobId, instance.Id, string.Join(", ", _capabilityRegistry.RegisteredCapabilities));

        WorkflowDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinition>(
                instance.ProcessVersion.JsonDefinition,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Null workflow definition");
        }
        catch (Exception ex)
        {
            await FailJobAsync(job, instance, $"Failed to parse workflow definition: {ex.Message}", cancellationToken);
            return;
        }

        instance.Status = ProcessStatus.Running;
        instance.StartedAt ??= DateTime.UtcNow;
        job.AttemptCount++;
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.WriteSuccessAsync(new AuditWriteRequest
        {
            EventType  = AuditEventTypes.JobStarted,
            Category   = AuditCategories.Job,
            EntityType = "Job",
            EntityId   = job.Id.ToString(),
            EntityName = instance.ProcessDefinition?.Name,
            Action     = "Job started",
            Details    = new { instanceId = instance.Id, attempt = job.AttemptCount },
        }, cancellationToken);

        // Build shared WorkflowContext — one per job execution
        var workflowContext = new WorkflowContext(
            processInstance: instance,
            jobId: jobId,
            registry: _capabilityRegistry,
            initialVariables: BuildInitialVariables(instance, definition));

        var stepMap = definition.Steps.ToDictionary(s => s.Id);
        var currentStepId = definition.Steps.FirstOrDefault()?.Id;

        while (currentStepId is not null && !cancellationToken.IsCancellationRequested)
        {
            if (await IsJobCancellationRequestedAsync(job.Id, cancellationToken))
            {
                if (stepMap.TryGetValue(currentStepId, out var pendingStep))
                {
                    var pendingInstance = await _context.ProcessStepInstances
                        .FirstOrDefaultAsync(
                            s => s.ProcessInstanceId == instance.Id && s.StepId == pendingStep.Id,
                            cancellationToken);
                    await CancelJobAsync(
                        job, instance, pendingInstance,
                        $"Workflow cancelled before step '{pendingStep.Name}'",
                        cancellationToken);
                }
                else
                {
                    await CancelJobAsync(
                        job, instance, null,
                        "Workflow cancelled",
                        cancellationToken);
                }

                return;
            }

            if (!stepMap.TryGetValue(currentStepId, out var stepDef))
            {
                _logger.LogWarning("Step {StepId} not found in definition", currentStepId);
                break;
            }

            var stepInstance = await CreateOrGetStepInstanceAsync(instance.Id, stepDef, cancellationToken);

            // foreach.loop / foreach.row / foreach.file must re-run on every iteration — never skip based on prior Success.
            var isForeachLoop = string.Equals(stepDef.Type, "foreach.loop", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(stepDef.Type, "foreach.row", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(stepDef.Type, "foreach.file", StringComparison.OrdinalIgnoreCase);
            if (!isForeachLoop && stepInstance.Status == StepStatus.Success)
            {
                RehydrateStepOutputVariables(workflowContext, stepInstance, stepDef);
                currentStepId = stepDef.NextStepId;
                continue;
            }

            var stepResult = await ExecuteStepAsync(
                job, workflowContext, stepInstance, stepDef, cancellationToken);

            if (await IsJobCancellationRequestedAsync(job.Id, cancellationToken))
            {
                await CancelJobAsync(
                    job, instance, stepInstance,
                    $"Workflow cancelled after step '{stepDef.Name}'",
                    cancellationToken);
                return;
            }

            if (!stepResult.Success)
            {
                if (await IsJobCancellationRequestedAsync(job.Id, cancellationToken))
                {
                    await CancelJobAsync(
                        job, instance, stepInstance,
                        $"Workflow cancelled during step '{stepDef.Name}'",
                        cancellationToken);
                    return;
                }

                if (stepResult.OutputVariables is not null)
                    workflowContext.MergeVariables(stepResult.OutputVariables);

                // ── Failure routing ─────────────────────────────────────────────────
                // OnFailureStepId (designer) takes precedence over legacy OnErrorStepId.
                var failureStepId = stepDef.OnFailureStepId ?? stepDef.OnErrorStepId;
                var errorMsg      = stepResult.ErrorMessage ?? "Step failed";

                if (!string.IsNullOrEmpty(failureStepId) && stepMap.ContainsKey(failureStepId))
                {
                    _logger.LogWarning(
                        "Step {StepId} ({StepName}) failed — routing to failure handler {FailureStepId}. Error: {Error}",
                        stepDef.Id, stepDef.Name, failureStepId, errorMsg);

                    // Create failure report before routing so mail.send can attach it.
                    var reportName = await CreateFailureReportAsync(
                        job, instance, stepDef, errorMsg, workflowContext, cancellationToken);

                    // Inject error details + report artifact name into context.
                    workflowContext.MergeVariables(new Dictionary<string, object>
                    {
                        ["errorMessage"]              = errorMsg,
                        ["failedStepId"]              = stepDef.Id,
                        ["failedStepName"]            = stepDef.Name,
                        ["failedStepType"]            = stepDef.Type,
                        ["failureReportArtifactName"] = reportName,
                    });

                    currentStepId = failureStepId;
                    continue;
                }

                // No failure handler — honour ContinueOnError or abort the job.
                if (!stepDef.ContinueOnError)
                {
                    // Create failure report even without a handler so it is available for download.
                    await CreateFailureReportAsync(
                        job, instance, stepDef, errorMsg, workflowContext, cancellationToken);

                    await FailJobAsync(job, instance, errorMsg, cancellationToken);
                    return;
                }
                _logger.LogWarning("Step {StepId} failed but ContinueOnError=true, continuing", stepDef.Id);
            }
            else
            {
                // Merge output variables into context
                workflowContext.MergeVariables(stepResult.OutputVariables);

                if (stepResult.OutputVariables is { Count: > 0 })
                {
                    _logger.LogInformation(
                        "Step '{StepName}' output variables merged: [{Keys}]",
                        stepDef.Name,
                        string.Join(", ", stepResult.OutputVariables.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)));
                }

                // Register produced artifacts in context + persist metadata
                if (stepResult.ProducedArtifacts?.Count > 0)
                {
                    workflowContext.RegisterArtifacts(stepResult.ProducedArtifacts);
                    var entities = stepResult.ProducedArtifacts
                        .Select(a => a.ToEntity(instance.Id, stepInstance.Id))
                        .ToList();
                    await _artifactRepository.AddRangeAsync(entities, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // Handler routing override (condition.if, foreach.loop, …)
                if (stepResult.OutputVariables is not null
                    && stepResult.OutputVariables.TryGetValue("nextStepId", out var overrideNext)
                    && overrideNext is string overrideNextStr
                    && !string.IsNullOrEmpty(overrideNextStr))
                {
                    // foreach.loop starting another iteration — reset body steps so they re-execute.
                    if (isForeachLoop
                        && stepResult.OutputVariables.TryGetValue("foreachCompleted", out var fcObj)
                        && fcObj is bool foreachCompleted
                        && !foreachCompleted)
                    {
                        await ResetLoopBodyStepInstancesAsync(
                            instance.Id, stepDef, stepMap, cancellationToken);
                    }

                    currentStepId = overrideNextStr;
                    continue;
                }
            }

            currentStepId = stepDef.NextStepId;
        }

        if (await IsJobCancellationRequestedAsync(job.Id, cancellationToken))
        {
            await CancelJobAsync(job, instance, null, "Workflow cancelled", cancellationToken);
            return;
        }

        await CompleteJobAsync(job, instance, cancellationToken);
    }

    private async Task<bool> IsJobCancellationRequestedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var status = await _context.Jobs
            .AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => j.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status == JobStatus.Cancelling;
    }

    private async Task CancelJobAsync(
        Job job,
        ProcessInstance instance,
        ProcessStepInstance? stepInstance,
        string message,
        CancellationToken cancellationToken)
    {
        var browserCleanedUp = await CleanupBrowserSessionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        job.Status       = JobStatus.Cancelled;
        job.CancelledAtUtc ??= now;
        job.CompletedAt  = now;
        job.LockedAt     = null;
        job.ErrorMessage = null;

        instance.Status       = ProcessStatus.Cancelled;
        instance.CompletedAt  = now;
        instance.ErrorMessage = null;

        if (stepInstance is not null && stepInstance.Status == StepStatus.Running)
        {
            stepInstance.Status      = StepStatus.Skipped;
            stepInstance.CompletedAt = now;
            stepInstance.ErrorMessage = null;
        }

        await AddJobLogAsync(job.Id, null, "Information",
            "Cancellation detected by worker", cancellationToken);
        await AddJobLogAsync(job.Id, stepInstance?.Id, "Information", message, cancellationToken);

        if (browserCleanedUp)
        {
            await AddJobLogAsync(job.Id, stepInstance?.Id, "Information",
                "Browser session cleaned up", cancellationToken);
        }

        await AddJobLogAsync(job.Id, null, "Information", "Workflow cancelled", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PublishInstanceCompletedAsync(instance, ProcessStatus.Cancelled, cancellationToken);

        _logger.LogInformation("Job {JobId} cancelled cooperatively: {Message}", job.Id, message);
    }

    private async Task<bool> CleanupBrowserSessionAsync(CancellationToken cancellationToken)
    {
        if (!_capabilityRegistry.IsRegistered<IBrowserCapability>())
            return false;

        try
        {
            var browser = _capabilityRegistry.Resolve<IBrowserCapability>();
            await browser.CloseAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Browser cleanup during job cancellation failed");
            return false;
        }
    }

    private async Task<StepResult> ExecuteStepAsync(
        Job job,
        WorkflowContext context,
        ProcessStepInstance stepInstance,
        StepDefinition stepDef,
        CancellationToken cancellationToken)
    {
        var handler = _stepHandlers.FirstOrDefault(h =>
            string.Equals(h.HandlerType, stepDef.Type, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
        {
            var msg = $"No handler registered for step type '{stepDef.Type}'";
            _logger.LogError(msg);
            await UpdateStepAsync(stepInstance, StepStatus.Failed, null, msg, cancellationToken);
            await AddJobLogAsync(job.Id, stepInstance.Id, "Error", msg, cancellationToken);
            return StepResult.Fail(msg);
        }

        // Expose current step definition to the handler
        context.StepDefinition = stepDef;

        stepInstance.Status = StepStatus.Running;
        stepInstance.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await PublishStepStartedAsync(stepInstance, cancellationToken);

        await AddJobLogAsync(job.Id, stepInstance.Id, "Information",
            $"Starting step '{stepDef.Name}' ({stepDef.Type})", cancellationToken);

        if (await IsJobCancellationRequestedAsync(job.Id, cancellationToken))
        {
            return StepResult.Fail("Job cancellation requested");
        }

        StepResult result;
        var attempt    = 0;
        var maxAttempts = stepDef.EffectiveMaxAttempts;
        var delaySec    = stepDef.EffectiveDelaySeconds;
        var startedAt  = DateTime.UtcNow;

        if (maxAttempts > 1)
            _logger.LogInformation(
                "Step '{StepName}' ({StepId}): retry policy active — maxAttempts={Max}, delaySeconds={Delay}",
                stepDef.Name, stepDef.Id, maxAttempts, delaySec);

        do
        {
            attempt++;
            try
            {
                result = await handler.ExecuteAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in step handler {HandlerType}", handler.HandlerType);
                result = StepResult.Fail(ex.Message);
            }

            if (!result.Success && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Step '{StepName}' ({StepId}) failed on attempt {Attempt}/{Max} — retrying in {Delay}s. Error: {Error}",
                    stepDef.Name, stepDef.Id, attempt, maxAttempts, delaySec, result.ErrorMessage);

                await AddJobLogAsync(job.Id, stepInstance.Id, "Warning",
                    $"Attempt {attempt}/{maxAttempts} failed: {result.ErrorMessage}. " +
                    $"Retrying in {delaySec}s…", cancellationToken);

                if (delaySec > 0)
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), cancellationToken);
            }
        } while (!result.Success && attempt < maxAttempts);

        if (result.Success && attempt > 1)
        {
            _logger.LogInformation(
                "Step '{StepName}' ({StepId}) retry succeeded on attempt {Attempt}/{Max}",
                stepDef.Name, stepDef.Id, attempt, maxAttempts);
            await AddJobLogAsync(job.Id, stepInstance.Id, "Information",
                $"Step retry succeeded on attempt {attempt}/{maxAttempts}", cancellationToken);
        }

        if (!result.Success && attempt >= maxAttempts && maxAttempts > 1)
        {
            _logger.LogWarning(
                "Step '{StepName}' ({StepId}) exhausted all {Max} attempt(s). Final error: {Error}",
                stepDef.Name, stepDef.Id, maxAttempts, result.ErrorMessage);
            await AddJobLogAsync(job.Id, stepInstance.Id, "Warning",
                $"Step exhausted all {maxAttempts} attempt(s). Final error: {result.ErrorMessage}",
                cancellationToken);
        }

        var completedAt = DateTime.UtcNow;
        var durationMs  = (long)(completedAt - (stepInstance.StartedAt ?? completedAt)).TotalMilliseconds;

        await UpdateStepAsync(
            stepInstance,
            result.Success ? StepStatus.Success : StepStatus.Failed,
            result.OutputData,
            result.ErrorMessage,
            cancellationToken,
            completedAt,
            durationMs,
            attempt);

        if (result.Success && result.OutputVariables is { Count: > 0 })
            PersistStepOutputVariables(stepInstance, result.OutputVariables);

        // Record step output in context for downstream steps
        var stepOutput = new StepOutput
        {
            StepId           = stepDef.Id,
            StepName         = stepDef.Name,
            Success          = result.Success,
            ErrorMessage     = result.ErrorMessage,
            Variables        = result.OutputVariables ?? new Dictionary<string, object>(),
            ProducedArtifacts = result.ProducedArtifacts ?? new List<Application.Artifacts.ArtifactReference>(),
            StartedAt        = startedAt,
            CompletedAt      = completedAt,
            DurationMs       = durationMs
        };
        context.RegisterStepOutput(stepOutput);

        await AddJobLogAsync(job.Id, stepInstance.Id,
            result.Success ? "Information" : "Error",
            result.Success
                ? $"Step '{stepDef.Name}' completed in {durationMs}ms"
                : $"Step '{stepDef.Name}' failed: {result.ErrorMessage}",
            cancellationToken);

        return result;
    }

    private async Task<ProcessStepInstance> CreateOrGetStepInstanceAsync(
        Guid instanceId, StepDefinition stepDef, CancellationToken cancellationToken)
    {
        var existing = await _context.ProcessStepInstances
            .FirstOrDefaultAsync(s => s.ProcessInstanceId == instanceId && s.StepId == stepDef.Id, cancellationToken);

        if (existing is not null) return existing;

        var stepInstance = new ProcessStepInstance
        {
            ProcessInstanceId = instanceId,
            StepId   = stepDef.Id,
            StepName = stepDef.Name,
            StepType = stepDef.Type,
            Status   = StepStatus.Pending
        };
        _context.ProcessStepInstances.Add(stepInstance);
        await _context.SaveChangesAsync(cancellationToken);
        return stepInstance;
    }

    private async Task UpdateStepAsync(
        ProcessStepInstance step,
        StepStatus status,
        string? outputData,
        string? errorMessage,
        CancellationToken cancellationToken,
        DateTime? completedAt = null,
        long? durationMs = null,
        int? attemptNumber = null)
    {
        step.Status        = status;
        step.OutputData    = outputData;
        step.ErrorMessage  = errorMessage;
        step.CompletedAt   = completedAt ?? DateTime.UtcNow;
        step.DurationMs    = durationMs;
        if (attemptNumber.HasValue) step.AttemptNumber = attemptNumber.Value;
        await _context.SaveChangesAsync(cancellationToken);
        await PublishStepStatusAsync(step, cancellationToken);
    }

    private async Task PublishStepStartedAsync(ProcessStepInstance step, CancellationToken cancellationToken)
    {
        try
        {
            await _monitoring.PublishStepStartedAsync(step, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish step started event for {StepId}", step.Id);
        }
    }

    private async Task PublishStepStatusAsync(ProcessStepInstance step, CancellationToken cancellationToken)
    {
        try
        {
            switch (step.Status)
            {
                case StepStatus.Success:
                    await _monitoring.PublishStepCompletedAsync(step, cancellationToken);
                    break;
                case StepStatus.Failed:
                    await _monitoring.PublishStepFailedAsync(step, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish step status event for {StepId}", step.Id);
        }
    }

    private async Task PublishInstanceCompletedAsync(
        ProcessInstance instance,
        ProcessStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            await _monitoring.PublishInstanceCompletedAsync(instance, status, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish instance completed event for {InstanceId}", instance.Id);
        }
    }

    private async Task AddJobLogAsync(
        Guid jobId, Guid? stepInstanceId, string level, string message, CancellationToken cancellationToken,
        string? details = null, string? exception = null)
    {
        _context.JobLogs.Add(new JobLog
        {
            JobId = jobId,
            StepInstanceId = stepInstanceId,
            Level = level,
            Message = message,
            Details = details,
            Exception = exception
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task CompleteJobAsync(Job job, ProcessInstance instance, CancellationToken cancellationToken)
    {
        if (await IsJobCancellationRequestedAsync(job.Id, cancellationToken))
        {
            await CancelJobAsync(job, instance, null, "Workflow cancelled", cancellationToken);
            return;
        }

        job.Status      = JobStatus.Success;
        job.CompletedAt = DateTime.UtcNow;
        job.LockedAt    = null;

        instance.Status      = ProcessStatus.Success;
        instance.CompletedAt = DateTime.UtcNow;

        await AddJobLogAsync(job.Id, null, "Information", "Workflow completed successfully", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PublishInstanceCompletedAsync(instance, ProcessStatus.Success, cancellationToken);
        _logger.LogInformation("Job {JobId} completed successfully", job.Id);

        await _audit.WriteSuccessAsync(new AuditWriteRequest
        {
            EventType  = AuditEventTypes.JobCompleted,
            Category   = AuditCategories.Job,
            EntityType = "Job",
            EntityId   = job.Id.ToString(),
            EntityName = instance.ProcessDefinition?.Name,
            Action     = "Job completed",
            Details    = new { instanceId = instance.Id },
        }, cancellationToken);
    }

    private async Task FailJobAsync(Job job, ProcessInstance instance, string error, CancellationToken cancellationToken)
    {
        if (await IsJobCancellationRequestedAsync(job.Id, cancellationToken))
        {
            await CancelJobAsync(job, instance, null, "Workflow cancelled", cancellationToken);
            return;
        }

        var shouldRetry = job.AttemptCount < job.MaxAttempts;

        if (shouldRetry)
        {
            job.Status      = JobStatus.Retrying;
            job.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, job.AttemptCount) * 10);
            job.LockedAt    = null;
            job.ErrorMessage = error;
            _logger.LogWarning("Job {JobId} failed, scheduling retry #{Attempt}", job.Id, job.AttemptCount + 1);
        }
        else
        {
            job.Status      = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.LockedAt    = null;
            job.ErrorMessage = error;

            instance.Status       = ProcessStatus.Failed;
            instance.ErrorMessage = error;
            instance.CompletedAt  = DateTime.UtcNow;

            _logger.LogError("Job {JobId} permanently failed: {Error}", job.Id, error);

            await _audit.WriteFailureAsync(new AuditWriteRequest
            {
                EventType  = AuditEventTypes.JobFailed,
                Category   = AuditCategories.Job,
                Severity   = AuditSeverity.Error,
                EntityType = "Job",
                EntityId   = job.Id.ToString(),
                EntityName = instance.ProcessDefinition?.Name,
                Action     = "Job failed",
                Details    = new { instanceId = instance.Id, error },
            }, cancellationToken);
        }

        await AddJobLogAsync(job.Id, null, "Error", error, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (!shouldRetry)
            await PublishInstanceCompletedAsync(instance, ProcessStatus.Failed, cancellationToken);
    }

    // ------------------------------------------------------------------ //
    // Failure Report
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Creates a plain-text failure report artifact and registers it in the workflow context.
    /// Returns the artifact name so callers can inject it as {{failureReportArtifactName}}.
    /// Never throws — errors are logged and an empty string is returned.
    /// </summary>
    private async Task<string> CreateFailureReportAsync(
        Job job,
        ProcessInstance instance,
        StepDefinition failedStep,
        string errorMessage,
        WorkflowContext context,
        CancellationToken ct)
    {
        try
        {
            // Load definition name separately (ProcessDefinition not included in the main query)
            var defName = await _context.ProcessDefinitions
                .Where(d => d.Id == instance.ProcessDefinitionId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct) ?? "Unknown";

            // Determine unique artifact name for this process instance
            var existing     = await _artifactRepository.GetByProcessInstanceAsync(instance.Id, ct);
            var reportCount  = existing.Count(a => a.Name.StartsWith("failure-report-"));
            var artifactName = reportCount == 0
                ? $"failure-report-{instance.Id}.txt"
                : $"failure-report-{instance.Id}-{reportCount + 1}.txt";

            // Build report text
            var sb = new StringBuilder();
            sb.AppendLine("=====================================");
            sb.AppendLine("  WORKFLOW FAILURE REPORT");
            sb.AppendLine("=====================================");
            sb.AppendLine($"Process Instance Id : {instance.Id}");
            sb.AppendLine($"Process Definition  : {defName}");
            sb.AppendLine($"Version             : v{instance.ProcessVersion.VersionNumber}");
            sb.AppendLine($"Job Id              : {job.Id}");
            sb.AppendLine($"Started At          : {instance.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—"} UTC");
            sb.AppendLine($"Failed At           : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Triggered By        : {instance.TriggeredBy ?? "—"}");
            sb.AppendLine($"Correlation Id      : {instance.CorrelationId ?? "—"}");
            sb.AppendLine();
            sb.AppendLine("--- FAILED STEP ---");
            sb.AppendLine($"Step Id   : {failedStep.Id}");
            sb.AppendLine($"Step Name : {failedStep.Name}");
            sb.AppendLine($"Step Type : {failedStep.Type}");
            sb.AppendLine($"Error     : {errorMessage}");
            sb.AppendLine();

            if (context.Variables.Count > 0)
            {
                sb.AppendLine("--- WORKFLOW VARIABLES ---");
                foreach (var (k, v) in context.Variables.OrderBy(x => x.Key))
                    sb.AppendLine($"  {k} = {v}");
                sb.AppendLine();
            }

            if (context.Artifacts.Count > 0)
            {
                sb.AppendLine("--- ARTIFACTS ---");
                foreach (var a in context.Artifacts.Values.OrderBy(a => a.Name))
                    sb.AppendLine($"  {a.Name}  ({a.ContentType}, {a.SizeBytes:N0} bytes)");
                sb.AppendLine();
            }

            sb.AppendLine("=====================================");
            sb.AppendLine($"Generated : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("=====================================");

            // Save binary content to artifact store
            var artifactId = Guid.NewGuid();
            var bytes      = Encoding.UTF8.GetBytes(sb.ToString());
            await using var stream = new MemoryStream(bytes);
            var storagePath = await _artifactStore.SaveAsync(artifactId, artifactName, stream, ct);

            // Persist artifact metadata
            var artifactRef = new ArtifactReference
            {
                Id          = artifactId,
                Name        = artifactName,
                ContentType = "text/plain",
                StoragePath = storagePath,
                SizeBytes   = bytes.Length,
            };

            await _artifactRepository.AddAsync(artifactRef.ToEntity(instance.Id, null, isPersistent: true), ct);
            await _context.SaveChangesAsync(ct);

            // Make the artifact available to downstream steps (e.g. mail.send attachment)
            context.RegisterArtifacts(new[] { artifactRef });

            _logger.LogInformation(
                "WorkflowEngine: failure report '{Name}' created for instance {InstanceId}",
                artifactName, instance.Id);

            return artifactName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "WorkflowEngine: failed to create failure report for instance {InstanceId}", instance.Id);
            return string.Empty;
        }
    }

    private static void PersistStepOutputVariables(
        ProcessStepInstance stepInstance,
        Dictionary<string, object> outputVariables)
    {
        // InputData is unused for step inputs; store serialized output variables for job-retry rehydration.
        stepInstance.InputData = JsonSerializer.Serialize(outputVariables);
    }

    private void RehydrateStepOutputVariables(
        WorkflowContext workflowContext,
        ProcessStepInstance stepInstance,
        StepDefinition stepDef)
    {
        if (string.IsNullOrWhiteSpace(stepInstance.InputData))
        {
            _logger.LogInformation(
                "WorkflowEngine: skipped successful step '{Name}' ({Id}) — no persisted output variables to rehydrate",
                stepDef.Name, stepDef.Id);
            return;
        }

        try
        {
            var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(
                stepInstance.InputData,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (variables is null || variables.Count == 0)
            {
                _logger.LogInformation(
                    "WorkflowEngine: skipped successful step '{Name}' ({Id}) — persisted output variables empty",
                    stepDef.Name, stepDef.Id);
                return;
            }

            workflowContext.MergeVariables(variables);

            _logger.LogInformation(
                "WorkflowEngine: rehydrated {Count} output variable(s) from skipped step '{Name}' ({Id}): [{Keys}]",
                variables.Count,
                stepDef.Name,
                stepDef.Id,
                string.Join(", ", variables.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "WorkflowEngine: failed to rehydrate output variables for skipped step '{Name}' ({Id})",
                stepDef.Name, stepDef.Id);
        }
    }

    private static Dictionary<string, object> BuildInitialVariables(
        ProcessInstance instance, WorkflowDefinition definition)
    {
        var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (definition.DefaultVariables is not null)
            foreach (var (k, v) in definition.DefaultVariables)
                variables[k] = v;

        if (!string.IsNullOrEmpty(instance.InputData))
        {
            try
            {
                var inputVars = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    instance.InputData,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (inputVars is not null)
                    foreach (var (k, v) in inputVars)
                        variables[k] = NormalizeVariableValue(v);
            }
            catch { /* ignore malformed input */ }
        }

        variables["instanceId"]    = instance.Id.ToString();
        variables["correlationId"] = instance.CorrelationId ?? "";

        return variables;
    }

    /// <summary>
    /// Resets all step instances in a foreach loop body to <see cref="StepStatus.Pending"/>
    /// so they re-execute on the next iteration.
    /// </summary>
    private async Task ResetLoopBodyStepInstancesAsync(
        Guid instanceId,
        StepDefinition foreachStep,
        IReadOnlyDictionary<string, StepDefinition> stepMap,
        CancellationToken cancellationToken)
    {
        var bodyStepIds = GetLoopBodyStepIds(foreachStep, stepMap);
        if (bodyStepIds.Count == 0) return;

        var instances = await _context.ProcessStepInstances
            .Where(s => s.ProcessInstanceId == instanceId && bodyStepIds.Contains(s.StepId))
            .ToListAsync(cancellationToken);

        foreach (var si in instances)
        {
            si.Status       = StepStatus.Pending;
            si.OutputData   = null;
            si.ErrorMessage = null;
            si.CompletedAt  = null;
            si.DurationMs   = null;
            si.StartedAt    = null;
        }

        if (instances.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "ForEach {StepId}: reset {Count} loop-body step instance(s) for next iteration",
                foreachStep.Id, instances.Count);
        }
    }

    /// <summary>
    /// Returns all step IDs reachable from <see cref="StepDefinition.LoopStepId"/>
    /// without traversing back into the foreach step itself.
    /// </summary>
    private static HashSet<string> GetLoopBodyStepIds(
        StepDefinition foreachStep,
        IReadOnlyDictionary<string, StepDefinition> stepMap)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(foreachStep.LoopStepId)) return result;

        var queue = new Queue<string>();
        queue.Enqueue(foreachStep.LoopStepId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (string.Equals(id, foreachStep.Id, StringComparison.OrdinalIgnoreCase)) continue;
            if (!result.Add(id)) continue;
            if (!stepMap.TryGetValue(id, out var step)) continue;

            foreach (var next in GetForwardStepIds(step))
            {
                if (!string.Equals(next, foreachStep.Id, StringComparison.OrdinalIgnoreCase))
                    queue.Enqueue(next);
            }
        }

        return result;
    }

    private static IEnumerable<string> GetForwardStepIds(StepDefinition step)
    {
        if (!string.IsNullOrEmpty(step.NextStepId))      yield return step.NextStepId;
        if (!string.IsNullOrEmpty(step.TrueStepId))      yield return step.TrueStepId;
        if (!string.IsNullOrEmpty(step.FalseStepId))     yield return step.FalseStepId;
    }

    private static object NormalizeVariableValue(object value)
    {
        if (value is not JsonElement je)
            return value;

        return je.ValueKind switch
        {
            JsonValueKind.String  => je.GetString() ?? "",
            JsonValueKind.Number  => je.TryGetInt64(out var l) ? l : je.GetDouble(),
            JsonValueKind.True    => true,
            JsonValueKind.False   => false,
            JsonValueKind.Null    => "",
            _                     => je.GetRawText()
        };
    }
}
