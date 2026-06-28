using Company.Orchestrator.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Worker.Workers;

public class JobPollerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobPollerWorker> _logger;
    private readonly string _workerInstanceId;

    private static readonly int PollingIntervalMs = 5_000;
    private static readonly int BatchSize = 10;

    public JobPollerWorker(
        IServiceProvider serviceProvider,
        IWorkerIdentityProvider identity,
        ILogger<JobPollerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _workerInstanceId = identity.WorkerId;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobPollerWorker started | WorkerInstanceId: {WorkerInstanceId}", _workerInstanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in polling loop");
            }

            await Task.Delay(PollingIntervalMs, stoppingToken);
        }

        _logger.LogInformation("JobPollerWorker stopping");
    }

    private async Task PollAndExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

        var pendingJobs = await jobRepository.GetPendingJobsAsync(BatchSize, cancellationToken);

        if (pendingJobs.Count == 0)
        {
            _logger.LogDebug("No pending jobs found");
            return;
        }

        _logger.LogInformation("Found {Count} pending jobs", pendingJobs.Count);

        var tasks = pendingJobs.Select(job => ExecuteJobWithLockAsync(
            job.Id, jobRepository, workflowEngine, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteJobWithLockAsync(
        Guid jobId,
        IJobRepository jobRepository,
        IWorkflowEngine workflowEngine,
        CancellationToken cancellationToken)
    {
        var acquired = await jobRepository.TryAcquireLockAsync(jobId, _workerInstanceId, cancellationToken);

        if (!acquired)
        {
            _logger.LogDebug("Could not acquire lock for Job {JobId}, skipping", jobId);
            return;
        }

        _logger.LogInformation("Executing Job {JobId}", jobId);

        try
        {
            await workflowEngine.ExecuteJobAsync(jobId, cancellationToken);
            _logger.LogInformation("Job {JobId} executed successfully", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Job {JobId}", jobId);
        }
    }
}
