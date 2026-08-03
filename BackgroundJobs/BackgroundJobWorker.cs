using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insgram_Manager.BackgroundJobs;

/// <summary>
/// Background worker that consumes the automation job queue channel.
/// Runs as a hosted service, processing jobs concurrently up to MaxConcurrentWorkers.
/// Each job is executed via the strategy pattern — currently a no-op placeholder
/// since Instagram communication is Phase 6. The worker handles retry logic,
/// cancellation checks, and broadcasts progress via SignalR.
/// </summary>
public sealed class BackgroundJobWorker : BackgroundService
{
    private readonly AutomationService _automationService;
    private readonly IAutomationProgressService _progressService;
    private readonly Options.AutomationOptions _options;
    private readonly ILogger<BackgroundJobWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public BackgroundJobWorker(
        AutomationService automationService,
        IAutomationProgressService progressService,
        IOptions<Options.AutomationOptions> options,
        ILogger<BackgroundJobWorker> logger)
    {
        _automationService = automationService;
        _progressService = progressService;
        _options = options.Value;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrentWorkers, _options.MaxConcurrentWorkers);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Automation engine is disabled. Background worker will not start.");
            return;
        }

        _logger.LogInformation("Background job worker started. Max concurrent workers: {Max}", _options.MaxConcurrentWorkers);

        await foreach (var signal in _automationService.QueueReader.ReadAllAsync(stoppingToken))
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);

            _ = ProcessJobAsync(signal, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(QueuedJobSignal signal, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _automationService.GetJobAsync(signal.JobId);
            if (job is null || job.Status is JobStatus.Cancelled or JobStatus.Paused)
                return;

            await _automationService.UpdateJobStatusAsync(signal.JobId, JobStatus.Running);
            await _progressService.NotifyJobStartedAsync(signal.JobId, signal.Type);

            _logger.LogInformation("Processing automation job {JobId} of type {Type}", signal.JobId, signal.Type);

            try
            {
                var result = await ExecuteJobAsync(signal, cancellationToken);

                if (result.Success)
                {
                    await _automationService.UpdateJobStatusAsync(
                        signal.JobId, JobStatus.Completed, resultMessage: result.Message);
                    await _progressService.NotifyJobCompletedAsync(signal.JobId, signal.Type, result.Message);
                }
                else
                {
                    await HandleJobFailureAsync(signal, result.Error ?? "Unknown error", cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await _automationService.UpdateJobStatusAsync(signal.JobId, JobStatus.Cancelled, "Worker shutting down.");
                await _progressService.NotifyJobCancelledAsync(signal.JobId);
            }
            catch (Exception ex)
            {
                await HandleJobFailureAsync(signal, ex.Message, cancellationToken);
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private async Task HandleJobFailureAsync(QueuedJobSignal signal, string error, CancellationToken cancellationToken)
    {
        var job = await _automationService.GetJobAsync(signal.JobId);
        if (job is null)
            return;

        if (job.RetryCount < job.MaxRetries)
        {
            _logger.LogWarning("Job {JobId} failed, scheduling retry {RetryCount}/{MaxRetries}: {Error}",
                signal.JobId, job.RetryCount + 1, job.MaxRetries, error);

            await _automationService.UpdateJobStatusAsync(signal.JobId, JobStatus.Retrying, errorMessage: error);
            await _progressService.NotifyJobRetryingAsync(signal.JobId, job.RetryCount + 1, job.MaxRetries);

            await Task.Delay(_options.RetryDelayMs, cancellationToken);

            await _automationService.RetryJobAsync(signal.JobId);
        }
        else
        {
            await _automationService.UpdateJobStatusAsync(signal.JobId, JobStatus.Failed, errorMessage: error);
            await _progressService.NotifyJobFailedAsync(signal.JobId, signal.Type, error);
        }
    }

    /// <summary>
    /// Executes the job based on its automation type.
    /// Phase 5: No Instagram communication yet — returns a placeholder result.
    /// Phase 6 will inject IAutomationStrategy implementations.
    /// </summary>
    private static Task<(bool Success, string? Message, string? Error)> ExecuteJobAsync(
        QueuedJobSignal signal, CancellationToken cancellationToken)
    {
        return signal.Type switch
        {
            AutomationType.CancelPendingRequests => Task.FromResult<(bool Success, string? Message, string? Error)>(
                (false, null, "Cancel pending requests requires Instagram communication (Phase 6).")),
            _ => Task.FromResult<(bool Success, string? Message, string? Error)>(
                (false, null, $"{signal.Type} automation is not yet implemented. Requires Instagram communication layer (Phase 6)."))
        };
    }

    public override void Dispose()
    {
        _concurrencyLimiter.Dispose();
        base.Dispose();
    }
}
