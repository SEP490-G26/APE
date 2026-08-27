namespace BE_IntegrationTests.Infrastructure;

public static class PollingHelper
{
    public static async Task<T> WaitAsync<T>(
        Func<Task<T>> probe,
        Func<T, bool> completed,
        TimeSpan timeout,
        TimeSpan interval,
        string failureMessage)
    {
        var deadline = DateTime.UtcNow + timeout;
        T latest = default!;

        while (DateTime.UtcNow < deadline)
        {
            latest = await probe();

            if (completed(latest))
            {
                return latest;
            }

            await Task.Delay(interval);
        }

        throw new AssertFailedException(
            $"{failureMessage} Timeout={timeout}. LastObserved={latest}");
    }
}
