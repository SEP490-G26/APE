
using Application.Exceptions;
using Application.Interfaces;
using Application.Models;
using Application.Options;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public sealed class SubmissionGradingProcessor
    : ISubmissionGradingProcessor
{
    private const string LeaseLostMessage =
        "Submission lease was lost during processing.";
    private const string DeadlineExceededMessage =
        "Submission processing deadline exceeded.";
    private const string PollingLimitExceededMessage =
        "Submission polling limit exceeded.";
    private const double RetryJitterUpperBound = 0.20d;

    private readonly IPESubmissionRepository _submissionRepository;
    private readonly IPEQuestionRepository _questionRepository;
    private readonly ICodeExecutionClient _executionClient;
    private readonly ISubmissionResultEvaluator _resultEvaluator;
    private readonly GradingWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmissionGradingProcessor> _logger;

    public SubmissionGradingProcessor(
        IPESubmissionRepository submissionRepository,
        IPEQuestionRepository questionRepository,
        ICodeExecutionClient executionClient,
        ISubmissionResultEvaluator resultEvaluator,
        IOptions<GradingWorkerOptions> options,
        TimeProvider timeProvider,
        ILogger<SubmissionGradingProcessor> logger)
    {
        _submissionRepository = submissionRepository;
        _questionRepository = questionRepository;
        _executionClient = executionClient;
        _resultEvaluator = resultEvaluator;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessAsync(
        string submissionId,
        string leaseOwner,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
            throw new ArgumentException(
                "SubmissionId is required.",
                nameof(submissionId));

        if (string.IsNullOrWhiteSpace(leaseOwner))
            throw new ArgumentException(
                "LeaseOwner is required.",
                nameof(leaseOwner));

        var submission = await _submissionRepository.GetByIdAsync(
            submissionId,
            cancellationToken);

        if (submission is null)
        {
            _logger.LogWarning(
                "PE submission {SubmissionId} was not found.",
                submissionId);

            return;
        }

        if (submission.Status != SubmissionProcessingStatus.Processing)
            return;

        var absoluteDeadline = CalculateAbsoluteDeadline(
            ResolveProcessingStartUtc(
                submission,
                GetUtcNow()),
            TimeSpan.FromSeconds(_options.MaximumProcessingSeconds));

        if (GetRemainingProcessingTime(absoluteDeadline) ==
                TimeSpan.Zero &&
            await FailForDeadlineIfLeaseStillOwnedAsync(
                submission,
                leaseOwner,
                absoluteDeadline,
                cancellationToken))
        {
            return;
        }

        var question = await _questionRepository.GetByIdAsync(
            submission.QuestionId,
            cancellationToken);

        if (question is null)
        {
            await MarkFailedAsync(
                submission,
                leaseOwner,
                "Programming question was not found.",
                cancellationToken);

            return;
        }

        if (question.TestCases.Count == 0)
        {
            await MarkFailedAsync(
                submission,
                leaseOwner,
                "Programming question has no test cases.",
                cancellationToken);

            return;
        }

        try
        {
            if (!submission.Execution.HasTokens)
            {
                await CreateExecutionBatchAsync(
                    submission,
                    question,
                    absoluteDeadline,
                    leaseOwner,
                    cancellationToken);
            }

            if (!submission.Execution.HasTokens)
                return;

            if (!ValidateExistingExecutionMapping(
                    submission,
                    question.TestCases.Count,
                    out var tokenValidationError))
            {
                await MarkFailedAsync(
                    submission,
                    leaseOwner,
                    tokenValidationError,
                    cancellationToken);

                return;
            }

            var executionResults = await PollUntilCompletedAsync(
                submission,
                question,
                absoluteDeadline,
                leaseOwner,
                cancellationToken);

            EnsureDeadlineNotExceeded(absoluteDeadline);


            var evaluation = _resultEvaluator.Evaluate(
                question,
                executionResults,
                submission.MaxScore);

            EnsureDeadlineNotExceeded(absoluteDeadline);

            var completedAt = GetUtcNow();

            submission.Complete(
                leaseOwner,
                completedAt,
                evaluation);

            await PersistTerminalStateOrThrowLeaseLostAsync(
                submission,
                leaseOwner,
                completedAt,
                "completing the submission",
                cancellationToken);

            _logger.LogInformation(
                "PE submission {SubmissionId} completed with verdict " +
                "{Verdict} and score {Score}/{MaxScore}.",
                submission.Id,
                submission.FinalVerdict,
                submission.QuestionScore,
                submission.MaxScore);
        }
        catch (CodeExecutionClientException exception)
        {
            await HandleExecutionFailureAsync(
                submission,
                leaseOwner,
                exception,
                cancellationToken);
        }
        catch (DomainRuleException exception)
        {
            if (IsLeaseLoss(exception))
            {
                _logger.LogWarning(
                    "Stopping processing for submission {SubmissionId} because lease ownership was lost.",
                    submission.Id);

                return;
            }

            if (IsDeadlineExceeded(exception))
            {
                await FailForDeadlineIfLeaseStillOwnedAsync(
                    submission,
                    leaseOwner,
                    absoluteDeadline,
                    cancellationToken);

                return;
            }

            await MarkFailedAsync(
                submission,
                leaseOwner,
                exception.Message,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Unexpected grading error for submission {SubmissionId}. ExceptionType: {ExceptionType}.",
                submission.Id,
                exception.GetType().FullName);

            await MarkFailedAsync(
                submission,
                leaseOwner,
                "Unexpected grading error.",
                cancellationToken);
        }
    }

    private async Task CreateExecutionBatchAsync(
        PE_Submission submission,
        PEQuestion question,
        DateTime absoluteDeadline,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                EnsureDeadlineNotExceeded(absoluteDeadline);

                await RenewLeaseIfDueAsync(
                    submission,
                    leaseOwner,
                    cancellationToken);

                var request =
                    CodeExecutionBatchRequest.Create(
                        submission.LanguageId,
                        submission.SubmittedCode,
                        BuildExecutionCaseRequests(question));

                EnsureDeadlineNotExceeded(absoluteDeadline);

                var receipt =
                    await _executionClient.CreateBatchAsync(
                        request,
                        cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                EnsureDeadlineNotExceeded(absoluteDeadline);

                await RenewLeaseIfDueAsync(
                    submission,
                    leaseOwner,
                    cancellationToken);

                var executionCases = BuildExecutionCases(
                    receipt,
                    request.Cases.Count);

                var utcNow = GetUtcNow();

                submission.AttachExecutionTokens(
                    executionCases,
                    leaseOwner,
                    utcNow);

                await PersistExecutionStateOrThrowLeaseLostAsync(
                    submission,
                    leaseOwner,
                    utcNow,
                    "persisting execution tokens",
                    cancellationToken);

                return;
            }
            catch (CodeExecutionClientException exception)
                when (
                    exception.IsTransient &&
                    submission.Execution.AttemptCount <
                    _options.MaximumExecutionAttempts)
            {
                var utcNow = GetUtcNow();

                submission.RegisterTransientExecutionFailure(
                    leaseOwner,
                    utcNow,
                    exception.SafeMessage);

                await PersistExecutionStateOrThrowLeaseLostAsync(
                    submission,
                    leaseOwner,
                    utcNow,
                    "recording execution failure",
                    cancellationToken);

                var delay = CalculateRetryDelay(
                    submission.Execution.AttemptCount,
                    exception.RetryAfter);

                await DelayWithLeaseRenewalAsync(
                    submission,
                    leaseOwner,
                    delay,
                    absoluteDeadline,
                    cancellationToken);

                utcNow = GetUtcNow();

                EnsureDeadlineNotExceeded(absoluteDeadline);

                submission.BeginAdditionalExecutionAttempt(
                    leaseOwner,
                    utcNow);

                await PersistExecutionStateOrThrowLeaseLostAsync(
                    submission,
                    leaseOwner,
                    utcNow,
                    "starting another execution attempt",
                    cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<CodeExecutionResult>>
     PollUntilCompletedAsync(
        PE_Submission submission,
        PEQuestion question,
        DateTime absoluteDeadline,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        EnsureDeadlineNotExceeded(absoluteDeadline);

        await RenewLeaseIfDueAsync(
            submission,
            leaseOwner,
            cancellationToken);

        var tokens = BuildPollingTokens(
            submission,
            question.TestCases.Count);
        var transientFailureCount = 0;

        for (var pollingAttempt = 1;
             pollingAttempt <= _options.MaximumPollingAttempts;
             pollingAttempt++)
        {
            try
            {
                EnsureDeadlineNotExceeded(absoluteDeadline);

                await RenewLeaseIfDueAsync(
                    submission,
                    leaseOwner,
                    cancellationToken);

                EnsureDeadlineNotExceeded(absoluteDeadline);

                var results =
                    await GetBatchResultsAsync(
                        tokens,
                        cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                EnsureDeadlineNotExceeded(absoluteDeadline);

                await RenewLeaseIfDueAsync(
                    submission,
                    leaseOwner,
                    cancellationToken);

                transientFailureCount = 0;

                if (results.All(result => result.IsTerminal))
                    return results;

                await DelayWithLeaseRenewalAsync(
                    submission,
                    leaseOwner,
                    TimeSpan.FromSeconds(
                        _options.PollingIntervalSeconds),
                    absoluteDeadline,
                    cancellationToken);
            }
            catch (CodeExecutionClientException exception)
                when (exception.IsTransient)
            {
                transientFailureCount++;

                var utcNow = GetUtcNow();

                submission.RegisterTransientExecutionFailure(
                    leaseOwner,
                    utcNow,
                    exception.SafeMessage);

                await PersistExecutionStateOrThrowLeaseLostAsync(
                    submission,
                    leaseOwner,
                    utcNow,
                    "recording polling failure",
                    cancellationToken);

                if (transientFailureCount >=
                    _options.MaximumExecutionAttempts)
                {
                    throw;
                }

                var delay = CalculateRetryDelay(
                    transientFailureCount,
                    exception.RetryAfter);

                _logger.LogWarning(
                    "Transient provider polling failure for submission " +
                    "{SubmissionId}. ProviderName: {ProviderName}. " +
                    "ErrorCategory: {ErrorCategory}. IsTransient: {IsTransient}. " +
                    "RetryAfter: {RetryAfter}. Delay: {Delay}.",
                    submission.Id,
                    exception.ProviderName,
                    exception.ErrorCategory,
                    exception.IsTransient,
                    exception.RetryAfter,
                    delay);

                await DelayWithLeaseRenewalAsync(
                    submission,
                    leaseOwner,
                    delay,
                    absoluteDeadline,
                    cancellationToken);
            }
        }

        throw new CodeExecutionClientException(
            PollingLimitExceededMessage,
            isTransient: false,
            errorCategory: CodeExecutionErrorCategory.Timeout);
    }

    private async Task HandleExecutionFailureAsync(
        PE_Submission submission,
        string leaseOwner,
        CodeExecutionClientException exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            "Provider processing failed for submission {SubmissionId}. " +
            "ProviderName: {ProviderName}. ErrorCategory: {ErrorCategory}. " +
            "IsTransient: {IsTransient}. StatusCode: {StatusCode}. RetryAfter: {RetryAfter}.",
            submission.Id,
            exception.ProviderName,
            exception.ErrorCategory,
            exception.IsTransient,
            exception.StatusCode,
            exception.RetryAfter);

        await MarkFailedAsync(
            submission,
            leaseOwner,
            exception.SafeMessage,
            cancellationToken);
    }

    private async Task MarkFailedAsync(
        PE_Submission submission,
        string leaseOwner,
        string error,
        CancellationToken cancellationToken)
    {
        if (submission.Status is
            SubmissionProcessingStatus.Completed or
            SubmissionProcessingStatus.Failed)
        {
            return;
        }

        var failedAt = GetUtcNow();

        try
        {
            submission.MarkProcessingFailed(
                leaseOwner,
                failedAt,
                error);
        }
        catch (DomainRuleException exception)
            when (IsLeaseLoss(exception))
        {
            _logger.LogWarning(
                "Lease lost while marking submission {SubmissionId} as failed. The submission was left unchanged.",
                submission.Id);

            return;
        }

        try
        {
            await PersistTerminalStateOrThrowLeaseLostAsync(
                submission,
                leaseOwner,
                failedAt,
                "marking the submission as failed",
                cancellationToken);
        }
        catch (DomainRuleException exception)
            when (IsLeaseLoss(exception))
        {
            _logger.LogWarning(
                "Lease lost while persisting failure for submission {SubmissionId}. The submission was left unchanged.",
                submission.Id);

            return;
        }

        _logger.LogWarning(
            "PE submission {SubmissionId} marked as failed. ErrorCategory: {ErrorCategory}.",
            submission.Id,
            CategorizeFailure(error));
    }

    private TimeSpan CalculateRetryDelay(
        int attemptNumber,
        TimeSpan? retryAfter)
    {
        var backoff = CalculateBackoffWithJitter(
            attemptNumber);

        if (retryAfter is not { } retryAfterValue ||
            retryAfterValue <= TimeSpan.Zero)
        {
            return backoff;
        }

        return retryAfterValue > backoff
            ? retryAfterValue
            : backoff;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private async Task RenewLeaseIfDueAsync(
        PE_Submission submission,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        if (submission.LeaseExpiresAt is null)
        {
            throw new DomainRuleException(LeaseLostMessage);
        }

        var utcNow = GetUtcNow();
        var renewalThreshold = TimeSpan.FromSeconds(
            _options.LeaseRenewalSeconds);

        if (submission.LeaseExpiresAt.Value - utcNow > renewalThreshold)
        {
            return;
        }

        var newLeaseExpiresAt = utcNow.AddSeconds(_options.LeaseSeconds);

        var renewed = await _submissionRepository.RenewLeaseAsync(
            submission.Id,
            leaseOwner,
            utcNow,
            newLeaseExpiresAt,
            cancellationToken);

        if (!renewed)
        {
            throw new DomainRuleException(LeaseLostMessage);
        }

        submission.RenewLease(
            leaseOwner,
            newLeaseExpiresAt,
            utcNow);
    }

    private DateTime ResolveProcessingStartUtc(
        PE_Submission submission,
        DateTime utcNow)
    {
        if (submission.ProcessingStartedAt is { } processingStartedAt)
        {
            return processingStartedAt;
        }

        if (submission.LeaseAcquiredAt is { } leaseAcquiredAt)
        {
            return leaseAcquiredAt;
        }

        _logger.LogWarning(
            "Submission {SubmissionId} had no persisted processing timestamps; using current UTC time as deadline anchor.",
            submission.Id);

        return utcNow;
    }

    private static DateTime CalculateAbsoluteDeadline(
        DateTime effectiveStartUtc,
        TimeSpan maximumProcessingDuration)
    {
        var maxTicks = DateTime.MaxValue.Ticks -
                       maximumProcessingDuration.Ticks;

        if (effectiveStartUtc.Ticks > maxTicks)
        {
            return new DateTime(
                DateTime.MaxValue.Ticks,
                DateTimeKind.Utc);
        }

        return new DateTime(
            effectiveStartUtc.Ticks +
            maximumProcessingDuration.Ticks,
            DateTimeKind.Utc);
    }

    private TimeSpan GetRemainingProcessingTime(
        DateTime absoluteDeadline)
    {
        var remaining = absoluteDeadline - GetUtcNow();
        return remaining > TimeSpan.Zero
            ? remaining
            : TimeSpan.Zero;
    }

    private void EnsureDeadlineNotExceeded(
        DateTime absoluteDeadline)
    {
        if (GetRemainingProcessingTime(absoluteDeadline) ==
            TimeSpan.Zero)
        {
            throw new DomainRuleException(
                DeadlineExceededMessage);
        }
    }

    private TimeSpan CalculateBackoffWithJitter(
        int attemptNumber)
    {
        var exponent = Math.Max(
            attemptNumber - 1,
            0);
        var shift = Math.Min(exponent, 30);
        var multiplier = 1 << shift;
        var seconds = Math.Min(
            (double)_options.InitialRetryDelaySeconds *
            multiplier,
            30d);
        var backoff = TimeSpan.FromSeconds(seconds);
        var jitterTicks = (long)Math.Round(
            backoff.Ticks *
            Random.Shared.NextDouble() *
            RetryJitterUpperBound,
            MidpointRounding.AwayFromZero);

        return backoff.Add(
            TimeSpan.FromTicks(
                Math.Max(jitterTicks, 0L)));
    }

    private async Task DelayWithLeaseRenewalAsync(
        PE_Submission submission,
        string leaseOwner,
        TimeSpan requestedDelay,
        DateTime absoluteDeadline,
        CancellationToken cancellationToken)
    {
        if (requestedDelay <= TimeSpan.Zero)
        {
            return;
        }

        var remaining = GetRemainingProcessingTime(
            absoluteDeadline);

        if (requestedDelay >= remaining)
        {
            throw new DomainRuleException(
                DeadlineExceededMessage);
        }

        var maxChunk = TimeSpan.FromSeconds(
            _options.LeaseRenewalSeconds);
        var remainingDelay = requestedDelay;

        while (remainingDelay > TimeSpan.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureDeadlineNotExceeded(absoluteDeadline);

            await RenewLeaseIfDueAsync(
                submission,
                leaseOwner,
                cancellationToken);

            remaining = GetRemainingProcessingTime(
                absoluteDeadline);

            if (remainingDelay >= remaining)
            {
                throw new DomainRuleException(
                    DeadlineExceededMessage);
            }

            var chunk = remainingDelay < maxChunk
                ? remainingDelay
                : maxChunk;

            if (chunk >= remaining)
            {
                throw new DomainRuleException(
                    DeadlineExceededMessage);
            }

            await Task.Delay(
                chunk,
                _timeProvider,
                cancellationToken);

            remainingDelay -= chunk;

            cancellationToken.ThrowIfCancellationRequested();
            EnsureDeadlineNotExceeded(absoluteDeadline);

            if (remainingDelay > TimeSpan.Zero)
            {
                await RenewLeaseIfDueAsync(
                    submission,
                    leaseOwner,
                    cancellationToken);
            }
        }
    }

    private async Task<bool> FailForDeadlineIfLeaseStillOwnedAsync(
        PE_Submission submission,
        string leaseOwner,
        DateTime absoluteDeadline,
        CancellationToken cancellationToken)
    {
        var utcNow = GetUtcNow();

        if (!submission.HasValidLease(
                leaseOwner,
                utcNow))
        {
            _logger.LogWarning(
                "Stopping submission {SubmissionId} after deadline expiration because lease ownership was already lost.",
                submission.Id);
            return true;
        }

        try
        {
            submission.MarkProcessingFailed(
                leaseOwner,
                utcNow,
                DeadlineExceededMessage);
        }
        catch (DomainRuleException exception)
            when (IsLeaseLoss(exception))
        {
            _logger.LogWarning(
                "Stopping submission {SubmissionId} after deadline expiration because lease ownership was lost during failure transition.",
                submission.Id);
            return true;
        }

        try
        {
            await PersistTerminalStateOrThrowLeaseLostAsync(
                submission,
                leaseOwner,
                utcNow,
                "persisting deadline failure",
                cancellationToken);
        }
        catch (DomainRuleException exception)
            when (IsLeaseLoss(exception))
        {
            _logger.LogWarning(
                "Stopping submission {SubmissionId} after deadline expiration because terminal CAS failed due to lease loss.",
                submission.Id);
            return true;
        }

        _logger.LogWarning(
            "PE submission {SubmissionId} failed because the processing deadline was exceeded.",
            submission.Id);

        return true;
    }

    private static IReadOnlyList<ExecutionCaseTracking> BuildExecutionCases(
        CodeExecutionBatchReceipt receipt,
        int expectedCount)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (receipt.Tokens.Count != expectedCount)
        {
            throw new DomainRuleException(
                "Execution receipt token count does not match the expected test case count.");
        }

        var caseIndexes = receipt.Tokens
            .Select(item => item.CaseIndex)
            .OrderBy(index => index)
            .ToArray();

        if (!caseIndexes.SequenceEqual(Enumerable.Range(0, expectedCount)))
        {
            throw new DomainRuleException(
                "Execution receipt tokens must cover the expected contiguous case index range.");
        }

        var duplicateToken = receipt.Tokens
            .GroupBy(token => token.ProviderToken, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateToken is not null)
        {
            throw new DomainRuleException(
                "Duplicate execution receipt tokens were returned.");
        }

        var executionCases = receipt.Tokens
            .OrderBy(item => item.CaseIndex)
            .Select(item => ExecutionCaseTracking.Create(
                item.CaseIndex,
                item.ProviderToken))
            .ToList();

        return executionCases;
    }

    private static IReadOnlyList<CodeExecutionCaseRequest> BuildExecutionCaseRequests(
        PEQuestion question)
    {
        return question.TestCases
            .Select((testCase, index) =>
                new CodeExecutionCaseRequest(
                    index,
                    testCase.Input ?? string.Empty,
                    testCase.TimeLimitMs,
                    testCase.MemoryLimitKb))
            .ToList();
    }

    private static IReadOnlyList<CodeExecutionToken> BuildPollingTokens(
        PE_Submission submission,
        int expectedCount)
    {
        if (submission.Execution.Cases.Count > 0)
        {
            return submission.Execution.Cases
                .OrderBy(item => item.CaseIndex)
                .Select(item => new CodeExecutionToken(
                    item.CaseIndex,
                    item.ProviderToken))
                .ToList();
        }

        var tokens = submission.Execution.Tokens.ToList();

        if (tokens.Count != expectedCount)
        {
            throw new DomainRuleException(
                "Persisted execution tokens do not match the expected test case count.");
        }

        return tokens
            .Select((token, index) => new CodeExecutionToken(index, token))
            .ToList();
    }

    private async Task<IReadOnlyList<CodeExecutionResult>> GetBatchResultsAsync(
        IReadOnlyList<CodeExecutionToken> tokens,
        CancellationToken cancellationToken)
    {
        var results = await _executionClient.GetBatchResultsAsync(
            tokens.Select(item => item.ProviderToken).ToList(),
            cancellationToken);

        var caseIndexByToken = tokens.ToDictionary(
            item => item.ProviderToken,
            item => item.CaseIndex,
            StringComparer.Ordinal);

        return results.Select(result =>
        {
            if (!caseIndexByToken.TryGetValue(
                    result.ProviderToken,
                    out var caseIndex))
            {
                throw new CodeExecutionClientException(
                    "The provider returned an unexpected polling token.",
                    isTransient: false,
                    errorCategory: CodeExecutionErrorCategory.Provider);
            }

            return result with { CaseIndex = caseIndex };
        }).ToList();
    }

    private static bool ValidateExistingExecutionMapping(
        PE_Submission submission,
        int expectedCount,
        out string error)
    {
        if (submission.Execution.Cases.Count > 0)
        {
            var caseIndexes = submission.Execution.Cases
                .Select(item => item.CaseIndex)
                .OrderBy(index => index)
                .ToArray();

            if (caseIndexes.Length != expectedCount ||
                !caseIndexes.SequenceEqual(Enumerable.Range(0, expectedCount)))
            {
                error =
                    "Persisted execution cases do not match the expected test case count.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        var tokens = submission.Execution.Tokens.ToList();

        if (tokens.Count != expectedCount)
        {
            error =
                "Persisted execution tokens do not match the expected test case count.";
            return false;
        }

        if (tokens.Any(string.IsNullOrWhiteSpace))
        {
            error =
                "Persisted execution tokens cannot contain empty values.";
            return false;
        }

        if (tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Count)
        {
            error =
                "Persisted execution tokens must be unique.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsLeaseLoss(DomainRuleException exception)
    {
        return exception.Message.Contains(
                   "valid lease for the specified owner",
                   StringComparison.Ordinal) ||
               exception.Message.Contains(
                    "lease was lost",
                    StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeadlineExceeded(
        DomainRuleException exception)
    {
        return string.Equals(
            exception.Message,
            DeadlineExceededMessage,
            StringComparison.Ordinal);
    }

    private async Task PersistExecutionStateOrThrowLeaseLostAsync(
        PE_Submission submission,
        string leaseOwner,
        DateTime utcNow,
        string action,
        CancellationToken cancellationToken)
    {
        var persisted = await _submissionRepository
            .TryPersistExecutionStateAsync(
                submission,
                leaseOwner,
                utcNow,
                cancellationToken);

        if (!persisted)
        {
            _logger.LogWarning(
                "Skipping submission {SubmissionId} after concurrent update while {Action}.",
                submission.Id,
                action);

            throw new DomainRuleException(LeaseLostMessage);
        }
    }

    private async Task PersistTerminalStateOrThrowLeaseLostAsync(
        PE_Submission submission,
        string leaseOwner,
        DateTime utcNow,
        string action,
        CancellationToken cancellationToken)
    {
        var persisted = await _submissionRepository
            .TryPersistTerminalStateAsync(
                submission,
                leaseOwner,
                utcNow,
                cancellationToken);

        if (!persisted)
        {
            _logger.LogWarning(
                "Skipping submission {SubmissionId} after concurrent update while {Action}.",
                submission.Id,
                action);

            throw new DomainRuleException(LeaseLostMessage);
        }
    }

    private static string CategorizeFailure(string error)
    {
        if (string.Equals(
                error,
                DeadlineExceededMessage,
                StringComparison.Ordinal))
        {
            return "Deadline";
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            return "Unknown";
        }

        if (error.Contains("question", StringComparison.OrdinalIgnoreCase))
        {
            return "Question";
        }

        if (error.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            return "ProviderTokenMapping";
        }

        if (error.Contains("provider", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Judge0", StringComparison.OrdinalIgnoreCase))
        {
            return "Provider";
        }

        return "Processing";
    }
}
