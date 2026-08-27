namespace Application.Options;

public sealed class PECodeRunOptions
{
    public int MaximumPollingAttempts { get; init; } = 20;

    public int PollingDelayMilliseconds { get; init; } = 1200;
}
