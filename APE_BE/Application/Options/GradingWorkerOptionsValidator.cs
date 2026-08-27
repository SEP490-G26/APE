using Microsoft.Extensions.Options;

namespace Application.Options;

public sealed class GradingWorkerOptionsValidator
    : IValidateOptions<GradingWorkerOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        GradingWorkerOptions options)
    {
        var errors = new List<string>();

        if (options.IdleDelaySeconds <= 0)
            errors.Add("GradingWorker:IdleDelaySeconds must be greater than zero.");

        if (options.BatchSize <= 0)
            errors.Add("GradingWorker:BatchSize must be greater than zero.");

        if (options.MaximumExecutionAttempts <= 0)
            errors.Add("GradingWorker:MaximumExecutionAttempts must be greater than zero.");

        if (options.InitialRetryDelaySeconds <= 0)
            errors.Add("GradingWorker:InitialRetryDelaySeconds must be greater than zero.");

        if (options.MaximumProcessingSeconds <= 0)
            errors.Add("GradingWorker:MaximumProcessingSeconds must be greater than zero.");

        if (options.LeaseSeconds <= 0)
            errors.Add("GradingWorker:LeaseSeconds must be greater than zero.");

        if (options.LeaseRenewalSeconds <= 0)
            errors.Add("GradingWorker:LeaseRenewalSeconds must be greater than zero.");

        if (options.LeaseRenewalSeconds >= options.LeaseSeconds)
            errors.Add("GradingWorker:LeaseRenewalSeconds must be less than LeaseSeconds.");

        if (options.PollingIntervalSeconds <= 0)
            errors.Add("GradingWorker:PollingIntervalSeconds must be greater than zero.");

        if (options.PollingIntervalSeconds >= options.LeaseSeconds)
            errors.Add("GradingWorker:PollingIntervalSeconds must be less than LeaseSeconds.");

        if (options.InitialRetryDelaySeconds >= options.LeaseSeconds)
            errors.Add("GradingWorker:InitialRetryDelaySeconds must be less than LeaseSeconds.");

        if (options.LeaseRenewalSeconds + options.PollingIntervalSeconds >= options.LeaseSeconds)
            errors.Add("GradingWorker:LeaseRenewalSeconds plus PollingIntervalSeconds must be less than LeaseSeconds.");

        if (options.MaximumProcessingSeconds <= options.PollingIntervalSeconds)
            errors.Add("GradingWorker:MaximumProcessingSeconds must be greater than PollingIntervalSeconds.");

        if (options.MaximumPollingAttempts <= 0)
            errors.Add("GradingWorker:MaximumPollingAttempts must be greater than zero.");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
