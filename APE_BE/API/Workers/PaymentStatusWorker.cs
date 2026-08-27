using Application.Services;

namespace API.Workers;

public class PaymentStatusWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentStatusWorker> _logger;

    public PaymentStatusWorker(IServiceScopeFactory scopeFactory, ILogger<PaymentStatusWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentStatusWorker started (polling every {Interval}s)", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var walletTopupService = scope.ServiceProvider.GetRequiredService<WalletTopupService>();
                var expiredCount = await walletTopupService.ExpirePendingPaymentsAsync(stoppingToken);

                if (expiredCount > 0)
                {
                    _logger.LogInformation("Marked {Count} expired pending payment(s) as failed.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in PaymentStatusWorker loop");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("PaymentStatusWorker stopped");
    }
}
