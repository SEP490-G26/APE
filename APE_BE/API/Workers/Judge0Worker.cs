using Application.Interfaces;
using Application.Options;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Workers;

public sealed class Judge0Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GradingWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly string _workerId;
    private readonly ILogger<Judge0Worker> _logger;

    public Judge0Worker(
        IServiceScopeFactory scopeFactory,
        IOptions<GradingWorkerOptions> options,
        TimeProvider timeProvider,
        ILogger<Judge0Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _workerId =
            $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Judge0 grading worker started with WorkerId {WorkerId}.",
            _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount =
                    await ProcessAvailableSubmissionsAsync(
                        stoppingToken);

                if (processedCount == 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(
                            _options.IdleDelaySeconds),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Unhandled Judge0 worker loop error. WorkerId: {WorkerId}. ExceptionType: {ExceptionType}.",
                    _workerId,
                    exception.GetType().FullName);

                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _options.IdleDelaySeconds),
                    stoppingToken);
            }
        }

        _logger.LogInformation(
            "Judge0 grading worker stopped.");
    }

    private async Task<int> ProcessAvailableSubmissionsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IPESubmissionRepository>();

        var processor = scope.ServiceProvider
            .GetRequiredService<ISubmissionGradingProcessor>();

        var pendingSubmissions =
            await repository.GetByStatusesAsync(
                [SubmissionProcessingStatus.Pending],
                _options.BatchSize,
                cancellationToken);

        var processedCount = 0;

        foreach (var pendingSubmission in pendingSubmissions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leaseAcquiredAt = GetUtcNow();
            var claimed =
                await repository.TryAcquirePendingAsync(
                    pendingSubmission.Id,
                    _workerId,
                    leaseAcquiredAt,
                    leaseAcquiredAt.AddSeconds(_options.LeaseSeconds),
                    cancellationToken);

            if (claimed is null)
            {
                continue;
            }

            await processor.ProcessAsync(
                claimed.Id,
                _workerId,
                cancellationToken);

            processedCount++;
        }

        var remainingCapacity =
            _options.BatchSize - processedCount;

        if (remainingCapacity <= 0)
        {
            return processedCount;
        }

        var processingSubmissions =
            await repository.GetByStatusesAsync(
                [SubmissionProcessingStatus.Processing],
                remainingCapacity,
                cancellationToken);

        foreach (var submission in processingSubmissions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leaseAcquiredAt = GetUtcNow();
            var reclaimed =
                await repository.TryReclaimExpiredProcessingAsync(
                    submission.Id,
                    _workerId,
                    leaseAcquiredAt,
                    leaseAcquiredAt.AddSeconds(_options.LeaseSeconds),
                    cancellationToken);

            if (reclaimed is null)
            {
                continue;
            }

            await processor.ProcessAsync(
                reclaimed.Id,
                _workerId,
                cancellationToken);

            processedCount++;
        }

        return processedCount;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
