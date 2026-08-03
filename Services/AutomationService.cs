using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insgram_Manager.Services;

/// <summary>
/// Automation service that manages job lifecycle: enqueue, pause, resume, retry, cancel.
/// Persists all state changes to the database and broadcasts progress via SignalR.
/// Uses an in-memory channel-based queue for the background worker to consume.
/// </summary>
public sealed class AutomationService : IAutomationService
{
    private readonly IDbContextFactory<Data.ApplicationDbContext> _contextFactory;
    private readonly IAutomationProgressService _progressService;
    private readonly IOptions<Options.AutomationOptions> _options;
    private readonly ILogger<AutomationService> _logger;
    private readonly System.Threading.Channels.Channel<QueuedJobSignal> _queue;

    public AutomationService(
        IDbContextFactory<Data.ApplicationDbContext> contextFactory,
        IAutomationProgressService progressService,
        IOptions<Options.AutomationOptions> options,
        ILogger<AutomationService> logger)
    {
        _contextFactory = contextFactory;
        _progressService = progressService;
        _options = options;
        _logger = logger;
        _queue = System.Threading.Channels.Channel.CreateUnbounded<QueuedJobSignal>();
    }

    /// <summary>
    /// Reader channel for the background worker to consume queued job signals.
    /// </summary>
    public System.Threading.Channels.ChannelReader<QueuedJobSignal> QueueReader => _queue.Reader;

    public async Task<string> EnqueueJobAsync(AutomationType type, Dictionary<string, object>? parameters = null)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var maxRetries = _options.Value.MaxRetries;

        await using var context = await _contextFactory.CreateDbContextAsync();

        var job = new AutomationJob
        {
            JobId = jobId,
            Type = type,
            Status = JobStatus.Pending,
            ParametersJson = AutomationJob.SerializeParameters(parameters ?? []),
            QueuedAt = DateTime.UtcNow,
            MaxRetries = maxRetries
        };

        context.AutomationJobs.Add(job);
        await context.SaveChangesAsync();

        await _progressService.NotifyJobQueuedAsync(jobId, type);

        await _queue.Writer.WriteAsync(new QueuedJobSignal(jobId, type));

        _logger.LogInformation("Enqueued automation job {JobId} of type {Type}", jobId, type);
        return jobId;
    }

    public async Task<bool> PauseJobAsync(string jobId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var job = await context.AutomationJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job is null || (job.Status != JobStatus.Running && job.Status != JobStatus.Pending))
            return false;

        job.Status = JobStatus.Paused;
        job.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await _progressService.NotifyJobPausedAsync(jobId);
        return true;
    }

    public async Task<bool> ResumeJobAsync(string jobId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var job = await context.AutomationJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job is null || job.Status != JobStatus.Paused)
            return false;

        job.Status = JobStatus.Pending;
        job.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await _progressService.NotifyJobResumedAsync(jobId);

        await _queue.Writer.WriteAsync(new QueuedJobSignal(jobId, job.Type));
        return true;
    }

    public async Task<bool> RetryJobAsync(string jobId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var job = await context.AutomationJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job is null || job.Status != JobStatus.Failed)
            return false;

        job.Status = JobStatus.Pending;
        job.RetryCount++;
        job.ErrorMessage = null;
        job.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await _progressService.NotifyJobRetryingAsync(jobId, job.RetryCount, job.MaxRetries);

        await _queue.Writer.WriteAsync(new QueuedJobSignal(jobId, job.Type));
        return true;
    }

    public async Task<bool> CancelJobAsync(string jobId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var job = await context.AutomationJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job is null)
            return false;

        if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
            return false;

        job.Status = JobStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await _progressService.NotifyJobCancelledAsync(jobId);
        return true;
    }

    public async Task<IReadOnlyList<AutomationJobDto>> GetAllJobsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.AutomationJobs
            .AsNoTracking()
            .OrderByDescending(j => j.QueuedAt)
            .Select(j => new AutomationJobDto
            {
                JobId = j.JobId,
                Type = j.Type,
                Status = j.Status,
                QueuedAt = j.QueuedAt,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                RetryCount = j.RetryCount,
                MaxRetries = j.MaxRetries,
                ErrorMessage = j.ErrorMessage,
                ResultMessage = j.ResultMessage
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AutomationJobDto>> GetJobsByStatusAsync(JobStatus status)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.AutomationJobs
            .AsNoTracking()
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.QueuedAt)
            .Select(j => new AutomationJobDto
            {
                JobId = j.JobId,
                Type = j.Type,
                Status = j.Status,
                QueuedAt = j.QueuedAt,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                RetryCount = j.RetryCount,
                MaxRetries = j.MaxRetries,
                ErrorMessage = j.ErrorMessage,
                ResultMessage = j.ResultMessage
            })
            .ToListAsync();
    }

    public async Task<AutomationJobDto?> GetJobAsync(string jobId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var job = await context.AutomationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        if (job is null)
            return null;

        return new AutomationJobDto
        {
            JobId = job.JobId,
            Type = job.Type,
            Status = job.Status,
            QueuedAt = job.QueuedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            RetryCount = job.RetryCount,
            MaxRetries = job.MaxRetries,
            ErrorMessage = job.ErrorMessage,
            ResultMessage = job.ResultMessage
        };
    }

    public async Task<AutomationStatsDto> GetStatsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var jobs = context.AutomationJobs.AsNoTracking();

        return new AutomationStatsDto
        {
            TotalJobs = await jobs.CountAsync(),
            PendingCount = await jobs.CountAsync(j => j.Status == JobStatus.Pending),
            RunningCount = await jobs.CountAsync(j => j.Status == JobStatus.Running),
            CompletedCount = await jobs.CountAsync(j => j.Status == JobStatus.Completed),
            FailedCount = await jobs.CountAsync(j => j.Status == JobStatus.Failed),
            CancelledCount = await jobs.CountAsync(j => j.Status == JobStatus.Cancelled),
            PausedCount = await jobs.CountAsync(j => j.Status == JobStatus.Paused)
        };
    }

    /// <summary>
    /// Updates job status in the database. Called by the background worker.
    /// </summary>
    public async Task UpdateJobStatusAsync(string jobId, JobStatus status, string? errorMessage = null, string? resultMessage = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var job = await context.AutomationJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job is null)
            return;

        job.Status = status;
        job.UpdatedAt = DateTime.UtcNow;

        if (status == JobStatus.Running && job.StartedAt is null)
            job.StartedAt = DateTime.UtcNow;

        if (status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
            job.CompletedAt = DateTime.UtcNow;

        if (errorMessage is not null)
            job.ErrorMessage = errorMessage;

        if (resultMessage is not null)
            job.ResultMessage = resultMessage;

        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Signal sent through the channel queue to notify the background worker of a job to process.
/// </summary>
public sealed record QueuedJobSignal(string JobId, AutomationType Type);
