# APE — Managed Judge Workflow Mentor Guide

> Dùng khi viết lại hoàn toàn workflow chấm PE bằng Codex trong VS Code.
>
> Bối cảnh: `.NET 8`, Clean Architecture (`API`, `Application`, `Domain`, `Infrastructure`), MongoDB, deploy AWS, dùng dịch vụ Judge0 bên ngoài thay vì self-host.
>
> **Quan trọng:** Tài liệu dựa trên mô tả codebase hiện tại. Codex phải kiểm tra code thật trước khi sửa. Nếu code khác tài liệu, phải báo chênh lệch thay vì tự giả định.

---

## 1. Mục tiêu cuối cùng

```text
Student
  |
  | POST PE submission
  v
SubmissionController
  |
  v
PESubmissionService
  |
  | validate session, exam, question, language, source files
  | persist PE_Submission = Pending
  v
MongoDB
  |
  v
Grading Worker
  |
  | atomic lease / claim
  v
SubmissionGradingProcessor
  |
  v
ICodeExecutionClient
  |
  v
Managed Judge0 Provider
  |
  | create batch
  | persist tokens
  | poll results
  v
SubmissionResultEvaluator
  |
  | normalize output
  | protect hidden tests
  | calculate verdict and score
  v
PE_Submission = Completed / Failed
```

---

## 2. Trách nhiệm từng lớp

### Domain

Chứa:

```text
PE_Submission state machine
SubmissionProcessingStatus
SubmissionVerdict
Execution token tracking
Lease state
Retry/reset rules
Domain invariants
```

Không chứa:

```text
HttpClient
Judge0 status_id
AWS SDK
MongoDB filters
Configuration
```

### Application

Chứa:

```text
PESubmissionService
SubmissionGradingProcessor
SubmissionResultEvaluator
ICodeExecutionClient
IPESubmissionRepository
IPEQuestionRepository
provider-neutral request/result models
retry orchestration
```

Không tham chiếu namespace `Infrastructure`.

### Infrastructure

Chứa:

```text
Managed Judge0 HTTP adapter
Judge0 HTTP contracts
status mapping
options and validation
Mongo repositories
atomic lease implementation
DI extension
```

### API

Chứa:

```text
Controllers
BackgroundService worker
authentication
middleware
composition root
health endpoints
```

Worker phải mỏng, không tự build payload Judge0 và không tính điểm.

---

## 3. Phần nên giữ và phần nên bỏ

### Giữ hoặc chuẩn hóa

```text
Domain/Entities/PE_Submission.cs
Application/Services/PESubmissionService.cs
Application/Services/SubmissionGradingProcessor.cs
Application/Services/SubmissionResultEvaluator.cs
Application/Interfaces/ICodeExecutionClient.cs
Infrastructure/Persistence/PESubmissionRepository.cs
API/Workers/Judge0Worker.cs
```

### Loại khỏi runtime mới

```text
Judge0 Docker Compose
localhost:2358 hard-code
Judge0 Redis/PostgreSQL self-host
privileged container setup
in-memory Channel làm nguồn job duy nhất
Judge0Service cũ trùng chức năng
duplicate worker registrations
hard-coded X-Auth-Token
```

Tài liệu self-host có thể chuyển vào:

```text
docs/archive/judge0-self-host/
```

và thêm nhãn:

```text
Deprecated — current architecture uses a managed provider.
```

---

## 4. Provider-neutral contracts

Application không nên biết Judge0 HTTP schema.

### Interface

```csharp
public interface ICodeExecutionClient
{
    Task<CodeExecutionBatchReceipt> CreateBatchAsync(
        CodeExecutionBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodeExecutionResult>> GetBatchResultsAsync(
        IReadOnlyCollection<string> tokens,
        CancellationToken cancellationToken = default);

    Task<CodeExecutionProviderHealth> CheckHealthAsync(
        CancellationToken cancellationToken = default);
}
```

### Request

```csharp
public sealed record CodeExecutionBatchRequest(
    int LanguageId,
    IReadOnlyList<CodeFile> SourceFiles,
    IReadOnlyList<CodeExecutionCase> Cases);

public sealed record CodeExecutionCase(
    int CaseIndex,
    string StandardInput,
    int TimeLimitMs,
    int MemoryLimitKb);
```

Khuyến nghị không gửi `ExpectedOutput` cho provider. APE tự so sánh `stdout`.

### Receipt

```csharp
public sealed record CodeExecutionBatchReceipt(
    string Provider,
    IReadOnlyList<CodeExecutionToken> Tokens);

public sealed record CodeExecutionToken(
    int CaseIndex,
    string Token);
```

Luôn map token với `CaseIndex`; không chỉ tin thứ tự response.

### Result

```csharp
public sealed record CodeExecutionResult(
    int CaseIndex,
    string Token,
    CodeExecutionState State,
    string? StandardOutput,
    string? StandardError,
    string? CompileOutput,
    string? Message,
    double? TimeSeconds,
    int? MemoryKb,
    string? ProviderStatusCode,
    string? ProviderStatusDescription);
```

```csharp
public enum CodeExecutionState
{
    Queued,
    Processing,
    AcceptedByProvider,
    WrongAnswerByProvider,
    TimeLimitExceeded,
    CompilationError,
    RuntimeError,
    InternalError,
    Unknown
}
```

`AcceptedByProvider` chưa phải verdict cuối của APE.

---

## 5. Managed Judge0 options

```csharp
public sealed class ManagedJudge0Options
{
    public const string SectionName = "CodeExecution:Judge0";

    public string BaseUrl { get; init; } = string.Empty;
    public string ProviderName { get; init; } = "Judge0";
    public int TimeoutSeconds { get; init; } = 30;
    public int MaximumBatchSize { get; init; } = 50;
    public Dictionary<string, string> Headers { get; init; } = new();
}
```

Không hard-code header vì provider ngoài có thể dùng:

```text
X-Auth-Token
Authorization
X-RapidAPI-Key
X-RapidAPI-Host
header riêng của provider
```

Secret values không được commit trong `appsettings.json`.

---

## 6. State machine và lease

### Trạng thái chính

```text
Pending
Processing
Completed
Failed
```

### Execution tracking nên có

```text
AttemptNumber
Provider
Tokens mapped by CaseIndex
BatchCreatedAt
LastPollAt
PollCount
TransientFailureCount
LastProviderError
```

### Lease nên có

```text
LeaseOwner
LeaseAcquiredAt
LeaseExpiresAt
```

### Transition

```text
Pending
  → Processing through atomic acquire

Processing without tokens
  → create batch
  → persist tokens immediately

Processing with tokens
  → resume polling

Processing
  → Completed

Processing
  → Failed

Processing with expired lease
  → reclaimed safely

Failed
  → Pending only through an explicit retry use case
```

---

## 7. Atomic repository contract

Repository nên có operation tương đương:

```csharp
Task<PE_Submission?> TryAcquireForProcessingAsync(
    string submissionId,
    string workerId,
    DateTime utcNow,
    DateTime leaseExpiresAt,
    CancellationToken cancellationToken = default);

Task<bool> RenewLeaseAsync(
    string submissionId,
    string workerId,
    DateTime newLeaseExpiresAt,
    CancellationToken cancellationToken = default);
```

Atomic filter:

```text
Id == submissionId
AND (
    Status == Pending
    OR (
        Status == Processing
        AND LeaseExpiresAt < utcNow
    )
)
```

Chỉ một worker được acquire thành công.

Suggested indexes:

```text
{ Status: 1, LeaseExpiresAt: 1, SubmittedAt: 1 }
{ SessionId: 1, QuestionId: 1, AttemptNumber: -1 }
```

Tên BSON phải theo mapping code thật.

---

## 8. Idempotency và recovery

### Quy tắc

```text
Nếu đã có execution tokens:
    không CreateBatch lần nữa
    tiếp tục polling token cũ
```

Token phải được persist ngay sau create batch.

### Failure window khó tránh

```text
Provider tạo batch thành công
nhưng app chết trước khi lưu token
```

Nếu provider hỗ trợ idempotency key, dùng:

```text
SubmissionId + AttemptNumber
```

Nếu không hỗ trợ:

- ghi rõ limitation;
- lưu audit timestamp;
- retry giới hạn;
- không tuyên bố exactly-once;
- theo dõi duplicate batch suspected.

---

## 9. Processor orchestration

```text
Load submission
Verify Processing and lease ownership
Load PEQuestion
Validate testcases

If no tokens:
    build provider-neutral request
    call CreateBatchAsync
    validate token coverage
    persist tokens immediately

Until absolute deadline:
    renew lease when needed
    call GetBatchResultsAsync

    if all terminal:
        evaluate
        complete
        persist
        return

    wait with cancellation support

Transient error:
    persist diagnostic
    bounded retry with backoff and jitter

Permanent error:
    mark Failed
```

---

## 10. Error classification

### Permanent — không retry vô hạn

```text
400 invalid payload
401 invalid API key
403 forbidden or plan restriction
404 wrong endpoint
422 invalid language/resource limit
unsupported language
invalid source package
irrecoverably malformed response
```

### Transient — retry có giới hạn

```text
408 timeout
429 rate limit
500–599 provider error
DNS/network interruption
connection reset
temporary incomplete result
```

Rules:

- đọc `Retry-After` cho 429;
- exponential backoff + jitter;
- có max attempts;
- có absolute deadline;
- cancellation do shutdown không được mark như provider failure;
- không log headers hoặc API key.

---

## 11. Result evaluator

Evaluator nhận:

```text
PEQuestion.TestCases
CodeExecutionResult by CaseIndex
MaxScore
```

Phải:

1. kiểm tra đủ đúng một result cho mỗi testcase;
2. phát hiện duplicate/missing CaseIndex;
3. map compilation/runtime/time-limit/system errors;
4. normalize newline;
5. so sánh output;
6. tính case verdict;
7. tính score;
8. ẩn hidden testcase;
9. aggregate runtime/memory theo rule đã document;
10. tạo kết quả deterministic.

Không dùng riêng `status_id == Accepted` để kết luận Accepted.

---

## 12. Hidden data policy

Không gửi hoặc log:

```text
SolutionCode
Hidden ExpectedOutput
Hidden Input
Hidden ActualOutput
API key
MongoDB connection string
JWT secret
```

Student DTO cho hidden case:

```json
{
  "caseIndex": 3,
  "isHidden": true,
  "verdict": "WrongAnswer",
  "input": null,
  "expectedOutput": null,
  "actualOutput": null
}
```

---

## 13. Worker

Worker chỉ:

```text
generate WorkerId
query candidates
atomic acquire
create scope
call processor
isolate exception per item
respect shutdown
idle delay
structured logging
```

Worker không:

```text
build Judge0 HTTP payload
calculate score
access IMongoCollection directly
use Channel as only durable queue
```

---

## 14. Implementation roadmap

### Phase 0 — Audit only

Output:

```text
docs/judge-rewrite/00_CURRENT_STATE_AUDIT.md
```

Không sửa source.

### Phase 1 — Domain invariants

- state machine;
- lease;
- token mapping;
- domain tests.

### Phase 2 — Application contracts

- provider-neutral interface/models;
- không leak Judge0 types.

### Phase 3 — Managed provider adapter

- typed HttpClient;
- configurable headers;
- batch create/get;
- status mapping;
- error classification;
- fake HTTP tests.

### Phase 4 — Mongo atomic lease

- acquire;
- renew;
- reclaim;
- concurrency integration tests.

### Phase 5 — Processor

- create/resume;
- persist tokens;
- polling deadline;
- retry;
- evaluator;
- tests.

### Phase 6 — Worker

- thin;
- graceful;
- scoped;
- concurrency safe.

### Phase 7 — API safety

- submit returns `202 Accepted`;
- ownership;
- sanitized DTO.

### Phase 8 — DI/config

- extension methods;
- startup validation;
- no duplicate registration;
- Secrets Manager-ready configuration.

### Phase 9 — Full tests

- accepted;
- wrong answer;
- compilation/runtime/time limit;
- 429;
- 5xx;
- 401;
- malformed response;
- restart;
- lease recovery;
- hidden DTO.

### Phase 10 — Legacy cleanup

Chỉ xóa code cũ khi build/test xanh.

### Phase 11 — AWS readiness

MVP:

```text
ECR
ECS Express Mode / Fargate
desired count = 1
MongoDB Atlas
Secrets Manager
CloudWatch
managed Judge0
```

Sau MVP:

```text
API service
Amazon SQS
dedicated grading worker
SQS DLQ
```

---

## 15. AWS deployment decisions

### MVP

```text
React                  → AWS Amplify Hosting
ASP.NET Core API       → Amazon ECR + ECS Express Mode/Fargate
MongoDB                → MongoDB Atlas
Documents              → Amazon S3
Secrets                → AWS Secrets Manager
Logs                   → Amazon CloudWatch
Code execution         → Managed Judge0-compatible service
```

Ban đầu giữ một ECS task vì API và BackgroundService cùng process.

### Khi nào thêm SQS

Thêm SQS khi:

```text
API cần scale độc lập
grading backlog tăng
cần DLQ
cần worker concurrency riêng
provider latency ảnh hưởng process
```

SQS message chỉ chứa:

```json
{
  "submissionId": "...",
  "attemptNumber": 1
}
```

Không chứa source code hoặc testcase.

---

## 16. Required configuration

Non-secret:

```text
CodeExecution__Provider
CodeExecution__Judge0__BaseUrl
CodeExecution__Judge0__ProviderName
CodeExecution__Judge0__TimeoutSeconds
CodeExecution__Judge0__MaximumBatchSize
GradingWorker__Enabled
GradingWorker__BatchSize
GradingWorker__IdleDelaySeconds
GradingWorker__LeaseSeconds
GradingWorker__LeaseRenewalSeconds
GradingWorker__PollingIntervalSeconds
GradingWorker__MaximumPollingSeconds
GradingWorker__MaximumTransientFailures
```

Secret:

```text
MongoDB connection string
JWT secret
Google OAuth secret
Gemini key
Judge provider authentication headers
```

ECS có thể inject Secrets Manager values thành environment variables. Không cần AWS SDK chỉ để đọc secret đã được ECS inject.

---

## 17. Definition of Done

- [ ] Submit API không chờ provider hoàn tất.
- [ ] Persist `Pending`.
- [ ] Atomic acquire.
- [ ] Không duplicate normal batch.
- [ ] Persist token trước polling dài.
- [ ] Restart resume token cũ.
- [ ] 429/5xx retry giới hạn.
- [ ] 401/403/422 fail đúng.
- [ ] Polling có deadline.
- [ ] Hidden data không lộ.
- [ ] Score tính tại APE.
- [ ] URL/header/key từ config.
- [ ] Không còn localhost hard-code.
- [ ] Domain/evaluator/client tests.
- [ ] Repository concurrency tests.
- [ ] Build toàn solution xanh.
- [ ] Không nullable warning mới.
- [ ] DI không trùng.
- [ ] Legacy workflow được cleanup an toàn.

---

## 18. Acceptance test matrix

```text
Accepted
Wrong Answer
Compilation Error
Runtime Error
Time Limit Exceeded
Provider Internal Error
429 then success
5xx exhaust retries
401 immediate failure
Malformed JSON
Malformed Base64
Missing token
Duplicate token
Missing CaseIndex
Duplicate CaseIndex
Polling deadline
Cancellation
Existing-token resume
Concurrent claim
Expired lease recovery
Hidden result sanitization
Unauthorized submission access
```

---

## 19. Official references

- Judge0 batch API: https://ce.judge0.com/
- Judge0 authentication behavior: https://github.com/judge0/judge0/blob/master/docs/api/authentication/authentication.md
- Amazon ECS Express Mode: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-overview.html
- ECS Secrets Manager integration: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html
- ECS secret refresh behavior: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/specifying-sensitive-data.html
- Amazon SQS DLQ: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-dead-letter-queues.html
