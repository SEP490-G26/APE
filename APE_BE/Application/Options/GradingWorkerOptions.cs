using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Options;

public sealed class GradingWorkerOptions
{
    public const string SectionName = "GradingWorker";

    public int IdleDelaySeconds { get; init; } = 2;

    public int BatchSize { get; init; } = 10;

    public int MaximumExecutionAttempts { get; init; } = 3;

    public int InitialRetryDelaySeconds { get; init; } = 2;

    public int LeaseSeconds { get; init; } = 120;

    public int LeaseRenewalSeconds { get; init; } = 30;

    public int PollingIntervalSeconds { get; init; } = 2;

    public int MaximumPollingAttempts { get; init; } = 30;

    public int MaximumProcessingSeconds { get; init; } = 90;
}
