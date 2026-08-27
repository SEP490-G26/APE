# APE — Judge0 Workflow, Code Map và Setup

> Tài liệu này tổng hợp các phần liên quan đến workflow chấm bài PE bằng Judge0 trong backend APE (.NET 8, Clean Architecture, MongoDB).
>
> **Kiến trúc được chọn làm chuẩn sau merge:**
>
> `SubmissionController → IPESubmissionService → PE_Submission(Pending) → Judge0Worker → SubmissionGradingProcessor → ICodeExecutionClient/Judge0Client → Judge0 → SubmissionResultEvaluator → MongoDB`
>
> Không dùng đồng thời workflow cũ `Judge0Service + Channel<GradingJob>` với workflow mới bên trên.

---

## 1. Mục tiêu của workflow

Workflow cần đảm bảo:

1. Sinh viên gửi source code qua API.
2. Backend tạo `PE_Submission` ở trạng thái `Pending`.
3. Worker claim submission theo cách atomic để tránh hai worker xử lý cùng một bài.
4. Processor gửi toàn bộ test case thành một batch tới Judge0.
5. Backend lưu các execution token.
6. Processor polling kết quả cho đến khi tất cả test case hoàn tất.
7. Evaluator chuyển kết quả Judge0 thành verdict nội bộ.
8. Backend tính điểm và cập nhật submission.
9. Dữ liệu test case ẩn không được trả về frontend.
10. Có timeout, retry và logging để xử lý lỗi tạm thời.

---

## 2. Workflow tổng thể

```text
Student
  |
  | POST /api/student/submissions/pe
  v
SubmissionController
  |
  v
IPESubmissionService.SubmitAsync(...)
  |
  | Validate user/session/question/language/files
  | Create PE_Submission(Status = Pending)
  v
MongoDB: PE_Submissions
  |
  v
Judge0Worker
  |
  | GetByStatusesAsync(Pending)
  | TryClaimPendingAsync(...)
  v
PE_Submission(Status = Processing)
  |
  v
SubmissionGradingProcessor
  |
  | CreateBatchAsync(...)
  v
ICodeExecutionClient
  |
  v
Judge0Client
  |
  | POST /submissions/batch?base64_encoded=true
  v
Judge0
  |
  | Return execution tokens
  v
PE_Submission.Execution.Tokens
  |
  | Poll:
  | GET /submissions/batch?tokens=...
  v
SubmissionResultEvaluator
  |
  | Map status
  | Normalize output
  | Protect hidden test cases
  | Calculate score
  v
PE_Submission(Status = Completed/Failed)
  |
  v
Frontend polls GET /api/student/submissions/pe/{id}
```

---

## 3. Cấu trúc file nên có

```text
APE_Core/
├── API/
│   ├── Controllers/
│   │   └── SubmissionController.cs
│   ├── Workers/
│   │   └── Judge0Worker.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
│
├── Application/
│   ├── DependencyInjection.cs
│   ├── Interfaces/
│   │   ├── ICodeExecutionClient.cs
│   │   ├── IPESubmissionRepository.cs
│   │   ├── IPESubmissionService.cs
│   │   ├── ISubmissionGradingProcessor.cs
│   │   ├── ISubmissionResultEvaluator.cs
│   │   └── ISubmissionScoringService.cs
│   ├── Models/
│   │   ├── CodeExecutionBatchRequest.cs
│   │   ├── CodeExecutionBatchReceipt.cs
│   │   └── CodeExecutionResult.cs
│   ├── Options/
│   │   ├── GradingWorkerOptions.cs
│   │   └── GradingWorkerOptionsValidator.cs
│   ├── Services/
│   │   ├── PESubmissionService.cs
│   │   ├── SubmissionGradingProcessor.cs
│   │   ├── SubmissionResultEvaluator.cs
│   │   └── EqualWeightSubmissionScoringService.cs
│   └── Exceptions/
│       └── CodeExecutionClientException.cs
│
├── Domain/
│   ├── Entities/
│   │   ├── CommonTypes.cs
│   │   ├── PEQuestion.cs
│   │   └── PE_Submission.cs
│   └── Enums/
│       ├── SubmissionProcessingStatus.cs
│       └── SubmissionVerdict.cs
│
└── Infrastructure/
    ├── Judge0/
    │   ├── Contracts/
    │   │   ├── Judge0CreateBatchRequest.cs
    │   │   ├── Judge0CreateSubmissionResponse.cs
    │   │   ├── Judge0BatchResultResponse.cs
    │   │   ├── Judge0SubmissionRequest.cs
    │   │   ├── Judge0SubmissionResult.cs
    │   │   ├── Judge0StatusResponse.cs
    │   │   ├── NullableDoubleJsonConverter.cs
    │   │   └── NullableIntJsonConverter.cs
    │   ├── IJudge0MultiFileProfileResolver.cs
    │   ├── IJudge0SourcePackageBuilder.cs
    │   ├── Judge0Client.cs
    │   ├── Judge0DependencyInjection.cs
    │   ├── Judge0MultiFileProfile.cs
    │   ├── Judge0MultiFileProfileResolver.cs
    │   ├── Judge0Options.cs
    │   ├── Judge0OptionsValidator.cs
    │   ├── Judge0SourcePackage.cs
    │   └── Judge0SourcePackageBuilder.cs
    └── Persistence/
        └── PESubmissionRepository.cs
```

---

# PHẦN A — DOMAIN

## 4. `CommonTypes.cs`

Judge0 workflow phụ thuộc trực tiếp vào `CodeFile` và `TestCase`.

```csharp
using Domain.Exceptions;

namespace Domain.Entities;

public sealed class CodeFile
{
    public string Filename { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public bool IsReadOnly { get; private set; }

    private CodeFile()
    {
    }

    public static CodeFile Create(
        string filename,
        string content,
        bool isReadOnly = false)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new DomainRuleException("Filename is required.");

        if (content is null)
            throw new DomainRuleException("File content is required.");

        return new CodeFile
        {
            Filename = filename.Trim(),
            Content = content,
            IsReadOnly = isReadOnly
        };
    }
}

public sealed class TestCase
{
    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public bool IsHidden { get; set; } = true;

    public bool IsSample { get; set; }

    public int TimeLimitMs { get; set; } = 2000;

    public int MemoryLimitKb { get; set; } = 256000;
}

public sealed class Hint
{
    public int Level { get; private set; }

    public string Text { get; private set; } = string.Empty;

    private Hint()
    {
    }

    public static Hint Create(int level, string text)
    {
        if (level <= 0)
            throw new DomainRuleException(
                "Hint level must be greater than zero.");

        if (string.IsNullOrWhiteSpace(text))
            throw new DomainRuleException("Hint text is required.");

        return new Hint
        {
            Level = level,
            Text = text.Trim()
        };
    }
}
```

### Quy ước đơn vị

```text
TestCase.TimeLimitMs   = milliseconds
TestCase.MemoryLimitKb = kilobytes

Judge0 cpu_time_limit  = seconds
Judge0 wall_time_limit = seconds
Judge0 memory_limit    = kilobytes
```

Khi gửi Judge0:

```csharp
var cpuTimeLimitSeconds = testCase.TimeLimitMs / 1000d;
```

Không giữ lại các field merge cũ:

```csharp
MaxExecutionTime
MaxMemory
```

---

## 5. `PEQuestion.cs`

Các property Judge0 cần:

```csharp
public List<CodeFile> SkeletonCode { get; set; } = [];

public List<CodeFile> SolutionCode { get; set; } = [];

public List<TestCase> TestCases { get; set; } = [];

public List<int> AllowedLanguageIds { get; set; } = [50];

public int DefaultLanguageId { get; set; } = 50;
```

Method resolve language:

```csharp
public int ResolveLanguageId(int? requestedLanguageId)
{
    var allowedLanguages = AllowedLanguageIds
        .Where(id => id > 0)
        .Distinct()
        .ToList();

    if (allowedLanguages.Count == 0)
    {
        throw new DomainRuleException(
            "Question has no configured programming language.");
    }

    if (allowedLanguages.Count == 1)
        return allowedLanguages[0];

    if (!requestedLanguageId.HasValue)
    {
        if (DefaultLanguageId <= 0 ||
            !allowedLanguages.Contains(DefaultLanguageId))
        {
            throw new DomainRuleException(
                "Default language configuration is invalid.");
        }

        return DefaultLanguageId;
    }

    if (!allowedLanguages.Contains(requestedLanguageId.Value))
    {
        throw new DomainRuleException(
            $"LanguageId {requestedLanguageId.Value} " +
            "is not allowed for this question.");
    }

    return requestedLanguageId.Value;
}
```

### Mapping hiện tại của APE

```text
C    → Judge0 LanguageId 50
Java → Judge0 LanguageId 62
```

Không hard-code `50` trong worker. `LanguageId` phải được resolve khi tạo submission và lưu trong `PE_Submission`.

---

## 6. Trạng thái và verdict

### `SubmissionProcessingStatus.cs`

```csharp
namespace Domain.Enums;

public enum SubmissionProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
```

### `SubmissionVerdict.cs`

```csharp
namespace Domain.Enums;

public enum SubmissionVerdict
{
    Accepted = 0,
    WrongAnswer = 1,
    TimeLimitExceeded = 2,
    CompilationError = 3,
    RuntimeError = 4,
    SystemError = 5
}
```

### State transition hợp lệ

```text
Pending → Processing → Completed
Pending → Processing → Failed
Failed  → Pending       (manual retry)
```

Không dùng lẫn các string cũ:

```text
"Pending"
"Grading"
"Passed"
"Failed"
```

với enum mới trong cùng codebase.

---

## 7. `PE_Submission.cs`

Entity chuẩn mới cần giữ các phần liên quan đến execution:

```csharp
public string Id { get; private set; } = string.Empty;

public string QuestionId { get; private set; } = string.Empty;

public string SessionId { get; private set; } = string.Empty;

public string? CourseId { get; private set; }

public IReadOnlyCollection<CodeFile> SubmittedCode =>
    _submittedCode.AsReadOnly();

public int LanguageId { get; private set; }

public double MaxScore { get; private set; }

public DateTime SubmittedAt { get; private set; }

public DateTime? ProcessingStartedAt { get; private set; }

public DateTime? CompletedAt { get; private set; }

public SubmissionProcessingStatus Status { get; private set; }

public SubmissionVerdict? FinalVerdict { get; private set; }

public int TestCasesPassed { get; private set; }

public int TotalTestCases { get; private set; }

public double QuestionScore { get; private set; }

public int RuntimeMs { get; private set; }

public int MemoryKb { get; private set; }

public int AttemptCount { get; private set; }

public ExecutionTracking Execution { get; private set; } = null!;

public IReadOnlyCollection<TestResultItem> TestResultItems =>
    _testResultItems.AsReadOnly();

public string? ProcessingError { get; private set; }
```

Các domain method quan trọng:

```csharp
public void BeginProcessing(DateTime utcNow);

public void AttachExecutionTokens(IEnumerable<string> tokens);

public void RegisterTransientExecutionFailure(string error);

public void BeginAdditionalExecutionAttempt(DateTime utcNow);

public void Complete(
    SubmissionEvaluation evaluation,
    DateTime completedAt);

public void MarkProcessingFailed(
    string error,
    DateTime failedAt);

public void ResetForManualRetry();
```

### Dữ liệu execution nên được lưu

```csharp
public sealed class ExecutionTracking
{
    public int AttemptCount { get; private set; }

    public DateTime? LastAttemptAt { get; private set; }

    public IReadOnlyCollection<string> Tokens =>
        _tokens.AsReadOnly();

    public string? LastError { get; private set; }

    public bool HasTokens => _tokens.Count > 0;
}
```

Lưu token vào MongoDB giúp worker có thể tiếp tục polling sau khi API restart.

---

## 8. Bảo vệ hidden test case

`TestResultItem.Create(...)` phải ẩn input/output của test case bí mật:

```csharp
var isHidden = testCase.IsHidden;

return new TestResultItem(
    testCaseIndex,
    verdict,
    isHidden ? null : testCase.Input,
    isHidden ? null : actualOutput,
    isHidden ? null : testCase.ExpectedOutput,
    standardError,
    compileOutput,
    runtimeMs,
    memoryKb,
    isHidden);
```

Frontend không được nhận:

```text
Hidden test input
Hidden expected output
Hidden actual output
Solution code
```

---

# PHẦN B — APPLICATION

## 9. `ICodeExecutionClient.cs`

Application không phụ thuộc trực tiếp vào class `Judge0Client`.

```csharp
using Application.Models;

namespace Application.Interfaces;

public interface ICodeExecutionClient
{
    Task<CodeExecutionBatchReceipt> CreateBatchAsync(
        CodeExecutionBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodeExecutionResult>> GetBatchResultsAsync(
        IReadOnlyCollection<string> tokens,
        CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default);
}
```

Lợi ích:

- Application không biết HTTP endpoint của Judge0.
- Có thể mock khi unit test.
- Có thể thay Judge0 bằng execution engine khác.
- Không đưa code Infrastructure vào Application.

---

## 10. Execution models

### `CodeExecutionBatchRequest.cs`

```csharp
using Domain.Entities;
using Domain.Exceptions;

namespace Application.Models;

public sealed class CodeExecutionBatchRequest
{
    private readonly List<CodeFile> _sourceFiles;
    private readonly List<TestCase> _testCases;

    private CodeExecutionBatchRequest(
        int languageId,
        IEnumerable<CodeFile> sourceFiles,
        IEnumerable<TestCase> testCases)
    {
        LanguageId = languageId;
        _sourceFiles = sourceFiles.ToList();
        _testCases = testCases.ToList();
    }

    public int LanguageId { get; }

    public IReadOnlyList<CodeFile> SourceFiles => _sourceFiles;

    public IReadOnlyList<TestCase> TestCases => _testCases;

    public bool IsMultiFile => _sourceFiles.Count > 1;

    public static CodeExecutionBatchRequest Create(
        int languageId,
        IEnumerable<CodeFile> sourceFiles,
        IEnumerable<TestCase> testCases)
    {
        if (languageId <= 0)
        {
            throw new DomainRuleException(
                "LanguageId must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(testCases);

        var files = sourceFiles.ToList();
        var cases = testCases.ToList();

        if (files.Count == 0)
        {
            throw new DomainRuleException(
                "At least one source file is required.");
        }

        if (cases.Count == 0)
        {
            throw new DomainRuleException(
                "At least one test case is required.");
        }

        var duplicateFile = files
            .GroupBy(
                file => file.Filename,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateFile is not null)
        {
            throw new DomainRuleException(
                $"Duplicate source filename: {duplicateFile.Key}.");
        }

        return new CodeExecutionBatchRequest(
            languageId,
            files,
            cases);
    }
}
```

### `CodeExecutionBatchReceipt.cs`

```csharp
namespace Application.Models;

public sealed record CodeExecutionBatchReceipt(
    IReadOnlyList<string> Tokens);
```

### `CodeExecutionResult.cs`

```csharp
namespace Application.Models;

public sealed record CodeExecutionResult(
    string Token,
    int StatusId,
    string? StatusDescription,
    string? StandardOutput,
    string? StandardError,
    string? CompileOutput,
    string? Message,
    double? TimeSeconds,
    int? MemoryKb)
{
    public bool IsQueued => StatusId == 1;

    public bool IsProcessing => StatusId == 2;

    public bool IsFinished => StatusId > 2;
}
```

---

## 11. `GradingWorkerOptions.cs`

```csharp
namespace Application.Options;

public sealed class GradingWorkerOptions
{
    public const string SectionName = "GradingWorker";

    public int IdleDelaySeconds { get; init; } = 2;

    public int BatchSize { get; init; } = 10;

    public int MaximumExecutionAttempts { get; init; } = 3;

    public int InitialRetryDelaySeconds { get; init; } = 2;

    public int PollingIntervalSeconds { get; init; } = 2;

    public int MaximumPollingAttempts { get; init; } = 30;
}
```

### Ý nghĩa

| Field | Ý nghĩa |
|---|---|
| `IdleDelaySeconds` | Thời gian worker chờ khi không có bài |
| `BatchSize` | Số submission worker lấy mỗi vòng |
| `MaximumExecutionAttempts` | Số lần thử lại khi Judge0 lỗi tạm thời |
| `InitialRetryDelaySeconds` | Delay đầu tiên của exponential backoff |
| `PollingIntervalSeconds` | Khoảng cách giữa các lần lấy kết quả |
| `MaximumPollingAttempts` | Số vòng polling tối đa |

Với cấu hình mặc định:

```text
2 giây × 30 lần = khoảng 60 giây polling tối đa
```

---

## 12. `Application/DependencyInjection.cs`

```csharp
using Application.Interfaces;
using Application.Options;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GradingWorkerOptions>()
            .Bind(configuration.GetSection(
                GradingWorkerOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<
            IValidateOptions<GradingWorkerOptions>,
            GradingWorkerOptionsValidator>();

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<
            ISubmissionScoringService,
            EqualWeightSubmissionScoringService>();

        services.AddScoped<
            ISubmissionResultEvaluator,
            SubmissionResultEvaluator>();

        services.AddScoped<
            ISubmissionGradingProcessor,
            SubmissionGradingProcessor>();

        return services;
    }
}
```

Không đăng ký `ISubmissionGradingProcessor` lần thứ hai trong `Program.cs`.

---

## 13. `IPESubmissionRepository.cs`

```csharp
using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface IPESubmissionRepository
{
    Task CreateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default);

    Task<PE_Submission?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PE_Submission>> GetBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<int> GetLatestAttemptNumberAsync(
        string sessionId,
        string questionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PE_Submission>> GetByStatusesAsync(
        IReadOnlyCollection<SubmissionProcessingStatus> statuses,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PE_Submission?> TryClaimPendingAsync(
        string submissionId,
        DateTime processingStartedAt,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default);
}
```

`TryClaimPendingAsync` là phần quan trọng để chống duplicate processing.

---

## 14. Atomic claim trong `PESubmissionRepository`

Ý tưởng:

```csharp
public async Task<PE_Submission?> TryClaimPendingAsync(
    string submissionId,
    DateTime processingStartedAt,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(submissionId))
        return null;

    var filter =
        Builders<PE_Submission>.Filter.Eq(
            item => item.Id,
            submissionId.Trim()) &
        Builders<PE_Submission>.Filter.Eq(
            item => item.Status,
            SubmissionProcessingStatus.Pending);

    var update =
        Builders<PE_Submission>.Update
            .Set(
                item => item.Status,
                SubmissionProcessingStatus.Processing)
            .Set(
                item => item.ProcessingStartedAt,
                processingStartedAt)
            .Set(
                item => item.ProcessingError,
                null);

    var options = new FindOneAndUpdateOptions<
        PE_Submission,
        PE_Submission>
    {
        ReturnDocument = ReturnDocument.After
    };

    return await _collection.FindOneAndUpdateAsync(
        filter,
        update,
        options,
        cancellationToken);
}
```

### Lưu ý quan trọng

Entity hiện tại dùng private setter và domain method `BeginProcessing(...)`.

Nếu dùng Mongo update trực tiếp như trên, cần bảo đảm:

- Mongo serializer map được private setter; hoặc
- Repository dùng bản persistence model riêng; hoặc
- Atomic claim chỉ cập nhật field claim/version và sau đó hydrate entity.

Điều bắt buộc là filter phải chứa:

```text
Id == submissionId
AND Status == Pending
```

để chỉ một worker claim thành công.

---

## 15. Fix nullable cho MongoDB Driver

Với các method trả `Task<Entity?>`, dùng `async/await`:

```csharp
public async Task<PE_Submission?> GetByIdAsync(
    string id,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(id))
        return null;

    return await _collection
        .Find(item => item.Id == id.Trim())
        .FirstOrDefaultAsync(cancellationToken);
}
```

Cách này tránh cảnh báo:

```text
CS8619:
Task<PE_Submission> does not match Task<PE_Submission?>
```

---

## 16. `SubmissionGradingProcessor.cs`

Processor là orchestration chính.

```csharp
public sealed class SubmissionGradingProcessor
    : ISubmissionGradingProcessor
{
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
}
```

### `ProcessAsync`

```csharp
public async Task ProcessAsync(
    string submissionId,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(submissionId))
    {
        throw new ArgumentException(
            "SubmissionId is required.",
            nameof(submissionId));
    }

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

    if (submission.Status !=
        SubmissionProcessingStatus.Processing)
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
            "Programming question was not found.",
            cancellationToken);

        return;
    }

    if (question.TestCases.Count == 0)
    {
        await MarkFailedAsync(
            submission,
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
                cancellationToken);
        }

        if (!submission.Execution.HasTokens)
            return;

        var executionResults =
            await PollUntilCompletedAsync(
                submission,
                cancellationToken);

        var evaluation = _resultEvaluator.Evaluate(
            question,
            executionResults,
            submission.MaxScore);

        submission.Complete(
            evaluation,
            GetUtcNow());

        await _submissionRepository.UpdateAsync(
            submission,
            cancellationToken);
    }
    catch (CodeExecutionClientException exception)
    {
        await HandleExecutionFailureAsync(
            submission,
            exception,
            cancellationToken);
    }
    catch (DomainRuleException exception)
    {
        await MarkFailedAsync(
            submission,
            exception.Message,
            cancellationToken);
    }
}
```

### Tạo Judge0 batch

```csharp
private async Task CreateExecutionBatchAsync(
    PE_Submission submission,
    PEQuestion question,
    CancellationToken cancellationToken)
{
    var request = CodeExecutionBatchRequest.Create(
        submission.LanguageId,
        submission.SubmittedCode,
        question.TestCases);

    var receipt = await _executionClient.CreateBatchAsync(
        request,
        cancellationToken);

    submission.AttachExecutionTokens(receipt.Tokens);

    await _submissionRepository.UpdateAsync(
        submission,
        cancellationToken);
}
```

### Polling

```csharp
private async Task<IReadOnlyList<CodeExecutionResult>>
    PollUntilCompletedAsync(
        PE_Submission submission,
        CancellationToken cancellationToken)
{
    var transientFailureCount = 0;

    for (var attempt = 1;
         attempt <= _options.MaximumPollingAttempts;
         attempt++)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var results =
                await _executionClient.GetBatchResultsAsync(
                    submission.Execution.Tokens,
                    cancellationToken);

            if (results.Count > 0 &&
                results.All(result => result.IsFinished))
            {
                return results;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(
                    _options.PollingIntervalSeconds),
                cancellationToken);
        }
        catch (CodeExecutionClientException exception)
            when (exception.IsTransient)
        {
            transientFailureCount++;

            if (transientFailureCount >=
                _options.MaximumExecutionAttempts)
            {
                throw;
            }

            var delay =
                CalculateRetryDelay(transientFailureCount);

            await Task.Delay(delay, cancellationToken);
        }
    }

    throw new CodeExecutionClientException(
        "Judge0 polling limit was exceeded.",
        isTransient: true);
}
```

### Exponential backoff

```csharp
private TimeSpan CalculateRetryDelay(int attemptNumber)
{
    var exponent = Math.Max(attemptNumber - 1, 0);

    var multiplier = Math.Pow(2, exponent);

    var seconds =
        _options.InitialRetryDelaySeconds * multiplier;

    return TimeSpan.FromSeconds(
        Math.Min(seconds, 30));
}
```

---

## 17. Result evaluator

Judge0 status mapping chính:

| Judge0 `status_id` | Ý nghĩa | Verdict APE |
|---:|---|---|
| 1 | In Queue | Chưa hoàn tất |
| 2 | Processing | Chưa hoàn tất |
| 3 | Accepted | So sánh output để xác nhận AC/WA |
| 4 | Wrong Answer | `WrongAnswer` |
| 5 | Time Limit Exceeded | `TimeLimitExceeded` |
| 6 | Compilation Error | `CompilationError` |
| 7–12 | Runtime-related errors | `RuntimeError` |
| 13–14 | Internal/exec format errors | `SystemError` hoặc `RuntimeError` theo policy |

Evaluator nên dùng cả `status_id` và output.

### Normalize output

```csharp
private static string NormalizeOutput(string? output)
{
    if (string.IsNullOrEmpty(output))
        return string.Empty;

    var normalized = output
        .Replace("\r\n", "\n")
        .Replace('\r', '\n')
        .Split('\n')
        .Select(line => line.TrimEnd())
        .ToList();

    while (normalized.Count > 0 &&
           string.IsNullOrWhiteSpace(normalized[^1]))
    {
        normalized.RemoveAt(normalized.Count - 1);
    }

    return string.Join('\n', normalized);
}
```

Không dùng `Trim()` toàn bộ từng dòng vì có thể làm thay đổi output có ý nghĩa.

### Score equal-weight

```csharp
var score = totalTestCases == 0
    ? 0d
    : Math.Round(
        (double)passedTestCases / totalTestCases * maxScore,
        2,
        MidpointRounding.AwayFromZero);
```

---

# PHẦN C — INFRASTRUCTURE / JUDGE0

## 18. `Judge0Options.cs`

```csharp
namespace Infrastructure.Judge0;

public sealed class Judge0Options
{
    public const string SectionName = "Judge0";

    public string BaseUrl { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public int MaximumBatchSize { get; init; } = 100;

    public int MaximumSourceFiles { get; init; } = 20;

    public int MaximumArchiveBytes { get; init; } =
        5 * 1024 * 1024;

    public int MultiFileLanguageId { get; init; } = 89;

    public string? ApiKey { get; init; }

    public string ApiKeyHeaderName { get; init; } =
        "X-Auth-Token";

    public Dictionary<string, string> DefaultHeaders
        { get; init; } = [];
}
```

Retry và polling nằm trong `GradingWorkerOptions`, không để trùng trong `Judge0Options`.

---

## 19. `Judge0OptionsValidator.cs`

Các validation tối thiểu:

```csharp
public ValidateOptionsResult Validate(
    string? name,
    Judge0Options options)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        errors.Add("Judge0:BaseUrl is required.");
    }
    else if (!Uri.TryCreate(
                 options.BaseUrl,
                 UriKind.Absolute,
                 out var baseUri) ||
             baseUri.Scheme is not ("http" or "https"))
    {
        errors.Add(
            "Judge0:BaseUrl must be an absolute HTTP or HTTPS URL.");
    }

    if (options.TimeoutSeconds <= 0)
    {
        errors.Add(
            "Judge0:TimeoutSeconds must be greater than zero.");
    }

    if (options.MaximumBatchSize <= 0)
    {
        errors.Add(
            "Judge0:MaximumBatchSize must be greater than zero.");
    }

    if (options.MaximumSourceFiles <= 0)
    {
        errors.Add(
            "Judge0:MaximumSourceFiles must be greater than zero.");
    }

    if (options.MaximumArchiveBytes <= 0)
    {
        errors.Add(
            "Judge0:MaximumArchiveBytes must be greater than zero.");
    }

    if (options.MultiFileLanguageId <= 0)
    {
        errors.Add(
            "Judge0:MultiFileLanguageId must be greater than zero.");
    }

    if (!string.IsNullOrWhiteSpace(options.ApiKey) &&
        string.IsNullOrWhiteSpace(options.ApiKeyHeaderName))
    {
        errors.Add(
            "Judge0:ApiKeyHeaderName is required " +
            "when ApiKey is configured.");
    }

    return errors.Count == 0
        ? ValidateOptionsResult.Success
        : ValidateOptionsResult.Fail(errors);
}
```

---

## 20. Judge0 HTTP contracts

### Create batch request

```csharp
internal sealed class Judge0CreateBatchRequest
{
    [JsonPropertyName("submissions")]
    public IReadOnlyList<Judge0SubmissionRequest> Submissions
        { get; init; } = [];
}
```

### Submission request

```csharp
internal sealed class Judge0SubmissionRequest
{
    [JsonPropertyName("language_id")]
    public int LanguageId { get; init; }

    [JsonPropertyName("source_code")]
    public string? SourceCode { get; init; }

    [JsonPropertyName("additional_files")]
    public string? AdditionalFiles { get; init; }

    [JsonPropertyName("stdin")]
    public string? StandardInput { get; init; }

    [JsonPropertyName("expected_output")]
    public string? ExpectedOutput { get; init; }

    [JsonPropertyName("cpu_time_limit")]
    public double CpuTimeLimitSeconds { get; init; }

    [JsonPropertyName("wall_time_limit")]
    public double WallTimeLimitSeconds { get; init; }

    [JsonPropertyName("memory_limit")]
    public int MemoryLimitKb { get; init; }
}
```

### Create response

Judge0 batch create trả về một JSON array, không phải object `{ tokens: [...] }`.

```csharp
internal sealed class Judge0CreateSubmissionResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ValidationErrors
        { get; init; }
}
```

### Batch result response

```csharp
internal sealed class Judge0BatchResultResponse
{
    [JsonPropertyName("submissions")]
    public List<Judge0SubmissionResult> Submissions
        { get; init; } = [];
}
```

### Submission result

```csharp
internal sealed class Judge0SubmissionResult
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("status_id")]
    public int StatusId { get; init; }

    [JsonPropertyName("status")]
    public Judge0StatusResponse? Status { get; init; }

    [JsonPropertyName("stdout")]
    public string? StandardOutput { get; init; }

    [JsonPropertyName("stderr")]
    public string? StandardError { get; init; }

    [JsonPropertyName("compile_output")]
    public string? CompileOutput { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("time")]
    [JsonConverter(typeof(NullableDoubleJsonConverter))]
    public double? TimeSeconds { get; init; }

    [JsonPropertyName("memory")]
    [JsonConverter(typeof(NullableIntJsonConverter))]
    public int? MemoryKb { get; init; }
}
```

Judge0 có thể trả `time` hoặc `memory` dưới dạng number, string hoặc null. Vì vậy cần custom JSON converters.

---

## 21. `Judge0Client.CreateBatchAsync`

```csharp
public async Task<CodeExecutionBatchReceipt> CreateBatchAsync(
    CodeExecutionBatchRequest request,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(request);

    if (request.TestCases.Count > _options.MaximumBatchSize)
    {
        throw new CodeExecutionClientException(
            $"The batch contains {request.TestCases.Count} test cases. " +
            $"The maximum allowed is {_options.MaximumBatchSize}.",
            isTransient: false);
    }

    var sourcePackage = _packageBuilder.Build(
        request.LanguageId,
        request.SourceFiles);

    var submissions = request.TestCases
        .Select(testCase => new Judge0SubmissionRequest
        {
            LanguageId = sourcePackage.Judge0LanguageId,
            SourceCode = sourcePackage.SourceCodeBase64,
            AdditionalFiles =
                sourcePackage.AdditionalFilesBase64,
            StandardInput = EncodeBase64(testCase.Input),
            ExpectedOutput =
                EncodeBase64(testCase.ExpectedOutput),
            CpuTimeLimitSeconds =
                ConvertMillisecondsToSeconds(
                    testCase.TimeLimitMs),
            WallTimeLimitSeconds =
                CalculateWallTimeLimitSeconds(
                    testCase.TimeLimitMs),
            MemoryLimitKb = testCase.MemoryLimitKb
        })
        .ToList();

    var payload = new Judge0CreateBatchRequest
    {
        Submissions = submissions
    };

    using var requestMessage = new HttpRequestMessage(
        HttpMethod.Post,
        "submissions/batch?base64_encoded=true")
    {
        Content = JsonContent.Create(
            payload,
            options: JsonOptions)
    };

    using var response = await SendAsync(
        requestMessage,
        cancellationToken);

    var results = await DeserializeAsync<
        List<Judge0CreateSubmissionResponse>>(
        response,
        cancellationToken);

    if (results.Count != submissions.Count)
    {
        throw new CodeExecutionClientException(
            $"Judge0 returned {results.Count} batch items for " +
            $"{submissions.Count} submissions.",
            isTransient: true,
            statusCode: response.StatusCode);
    }

    var tokens = new List<string>(results.Count);

    for (var index = 0; index < results.Count; index++)
    {
        var result = results[index];

        if (string.IsNullOrWhiteSpace(result.Token))
        {
            var errors = result.ValidationErrors is null
                ? "Unknown validation error."
                : JsonSerializer.Serialize(
                    result.ValidationErrors,
                    JsonOptions);

            throw new CodeExecutionClientException(
                $"Judge0 rejected test case {index}: {errors}",
                isTransient: false,
                statusCode: response.StatusCode);
        }

        tokens.Add(result.Token);
    }

    return new CodeExecutionBatchReceipt(tokens);
}
```

### API endpoint

```http
POST /submissions/batch?base64_encoded=true
```

Payload:

```json
{
  "submissions": [
    {
      "language_id": 50,
      "source_code": "BASE64_SOURCE",
      "stdin": "BASE64_INPUT",
      "expected_output": "BASE64_EXPECTED_OUTPUT",
      "cpu_time_limit": 2,
      "wall_time_limit": 3,
      "memory_limit": 256000
    }
  ]
}
```

---

## 22. `Judge0Client.GetBatchResultsAsync`

```csharp
public async Task<IReadOnlyList<CodeExecutionResult>>
    GetBatchResultsAsync(
        IReadOnlyCollection<string> tokens,
        CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(tokens);

    var normalizedTokens = tokens
        .Where(token => !string.IsNullOrWhiteSpace(token))
        .Select(token => token.Trim())
        .ToList();

    if (normalizedTokens.Count == 0)
    {
        throw new CodeExecutionClientException(
            "At least one execution token is required.",
            isTransient: false);
    }

    if (normalizedTokens.Count != tokens.Count)
    {
        throw new CodeExecutionClientException(
            "Execution tokens cannot contain empty values.",
            isTransient: false);
    }

    if (normalizedTokens
        .Distinct(StringComparer.Ordinal)
        .Count() != normalizedTokens.Count)
    {
        throw new CodeExecutionClientException(
            "Execution tokens must be unique.",
            isTransient: false);
    }

    var tokenParameter = Uri.EscapeDataString(
        string.Join(',', normalizedTokens));

    var fields =
        "token,status_id,status,stdout,stderr," +
        "compile_output,message,time,memory";

    var fieldsParameter = Uri.EscapeDataString(fields);

    var requestUri =
        $"submissions/batch" +
        $"?tokens={tokenParameter}" +
        $"&base64_encoded=true" +
        $"&fields={fieldsParameter}";

    using var response = await _httpClient.GetAsync(
        requestUri,
        cancellationToken);

    // Deserialize, validate count/order and map to
    // IReadOnlyList<CodeExecutionResult>.
}
```

### API endpoint

```http
GET /submissions/batch
    ?tokens=token1,token2
    &base64_encoded=true
    &fields=token,status_id,status,stdout,stderr,compile_output,message,time,memory
```

---

## 23. Base64 encode/decode

```csharp
private static string EncodeBase64(string value)
{
    return Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value ?? string.Empty));
}

private static string? DecodeNullableBase64(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    try
    {
        return Encoding.UTF8.GetString(
            Convert.FromBase64String(value));
    }
    catch (FormatException exception)
    {
        throw new CodeExecutionClientException(
            "Judge0 returned invalid Base64 content.",
            isTransient: true,
            innerException: exception);
    }
}
```

Dùng Base64 tránh lỗi source code, Unicode và newline khi truyền JSON.

---

## 24. Single-file và multi-file

### Single file

```text
Original LanguageId → Judge0 trực tiếp
source_code         → Base64 file content
additional_files    → null
```

### Multi-file

```text
Original C/Java LanguageId
        |
        v
Judge0MultiFileProfileResolver
        |
        v
ZIP source files + compile script + run script
        |
        v
Judge0 Multi-file LanguageId = 89
        |
        v
additional_files = Base64 ZIP archive
```

### C profile

```bash
#!/usr/bin/env bash
set -euo pipefail

mapfile -d '' sources < <(
  find . -type f -name '*.c' -print0
)

if [ "${#sources[@]}" -eq 0 ]; then
  echo "No C source files were found." >&2
  exit 1
fi

gcc -O2 -std=c11 -Wall -Wextra \
  "${sources[@]}" \
  -o program
```

Run:

```bash
#!/usr/bin/env bash
set -euo pipefail

exec ./program
```

### Java profile

Compile:

```bash
#!/usr/bin/env bash
set -euo pipefail

mapfile -d '' sources < <(
  find . -type f -name '*.java' -print0
)

if [ "${#sources[@]}" -eq 0 ]; then
  echo "No Java source files were found." >&2
  exit 1
fi

javac "${sources[@]}"
```

Run:

```bash
#!/usr/bin/env bash
set -euo pipefail

exec java Main
```

Java multi-file yêu cầu:

```text
Main.java
```

---

## 25. `Judge0DependencyInjection.cs`

```csharp
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Judge0;

public static class Judge0DependencyInjection
{
    public static IServiceCollection AddJudge0(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<Judge0Options>()
            .Bind(configuration.GetSection(
                Judge0Options.SectionName))
            .ValidateOnStart();

        services.AddSingleton<
            IValidateOptions<Judge0Options>,
            Judge0OptionsValidator>();

        services.AddSingleton<
            IJudge0MultiFileProfileResolver,
            Judge0MultiFileProfileResolver>();

        services.AddSingleton<
            IJudge0SourcePackageBuilder,
            Judge0SourcePackageBuilder>();

        services.AddHttpClient<
            ICodeExecutionClient,
            Judge0Client>((serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<
                        IOptions<Judge0Options>>()
                    .Value;

                client.BaseAddress = new Uri(
                    options.BaseUrl.TrimEnd('/') + "/");

                client.Timeout = TimeSpan.FromSeconds(
                    options.TimeoutSeconds);

                client.DefaultRequestHeaders.Accept.ParseAdd(
                    "application/json");

                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    client.DefaultRequestHeaders
                        .TryAddWithoutValidation(
                            options.ApiKeyHeaderName,
                            options.ApiKey);
                }

                foreach (var header in options.DefaultHeaders)
                {
                    client.DefaultRequestHeaders
                        .TryAddWithoutValidation(
                            header.Key,
                            header.Value);
                }
            });

        return services;
    }
}
```

Không đăng ký thêm:

```csharp
AddHttpClient<Judge0Service>(...)
```

nếu đã sử dụng `AddJudge0(...)`.

---

# PHẦN D — API VÀ WORKER

## 26. `Judge0Worker.cs`

Worker chuẩn dùng polling MongoDB, không dùng in-memory Channel.

```csharp
public sealed class Judge0Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GradingWorkerOptions _options;
    private readonly TimeProvider _timeProvider;
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
        _logger = logger;
    }
}
```

### Main loop

```csharp
protected override async Task ExecuteAsync(
    CancellationToken stoppingToken)
{
    _logger.LogInformation(
        "Judge0 grading worker started.");

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
                exception,
                "Unhandled Judge0 worker loop error.");

            await Task.Delay(
                TimeSpan.FromSeconds(
                    _options.IdleDelaySeconds),
                stoppingToken);
        }
    }

    _logger.LogInformation(
        "Judge0 grading worker stopped.");
}
```

### Claim Pending + resume Processing

```csharp
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

        var claimed =
            await repository.TryClaimPendingAsync(
                pendingSubmission.Id,
                GetUtcNow(),
                cancellationToken);

        if (claimed is null)
            continue;

        await processor.ProcessAsync(
            claimed.Id,
            cancellationToken);

        processedCount++;
    }

    var remainingCapacity =
        _options.BatchSize - processedCount;

    if (remainingCapacity <= 0)
        return processedCount;

    var processingSubmissions =
        await repository.GetByStatusesAsync(
            [SubmissionProcessingStatus.Processing],
            remainingCapacity,
            cancellationToken);

    foreach (var submission in processingSubmissions)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await processor.ProcessAsync(
            submission.Id,
            cancellationToken);

        processedCount++;
    }

    return processedCount;
}
```

Việc lấy lại `Processing` submission cho phép tiếp tục sau khi API restart.

---

## 27. `SubmissionController`

Endpoint submit PE:

```csharp
[HttpPost("pe")]
public async Task<IActionResult> SubmitPE(
    [FromBody] PESubmissionInputDto request,
    CancellationToken cancellationToken)
{
    var studentId =
        User.FindFirstValue(
            ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(studentId))
    {
        return Unauthorized(
            ApiResponse.Fail("Unauthorized."));
    }

    var response =
        await _peSubmissionService.SubmitAsync(
            studentId,
            request,
            cancellationToken);

    if (!response.Success)
        return BadRequest(response);

    return Accepted(response);
}
```

Nên trả HTTP `202 Accepted` vì bài đang được xử lý nền.

Endpoint lấy kết quả:

```csharp
[HttpGet("pe/{id}")]
public async Task<IActionResult> GetPESubmission(
    string id,
    CancellationToken cancellationToken)
{
    var studentId = CurrentUserId;

    if (string.IsNullOrWhiteSpace(studentId))
        return Unauthorized(ApiResponse.Fail("Unauthorized."));

    var result = await _peSubmissionService.GetByIdAsync(
        studentId,
        id,
        cancellationToken);

    return result.Success
        ? Ok(result)
        : NotFound(result);
}
```

---

## 28. `Program.cs`

Chỉ giữ các đăng ký sau:

```csharp
using Application;
using Infrastructure.Judge0;

builder.Services.AddApplication(
    builder.Configuration);

builder.Services.AddJudge0(
    builder.Configuration);

builder.Services.AddScoped<
    IPESubmissionRepository,
    PESubmissionRepository>();

builder.Services.AddScoped<
    IPEQuestionRepository,
    PEQuestionRepository>();

builder.Services.AddScoped<
    IPESubmissionService,
    PESubmissionService>();

builder.Services.AddHostedService<
    API.Workers.Judge0Worker>();
```

### Chỉ đăng ký một lần

Tìm và xóa duplicate:

```csharp
builder.Services.AddHostedService<Judge0Worker>();
builder.Services.AddHostedService<API.Workers.Judge0Worker>();
```

Chỉ giữ một dòng.

Không giữ song song:

```csharp
builder.Services.AddJudge0(builder.Configuration);
```

và:

```csharp
builder.Services.Configure<Judge0Options>(...);
builder.Services.AddHttpClient<Judge0Service>(...);
```

---

# PHẦN E — CONFIGURATION

## 29. `appsettings.json`

Phiên bản sạch, không chứa merge marker:

```json
{
  "Judge0": {
    "BaseUrl": "http://localhost:2358",
    "TimeoutSeconds": 30,
    "MaximumBatchSize": 100,
    "MaximumSourceFiles": 20,
    "MaximumArchiveBytes": 5242880,
    "MultiFileLanguageId": 89,
    "ApiKey": "",
    "ApiKeyHeaderName": "X-Auth-Token",
    "DefaultHeaders": {}
  },
  "GradingWorker": {
    "IdleDelaySeconds": 2,
    "BatchSize": 10,
    "MaximumExecutionAttempts": 3,
    "InitialRetryDelaySeconds": 2,
    "PollingIntervalSeconds": 2,
    "MaximumPollingAttempts": 30
  }
}
```

Không để các field workflow cũ:

```json
{
  "Judge0": {
    "PollingIntervalSeconds": 10,
    "MaxRetries": 1
  }
}
```

Retry/polling thuộc section `GradingWorker`.

---

## 30. `appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Infrastructure.Judge0": "Debug",
      "API.Workers.Judge0Worker": "Debug",
      "Application.Services.SubmissionGradingProcessor": "Debug"
    }
  },
  "Judge0": {
    "BaseUrl": "http://localhost:2358",
    "TimeoutSeconds": 30,
    "ApiKey": ""
  }
}
```

---

## 31. Environment variables

Có thể override cấu hình:

```powershell
$env:Judge0__BaseUrl = "http://localhost:2358"
$env:Judge0__TimeoutSeconds = "30"
$env:Judge0__ApiKey = ""
$env:GradingWorker__PollingIntervalSeconds = "2"
$env:GradingWorker__MaximumPollingAttempts = "30"
```

Dùng dấu `__` để biểu diễn nested configuration trong .NET.

Không commit API key thật vào Git.

---

# PHẦN F — LOCAL JUDGE0 SETUP VÀ SMOKE TEST

## 32. Kiểm tra prerequisite

```powershell
docker --version
docker compose version
```

Judge0 self-hosted phải expose cổng:

```text
http://localhost:2358
```

Sau khi có compose configuration của Judge0:

```powershell
docker compose up -d
docker compose ps
docker compose logs --tail 100
```

APE backend hiện chỉ kết nối tới Judge0 qua HTTP. Dockerfile của APE không tự tạo Judge0 server.

---

## 33. Kiểm tra Judge0 đang chạy

### PowerShell

```powershell
Invoke-RestMethod `
  -Method Get `
  -Uri "http://localhost:2358/languages"
```

Hoặc:

```powershell
Invoke-RestMethod `
  -Method Get `
  -Uri "http://localhost:2358/system_info"
```

Nếu nhận:

```text
No connection could be made because the target machine actively refused it
```

thì Judge0 chưa chạy hoặc chưa expose port `2358`.

---

## 34. Smoke test single submission

```powershell
$source = @'
#include <stdio.h>

int main(void)
{
    int a, b;
    scanf("%d %d", &a, &b);
    printf("%d\n", a + b);
    return 0;
}
'@

$body = @{
    language_id = 50
    source_code = $source
    stdin = "2 3"
    expected_output = "5"
    cpu_time_limit = 2
    memory_limit = 256000
} | ConvertTo-Json

$result = Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:2358/submissions?base64_encoded=false&wait=true" `
    -ContentType "application/json" `
    -Body $body

$result
```

Kết quả mong đợi:

```text
status.id = 3
stdout    = "5\n"
```

---

## 35. Smoke test batch

```powershell
$payload = @{
    submissions = @(
        @{
            language_id = 50
            source_code = @'
#include <stdio.h>
int main(void)
{
    int a, b;
    scanf("%d %d", &a, &b);
    printf("%d\n", a + b);
    return 0;
}
'@
            stdin = "2 3"
            expected_output = "5"
        },
        @{
            language_id = 50
            source_code = @'
#include <stdio.h>
int main(void)
{
    int a, b;
    scanf("%d %d", &a, &b);
    printf("%d\n", a + b);
    return 0;
}
'@
            stdin = "10 20"
            expected_output = "30"
        }
    )
} | ConvertTo-Json -Depth 5

$receipt = Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:2358/submissions/batch?base64_encoded=false" `
    -ContentType "application/json" `
    -Body $payload

$receipt
```

Batch create trả danh sách:

```json
[
  { "token": "..." },
  { "token": "..." }
]
```

Lấy kết quả:

```powershell
$tokens = ($receipt.token -join ",")

Invoke-RestMethod `
  -Method Get `
  -Uri "http://localhost:2358/submissions/batch?tokens=$tokens&base64_encoded=false&fields=token,status_id,status,stdout,stderr,compile_output,time,memory"
```

---

# PHẦN G — LOGGING, ERROR VÀ RELIABILITY

## 36. Log cần có

Worker:

```text
Judge0 grading worker started
Claimed submission {SubmissionId}
Processing submission {SubmissionId}
Judge0 batch created with {TokenCount} tokens
Judge0 polling attempt {Attempt}/{Maximum}
Submission completed with verdict and score
Submission failed
Judge0 grading worker stopped
```

Không log:

```text
Full hidden input
Hidden expected output
Student source code toàn bộ
API key
JWT
Mongo connection string
```

---

## 37. Phân loại lỗi

### Non-transient

Không retry:

```text
Invalid language id
No source file
No test case
Too many source files
Too many test cases
Invalid filename
Duplicate filename
Judge0 validation rejected payload
Unsupported multi-file language
```

### Transient

Có thể retry:

```text
HTTP 408
HTTP 429
HTTP 500–599
Network timeout
Connection reset
Temporary invalid/missing Judge0 result
Temporary Base64 response corruption
```

### Domain failure

Mark submission failed:

```text
Question not found
Question has no test cases
Invalid submission state transition
Invalid score/evaluation
```

---

## 38. Health check

`ICodeExecutionClient.IsHealthyAsync()` có thể gọi:

```http
GET /system_info
```

hoặc endpoint Judge0 phù hợp với deployment.

Ví dụ API health check:

```csharp
builder.Services
    .AddHealthChecks()
    .AddCheck<Judge0HealthCheck>("judge0");
```

Không để API báo healthy nếu database hoạt động nhưng Judge0 không kết nối được, nếu chấm PE là chức năng bắt buộc.

---

# PHẦN H — MERGE CLEANUP

## 39. Các implementation cũ cần loại bỏ

Sau merge có nhiều phiên bản Judge0 workflow.

### Loại bỏ workflow cũ

```text
Application.Services.Judge0Service
Judge0Service.SendSubmissionAsync(...)
Judge0Service.SendBatchAsync(...)
Channel<GradingJob>
GradingJob
GetPendingOrGradingAsync()
PE_Submission.Judge0Tokens kiểu property cũ
Status string "Grading"
```

khi đã sử dụng:

```text
ICodeExecutionClient
Judge0Client
SubmissionGradingProcessor
Judge0Worker polling MongoDB
ExecutionTracking
SubmissionProcessingStatus enum
```

### Không dùng cả hai worker

Sai:

```csharp
builder.Services.AddHostedService<Judge0Worker>();
builder.Services.AddHostedService<API.Workers.Judge0Worker>();
```

Đúng:

```csharp
builder.Services.AddHostedService<
    API.Workers.Judge0Worker>();
```

### Không dùng cả hai DI style

Sai:

```csharp
builder.Services.AddJudge0(builder.Configuration);

builder.Services.Configure<Judge0Options>(
    builder.Configuration.GetSection("Judge0"));

builder.Services.AddHttpClient<Judge0Service>(...);
```

Đúng:

```csharp
builder.Services.AddJudge0(
    builder.Configuration);
```

### Xóa merge markers

Tìm toàn solution:

```text
left-marker
middle-separator
right-marker
```

PowerShell:

```powershell
Get-ChildItem -Recurse -File |
    Select-String -Pattern '<<<<<<<|=======|>>>>>>>'
```

---

## 40. Source-of-truth sau merge

Giữ:

```text
Infrastructure.Judge0.Judge0Client
Application.Interfaces.ICodeExecutionClient
Application.Services.SubmissionGradingProcessor
API.Workers.Judge0Worker
Application.Options.GradingWorkerOptions
Domain.Enums.SubmissionProcessingStatus
Domain.Enums.SubmissionVerdict
PE_Submission.Execution
```

Không giữ code cũ cùng chức năng trong namespace khác.

---

# PHẦN I — BUILD VÀ TEST CHECKLIST

## 41. Build theo layer

```powershell
dotnet build .\Domain\Domain.csproj
dotnet build .\Application\Application.csproj
dotnet build .\Infrastructure\Infrastructure.csproj
dotnet build .\API\API.csproj
dotnet build .\APE_Core.sln
```

Thứ tự lỗi:

```text
Domain → Application → Infrastructure → API
```

Không sửa API trước khi Application còn compile error.

---

## 42. Checklist compile

- [ ] `CommonTypes.cs` không có property trùng.
- [ ] `PEQuestion.TestCases` không nullable.
- [ ] `PE_Submission` dùng enum mới.
- [ ] `IPESubmissionRepository` và repository khớp chữ ký.
- [ ] `GetByIdAsync` dùng `async/await` để tránh CS8619.
- [ ] `TryClaimPendingAsync` đã implement.
- [ ] `Application.AddApplication(...)` chỉ đăng ký processor một lần.
- [ ] `Infrastructure.AddJudge0(...)` chỉ đăng ký Judge0 một lần.
- [ ] `Judge0Worker` chỉ được đăng ký một lần.
- [ ] Không còn `Judge0Service` cũ.
- [ ] Không còn merge markers.
- [ ] Không có secret thật trong appsettings.

---

## 43. Checklist runtime

- [ ] Docker Engine chạy.
- [ ] Judge0 container healthy.
- [ ] `GET http://localhost:2358/languages` thành công.
- [ ] C language ID `50` tồn tại.
- [ ] Java language ID `62` tồn tại.
- [ ] Multi-file language ID `89` tồn tại.
- [ ] Single C submission chạy thành công.
- [ ] Batch submission trả đủ token.
- [ ] Worker log `Judge0 grading worker started`.
- [ ] API tạo submission với `Pending`.
- [ ] Worker chuyển `Pending → Processing`.
- [ ] Execution token được lưu MongoDB.
- [ ] Submission chuyển `Processing → Completed`.
- [ ] Hidden test output không trả về frontend.
- [ ] API restart vẫn tiếp tục xử lý submission `Processing`.
- [ ] Judge0 tắt thì submission được retry/mark failed đúng policy.

---

## 44. Test case tích hợp tối thiểu

### Accepted

```text
Input: 2 3
Expected: 5
Actual: 5
Verdict: Accepted
```

### Wrong Answer

```text
Input: 2 3
Expected: 5
Actual: 6
Verdict: WrongAnswer
```

### Compilation Error

```text
Source code không compile
Verdict: CompilationError
CompileOutput có dữ liệu
```

### Time Limit

```text
Infinite loop
Verdict: TimeLimitExceeded
```

### Runtime Error

```text
Invalid memory access / uncaught exception
Verdict: RuntimeError
```

### Hidden case

```text
IsHidden = true
Input/ExpectedOutput/ActualOutput trả về API = null
```

### Restart recovery

```text
1. Submission ở Processing và đã có tokens.
2. Restart API.
3. Worker lấy lại Processing submission.
4. Poll token cũ.
5. Complete submission mà không tạo batch mới.
```

### Atomic claim

```text
1. Chạy hai worker/API instance.
2. Cùng đọc một Pending submission.
3. Chỉ một TryClaimPendingAsync trả về entity.
4. Chỉ một Judge0 batch được tạo.
```

---

# PHẦN J — QUYẾT ĐỊNH KIẾN TRÚC

## 45. Vì sao dùng Mongo polling thay vì Channel

`Channel<GradingJob>` nhanh nhưng message mất khi process restart.

Mongo polling phù hợp với APE hiện tại vì:

- Submission đã là persistent job.
- Restart không làm mất bài.
- Worker có thể resume execution bằng Judge0 tokens.
- Atomic claim chống duplicate xử lý.
- Không cần thêm RabbitMQ/Kafka cho capstone.
- Dễ demo và debug.

---

## 46. Vì sao dùng batch Judge0

Mỗi PE question có nhiều test case.

Thay vì:

```text
POST submission × số test case
GET submission × số test case × số vòng polling
```

dùng:

```text
POST /submissions/batch
GET /submissions/batch
```

Batch giảm số HTTP round-trip và giữ mapping token theo thứ tự test case.

Judge0 batch create trả một phần tử tương ứng với mỗi submission trong request. Backend phải kiểm tra:

```text
response.Count == request.TestCases.Count
```

và mỗi phần tử phải có token hợp lệ.

---

## 47. Quy tắc quan trọng cuối cùng

1. Judge0 chỉ thực thi code và trả execution result.
2. Expected output chính thức phải lấy từ question/test case đã được validate.
3. AI không tự quyết định output khi chấm bài.
4. Backend chịu trách nhiệm verdict, scoring và hidden-test protection.
5. Worker không chứa HTTP details của Judge0.
6. Application không phụ thuộc Infrastructure.
7. Token phải persist để recovery sau restart.
8. Không hard-code language ID trong worker.
9. Không giữ hai workflow Judge0 sau merge.
10. Không commit secrets vào source control.

---

## 48. Tóm tắt setup bắt buộc

```text
1. Chạy Judge0 self-hosted tại http://localhost:2358
2. Smoke test /languages
3. Cấu hình Judge0 + GradingWorker trong appsettings
4. AddApplication(configuration)
5. AddJudge0(configuration)
6. Register repositories/services
7. AddHostedService<Judge0Worker>() đúng một lần
8. Submit API tạo Pending submission
9. Worker atomic claim
10. Processor create batch + persist tokens
11. Poll batch results
12. Evaluate + score + hide secret test data
13. Persist Completed/Failed
14. Frontend polling result API
```

---

## Nguồn tổng hợp

Tài liệu được tổng hợp từ các file và đoạn code APE đã cung cấp, ưu tiên kiến trúc chuẩn hóa mới gồm:

```text
ICodeExecutionClient
Judge0Client
SubmissionGradingProcessor
Judge0Worker
ExecutionTracking
SubmissionResultEvaluator
GradingWorkerOptions
Judge0Options
```

Đối chiếu hành vi API với tài liệu Judge0 CE về:

```text
POST /submissions/batch
GET /submissions/batch
base64_encoded
status_id
stdout/stderr/compile_output
time/memory
```
