using Domain.Enums;
using Domain.Exceptions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public sealed class PE_Submission
{
    [BsonElement("TopicTags")]
    private List<string> _topicTags = [];

    [BsonElement("SubmittedCode")]
    private List<CodeFile> _submittedCode = [];

    [BsonElement("TestResultItems")]
    private List<TestResultItem> _testResultItems = [];

    private PE_Submission()
    {
    }

    private PE_Submission(
        string id,
        string questionId,
        string sessionId,
        string? courseId,
        SubmissionMode mode,
        IEnumerable<string>? topicTags,
        IEnumerable<CodeFile> submittedCode,
        int languageId,
        double maxScore,
        int attemptCount,
        DateTime submittedAt)
    {
        Id = id;
        QuestionId = questionId;
        SessionId = sessionId;
        CourseId = courseId;
        Mode = mode;
        LanguageId = languageId;
        MaxScore = maxScore;
        AttemptCount = attemptCount;
        SubmittedAt = submittedAt;

        Status = SubmissionProcessingStatus.Pending;
        Execution = ExecutionTracking.Create();

        if (topicTags is not null)
        {
            _topicTags.AddRange(
                topicTags
                    .Where(tag =>
                        !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase));
        }

        _submittedCode.AddRange(submittedCode);
    }

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string QuestionId { get; private set; } =
        string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string SessionId { get; private set; } =
        string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CourseId { get; private set; }

    public SubmissionMode Mode { get; private set; }

    [BsonIgnore]
    public IReadOnlyCollection<string> TopicTags =>
        _topicTags.AsReadOnly();

    [BsonIgnore]
    public IReadOnlyCollection<CodeFile> SubmittedCode =>
        _submittedCode.AsReadOnly();

    public int LanguageId { get; private set; }

    public double MaxScore { get; private set; }

    public DateTime SubmittedAt { get; private set; }

    public DateTime? ProcessingStartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public string? LeaseOwner { get; private set; }

    public DateTime? LeaseAcquiredAt { get; private set; }

    public DateTime? LeaseExpiresAt { get; private set; }

    public SubmissionProcessingStatus Status { get; private set; }

    public SubmissionVerdict? FinalVerdict { get; private set; }

    public int TestCasesPassed { get; private set; }

    public int TotalTestCases { get; private set; }

    public double QuestionScore { get; private set; }

    public int RuntimeMs { get; private set; }

    public int MemoryKb { get; private set; }

    public int AttemptCount { get; private set; }

    public ExecutionTracking Execution { get; private set; } =
        null!;

    [BsonIgnore]
    public IReadOnlyCollection<TestResultItem> TestResultItems =>
        _testResultItems.AsReadOnly();

    public string? ProcessingError { get; private set; }

    public static PE_Submission Create(
        string id,
        string questionId,
        string sessionId,
        string? courseId,
        SubmissionMode mode,
        IEnumerable<string>? topicTags,
        IEnumerable<CodeFile> submittedCode,
        int languageId,
        double maxScore,
        int attemptCount,
        DateTime submittedAt)
    {
        ValidateRequiredIdentifier(id, nameof(id));

        ValidateRequiredIdentifier(
            questionId,
            nameof(questionId));

        ValidateRequiredIdentifier(
            sessionId,
            nameof(sessionId));

        if (languageId <= 0)
        {
            throw new DomainRuleException(
                "LanguageId must be greater than zero.");
        }

        if (maxScore <= 0)
        {
            throw new DomainRuleException(
                "MaxScore must be greater than zero.");
        }

        if (attemptCount <= 0)
        {
            throw new DomainRuleException(
                "Attempt count must be greater than zero.");
        }

        EnsureUtc(submittedAt);

        ArgumentNullException.ThrowIfNull(submittedCode);

        var files = submittedCode.ToList();

        if (files.Count == 0)
        {
            throw new DomainRuleException(
                "At least one source-code file is required.");
        }

        if (files.Any(file => file is null))
        {
            throw new DomainRuleException(
                "Submitted code cannot contain null files.");
        }

        var duplicateFilename = files
            .GroupBy(
                file => file.Filename,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateFilename is not null)
        {
            throw new DomainRuleException(
                $"Duplicate submitted filename: " +
                $"{duplicateFilename.Key}.");
        }

        return new PE_Submission(
            id.Trim(),
            questionId.Trim(),
            sessionId.Trim(),
            NormalizeOptionalIdentifier(courseId),
            mode,
            topicTags,
            files,
            languageId,
            maxScore,
            attemptCount,
            submittedAt);
    }

    public void BeginProcessing(
        DateTime utcNow)
    {
        BeginProcessing(
            "__legacy__",
            utcNow,
            utcNow.AddMinutes(5));
    }

    public void BeginProcessing(
        string leaseOwner,
        DateTime leaseAcquiredAt,
        DateTime leaseExpiresAt)
    {
        EnsureStatus(
            SubmissionProcessingStatus.Pending);

        ValidateLease(
            leaseOwner,
            leaseAcquiredAt,
            leaseExpiresAt);

        Status = SubmissionProcessingStatus.Processing;
        ProcessingStartedAt = leaseAcquiredAt;
        CompletedAt = null;
        FinalVerdict = null;
        ProcessingError = null;

        LeaseOwner = leaseOwner.Trim();
        LeaseAcquiredAt = leaseAcquiredAt;
        LeaseExpiresAt = leaseExpiresAt;

        Execution.StartAttempt(leaseAcquiredAt);
    }

    public void BeginAdditionalExecutionAttempt(
        string leaseOwner,
        DateTime utcNow)
    {
        EnsureStatus(
            SubmissionProcessingStatus.Processing);

        EnsureLeaseOwnedBy(
            leaseOwner,
            utcNow);

        EnsureUtc(utcNow);

        if (Execution.HasTokens)
        {
            throw new DomainRuleException(
                "Cannot start another attempt while " +
                "execution tokens exist.");
        }

        Execution.StartAttempt(utcNow);
        ProcessingError = null;
    }

    [Obsolete(
        "Use the lease-aware case-indexed AttachExecutionTokens overload " +
        "that requires leaseOwner and utcNow.")]
    public void AttachExecutionTokens(
        IEnumerable<string> tokens)
    {
        throw new DomainRuleException(
            "Use the lease-aware case-indexed AttachExecutionTokens " +
            "overload with leaseOwner and utcNow.");
    }

    public void AttachExecutionTokens(
        IEnumerable<ExecutionCaseTracking> executionCases,
        string leaseOwner,
        DateTime utcNow)
    {
        EnsureLeaseOwnedBy(
            leaseOwner,
            utcNow);

        EnsureStatus(
            SubmissionProcessingStatus.Processing);

        Execution.SetCases(executionCases);
    }

    public void RegisterTransientExecutionFailure(
        string leaseOwner,
        DateTime utcNow,
        string error)
    {
        EnsureStatus(
            SubmissionProcessingStatus.Processing);

        EnsureLeaseOwnedBy(
            leaseOwner,
            utcNow);

        if (string.IsNullOrWhiteSpace(error))
        {
            throw new DomainRuleException(
                "Execution error is required.");
        }

        Execution.RegisterFailure(error);
        ProcessingError = error.Trim();
    }

    public void Complete(
        string leaseOwner,
        DateTime utcNow,
        SubmissionEvaluation evaluation)
    {
        EnsureStatus(
            SubmissionProcessingStatus.Processing);

        EnsureLeaseOwnedBy(
            leaseOwner,
            utcNow);

        EnsureUtc(utcNow);

        ArgumentNullException.ThrowIfNull(evaluation);

        if (ProcessingStartedAt.HasValue &&
            utcNow < ProcessingStartedAt.Value)
        {
            throw new DomainRuleException(
                "CompletedAt cannot be earlier than " +
                "ProcessingStartedAt.");
        }

        if (evaluation.TestResults.Count == 0)
        {
            throw new DomainRuleException(
                "Completed submission must contain test results.");
        }

        if (evaluation.Score < 0 ||
            evaluation.Score > MaxScore)
        {
            throw new DomainRuleException(
                $"Submission score must be between " +
                $"0 and {MaxScore}.");
        }

        var orderedResults = evaluation.TestResults
            .OrderBy(result => result.TestCaseIndex)
            .ToList();

        var duplicateIndex = orderedResults
            .GroupBy(result => result.TestCaseIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateIndex is not null)
        {
            throw new DomainRuleException(
                $"Duplicate test case index: " +
                $"{duplicateIndex.Key}.");
        }

        var passedCount = orderedResults.Count(
            result => result.IsPassed);

        if (evaluation.PassedTestCases != passedCount)
        {
            throw new DomainRuleException(
                "Evaluation passed count is inconsistent.");
        }

        if (evaluation.TotalTestCases !=
            orderedResults.Count)
        {
            throw new DomainRuleException(
                "Evaluation total test count is inconsistent.");
        }

        if (evaluation.FinalVerdict ==
                SubmissionVerdict.Accepted &&
            passedCount != orderedResults.Count)
        {
            throw new DomainRuleException(
                "Accepted submission must pass every test case.");
        }

        _testResultItems.Clear();
        _testResultItems.AddRange(orderedResults);

        TestCasesPassed = passedCount;
        TotalTestCases = orderedResults.Count;

        QuestionScore = Math.Round(
            evaluation.Score,
            2,
            MidpointRounding.AwayFromZero);

        RuntimeMs = orderedResults.Max(
            result => result.RuntimeMs);

        MemoryKb = orderedResults.Max(
            result => result.MemoryKb);

        FinalVerdict = evaluation.FinalVerdict;
        Status = SubmissionProcessingStatus.Completed;
        CompletedAt = utcNow;
        ProcessingError = null;

        Execution.ClearTokens();
        ClearLease();
    }

    public void MarkProcessingFailed(
        string leaseOwner,
        DateTime utcNow,
        string error)
    {
        EnsureStatus(
            SubmissionProcessingStatus.Processing);

        EnsureLeaseOwnedBy(
            leaseOwner,
            utcNow);

        if (string.IsNullOrWhiteSpace(error))
        {
            throw new DomainRuleException(
                "Processing error is required.");
        }

        EnsureUtc(utcNow);

        if (ProcessingStartedAt.HasValue &&
            utcNow < ProcessingStartedAt.Value)
        {
            throw new DomainRuleException(
                "FailedAt cannot be earlier than " +
                "ProcessingStartedAt.");
        }

        Status = SubmissionProcessingStatus.Failed;
        FinalVerdict = SubmissionVerdict.SystemError;
        ProcessingError = error.Trim();
        QuestionScore = 0;
        CompletedAt = utcNow;

        Execution.RegisterFailure(error);
        Execution.ClearTokens();
        ClearLease();
    }

    public void ResetForManualRetry()
    {
        EnsureStatus(
            SubmissionProcessingStatus.Failed);

        Status = SubmissionProcessingStatus.Pending;
        FinalVerdict = null;
        ProcessingError = null;
        ProcessingStartedAt = null;
        CompletedAt = null;
        ClearLease();

        _testResultItems.Clear();

        TestCasesPassed = 0;
        TotalTestCases = 0;
        QuestionScore = 0;
        RuntimeMs = 0;
        MemoryKb = 0;

        Execution.Reset();
    }

    public void RenewLease(
        string leaseOwner,
        DateTime newLeaseExpiresAt,
        DateTime utcNow)
    {
        EnsureStatus(
            SubmissionProcessingStatus.Processing);

        EnsureLeaseOwnedBy(
            leaseOwner,
            utcNow);

        EnsureUtc(newLeaseExpiresAt);
        EnsureUtc(utcNow);

        if (newLeaseExpiresAt <= utcNow)
        {
            throw new DomainRuleException(
                "Lease expiry must be in the future.");
        }

        if (!LeaseAcquiredAt.HasValue)
        {
            throw new DomainRuleException(
                "Active lease acquisition time is missing.");
        }

        if (newLeaseExpiresAt <= LeaseAcquiredAt.Value)
        {
            throw new DomainRuleException(
                "Lease expiry must be later than lease acquisition.");
        }

        LeaseExpiresAt = newLeaseExpiresAt;
    }

    public bool HasValidLease(
        string leaseOwner,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (Status != SubmissionProcessingStatus.Processing)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(LeaseOwner) ||
            !LeaseAcquiredAt.HasValue ||
            !LeaseExpiresAt.HasValue)
        {
            return false;
        }

        return string.Equals(
                   LeaseOwner,
                   leaseOwner.Trim(),
                   StringComparison.Ordinal) &&
               LeaseExpiresAt.Value > utcNow;
    }

    public void EnsureLeaseOwnedBy(
        string leaseOwner,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new DomainRuleException(
                "Lease owner is required.");
        }

        if (!HasValidLease(
                leaseOwner,
                utcNow))
        {
            throw new DomainRuleException(
                "Submission does not have a valid lease for the specified owner.");
        }
    }

    private void EnsureStatus(
        SubmissionProcessingStatus expectedStatus)
    {
        if (Status == expectedStatus)
        {
            return;
        }

        throw new DomainRuleException(
            $"Invalid submission state transition. " +
            $"Expected {expectedStatus}, " +
            $"current status is {Status}.");
    }

    private static void ValidateRequiredIdentifier(
        string value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleException(
                $"{fieldName} is required.");
        }
    }

    private static string? NormalizeOptionalIdentifier(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void EnsureUtc(
        DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainRuleException(
                "DateTime value must use UTC.");
        }
    }

    private static void ValidateLease(
        string leaseOwner,
        DateTime leaseAcquiredAt,
        DateTime leaseExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new DomainRuleException(
                "Lease owner is required.");
        }

        EnsureUtc(leaseAcquiredAt);
        EnsureUtc(leaseExpiresAt);

        if (leaseExpiresAt <= leaseAcquiredAt)
        {
            throw new DomainRuleException(
                "Lease expiry must be later than lease acquisition.");
        }
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseAcquiredAt = null;
        LeaseExpiresAt = null;
    }
}

public sealed class TestResultItem
{
    private TestResultItem()
    {
    }

    private TestResultItem(
        int testCaseIndex,
        SubmissionVerdict verdict,
        string? input,
        string? actualOutput,
        string? expectedOutput,
        string? standardError,
        string? compileOutput,
        int runtimeMs,
        int memoryKb,
        bool isHidden)
    {
        TestCaseIndex = testCaseIndex;
        Verdict = verdict;
        Input = input;
        ActualOutput = actualOutput;
        ExpectedOutput = expectedOutput;
        StandardError = standardError;
        CompileOutput = compileOutput;
        RuntimeMs = runtimeMs;
        MemoryKb = memoryKb;
        IsHidden = isHidden;
    }

    public int TestCaseIndex { get; private set; }

    public SubmissionVerdict Verdict { get; private set; }

    public string? Input { get; private set; }

    public string? ActualOutput { get; private set; }

    public string? ExpectedOutput { get; private set; }

    public string? StandardError { get; private set; }

    public string? CompileOutput { get; private set; }

    public int RuntimeMs { get; private set; }

    public int MemoryKb { get; private set; }

    public bool IsHidden { get; private set; }

    [BsonIgnore]
    public bool IsPassed =>
        Verdict == SubmissionVerdict.Accepted;

    public static TestResultItem Create(
        int testCaseIndex,
        SubmissionVerdict verdict,
        TestCase testCase,
        string? actualOutput,
        string? standardError,
        string? compileOutput,
        int runtimeMs,
        int memoryKb)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        if (testCaseIndex < 0)
        {
            throw new DomainRuleException(
                "Test case index cannot be negative.");
        }

        if (runtimeMs < 0)
        {
            throw new DomainRuleException(
                "Runtime cannot be negative.");
        }

        if (memoryKb < 0)
        {
            throw new DomainRuleException(
                "Memory usage cannot be negative.");
        }

        var isHidden = testCase.IsHidden;

        return new TestResultItem(
            testCaseIndex,
            verdict,
            isHidden
                ? null
                : testCase.Input,
            actualOutput,
            isHidden
                ? null
                : testCase.ExpectedOutput,
            isHidden
                ? null
                : standardError,
            compileOutput,
            runtimeMs,
            memoryKb,
            isHidden);
    }
}

public sealed class ExecutionTracking
{
    [BsonElement("Tokens")]
    private List<string> _legacyTokens = [];

    [BsonElement("ExecutionCases")]
    private List<ExecutionCaseTracking> _executionCases = [];

    private ExecutionTracking()
    {
    }

    [BsonIgnore]
    public IReadOnlyCollection<string> Tokens =>
        // During the transition, ExecutionCases wins when present.
        _executionCases.Count > 0
            ? _executionCases
                .OrderBy(item => item.CaseIndex)
                .Select(item => item.ProviderToken)
                .ToList()
                .AsReadOnly()
            : _legacyTokens.AsReadOnly();

    [BsonIgnore]
    public IReadOnlyCollection<ExecutionCaseTracking> Cases =>
        _executionCases
            .OrderBy(item => item.CaseIndex)
            .ToList()
            .AsReadOnly();

    public int AttemptCount { get; private set; }

    public DateTime? LastAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    [BsonIgnore]
    public bool HasTokens =>
        _executionCases.Count > 0 ||
        _legacyTokens.Count > 0;

    public static ExecutionTracking Create()
    {
        return new ExecutionTracking();
    }

    public void StartAttempt(
        DateTime utcNow)
    {
        EnsureUtc(utcNow);

        AttemptCount++;
        LastAttemptAt = utcNow;
        LastError = null;
    }

    public void SetTokens(
        IEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var indexedTokens = tokens
            .Select((token, index) =>
                ExecutionCaseTracking.Create(
                    index,
                    token))
            .ToList();

        SetCases(indexedTokens);
    }

    public void SetCases(
        IEnumerable<ExecutionCaseTracking> executionCases)
    {
        ArgumentNullException.ThrowIfNull(executionCases);

        var cases = executionCases
            .Select(item => item ?? throw new DomainRuleException(
                "Execution cases cannot contain null items."))
            .OrderBy(item => item.CaseIndex)
            .ToList();

        if (cases.Count == 0)
        {
            throw new DomainRuleException(
                "At least one execution token is required.");
        }

        var duplicateCaseIndex = cases
            .GroupBy(item => item.CaseIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCaseIndex is not null)
        {
            throw new DomainRuleException(
                $"Duplicate execution case index: {duplicateCaseIndex.Key}.");
        }

        var duplicateToken = cases
            .GroupBy(
                item => item.ProviderToken,
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateToken is not null)
        {
            throw new DomainRuleException(
                $"Duplicate execution provider token: {duplicateToken.Key}.");
        }

        _executionCases.Clear();
        _executionCases.AddRange(cases);
        _legacyTokens.Clear();
    }

    public void RegisterFailure(
        string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new DomainRuleException(
                "Execution error is required.");
        }

        LastError = error.Trim();
    }

    public void ClearTokens()
    {
        _executionCases.Clear();
        _legacyTokens.Clear();
    }

    public void Reset()
    {
        _executionCases.Clear();
        _legacyTokens.Clear();
        AttemptCount = 0;
        LastAttemptAt = null;
        LastError = null;
    }

    private static void EnsureUtc(
        DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainRuleException(
                "DateTime value must use UTC.");
        }
    }
}

public sealed class ExecutionCaseTracking
{
    private ExecutionCaseTracking()
    {
    }

    private ExecutionCaseTracking(
        int caseIndex,
        string providerToken)
    {
        CaseIndex = caseIndex;
        ProviderToken = providerToken;
    }

    public int CaseIndex { get; private set; }

    public string ProviderToken { get; private set; } =
        string.Empty;

    public static ExecutionCaseTracking Create(
        int caseIndex,
        string providerToken)
    {
        if (caseIndex < 0)
        {
            throw new DomainRuleException(
                "Case index cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(providerToken))
        {
            throw new DomainRuleException(
                "Provider token is required.");
        }

        return new ExecutionCaseTracking(
            caseIndex,
            providerToken.Trim());
    }
}

public sealed class SubmissionEvaluation
{
    private readonly List<TestResultItem> _testResults;

    private SubmissionEvaluation(
        IEnumerable<TestResultItem> testResults,
        double score,
        SubmissionVerdict finalVerdict,
        int passedTestCases,
        int totalTestCases)
    {
        _testResults = testResults.ToList();
        Score = score;
        FinalVerdict = finalVerdict;
        PassedTestCases = passedTestCases;
        TotalTestCases = totalTestCases;
    }

    public IReadOnlyCollection<TestResultItem> TestResults =>
        _testResults.AsReadOnly();

    public double Score { get; }

    public SubmissionVerdict FinalVerdict { get; }

    public int PassedTestCases { get; }

    public int TotalTestCases { get; }

    public static SubmissionEvaluation Create(
        IEnumerable<TestResultItem> testResults,
        double score,
        SubmissionVerdict finalVerdict,
        int passedTestCases,
        int totalTestCases)
    {
        ArgumentNullException.ThrowIfNull(testResults);

        var results = testResults.ToList();

        if (results.Count == 0)
        {
            throw new DomainRuleException(
                "Submission evaluation requires test results.");
        }

        if (score < 0)
        {
            throw new DomainRuleException(
                "Submission score cannot be negative.");
        }

        if (passedTestCases < 0)
        {
            throw new DomainRuleException(
                "Passed test count cannot be negative.");
        }

        if (totalTestCases <= 0)
        {
            throw new DomainRuleException(
                "Total test count must be greater than zero.");
        }

        if (passedTestCases > totalTestCases)
        {
            throw new DomainRuleException(
                "Passed test count cannot exceed total test count.");
        }

        if (totalTestCases != results.Count)
        {
            throw new DomainRuleException(
                "Total test count must match the result count.");
        }

        var actualPassedCount =
            results.Count(result => result.IsPassed);

        if (passedTestCases != actualPassedCount)
        {
            throw new DomainRuleException(
                "Passed test count must match " +
                "the result collection.");
        }

        if (finalVerdict == SubmissionVerdict.Accepted &&
            passedTestCases != totalTestCases)
        {
            throw new DomainRuleException(
                "Accepted evaluation must pass every test case.");
        }

        return new SubmissionEvaluation(
            results,
            score,
            finalVerdict,
            passedTestCases,
            totalTestCases);
    }
}
