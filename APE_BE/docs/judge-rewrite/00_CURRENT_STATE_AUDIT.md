# P00 Current State Audit: PE Grading and Judge0 Rewrite Surface

Date: 2026-07-30

Scope:
- Audit only.
- No source, project, package, NuGet, or runtime configuration changes were made.
- This document is based on direct repository inspection plus explicitly user-supplied build/runtime evidence.

## 1. Evidence Scope and Verification Status

### 1.1 Codex sandbox restore/build result

Observed directly inside the Codex sandbox:

- `dotnet build .\APE_Core.sln` failed before compilation.
- `dotnet build .\APE_Core.sln --no-restore` also failed before compilation.
- Exact failure class: `NU1301`.
- Exact cause shown by the tool output:
  - `Unable to get repository signature information for source https://api.nuget.org/v3-index/repository-signatures/5.0.0/index.json`
  - `An attempt was made to access a socket in a way forbidden by its access permissions. (api.nuget.org:443)`

Conclusion:
- This is a Codex sandbox restore/network limitation.
- It is **not** evidence of source-code compilation failure.

### 1.2 Manual local full solution build result (user-supplied)

User supplied this exact evidence from `C:\Users\FPT SHOP\Desktop\APE_2\APE_BE`:

```text
dotnet build .\APE_Core.sln
Restore complete
Domain succeeded
Application succeeded
Infrastructure succeeded
API succeeded
BE_UnitTests succeeded
Build succeeded
```

Conclusion:
- Full solution build succeeded locally outside the Codex sandbox.
- No compiler errors or warnings were shown in the supplied output.

### 1.3 Manual local test result (user-supplied)

User supplied this exact evidence:

```text
dotnet test .\APE_Core.sln --no-restore
BE_UnitTests test succeeded
Test summary: total: 1; failed: 0; succeeded: 1; skipped: 0
```

Conclusion:
- Solution tests succeeded locally outside the Codex sandbox.
- Only 1 test executed.

### 1.4 Manual local API startup result (user-supplied)

User supplied runtime evidence for `dotnet run` from `API/`:

- API build completed successfully.
- Application startup completed successfully.
- `Database and AI configuration initialized successfully.`
- `API.Workers.Judge0Worker started successfully.`
- API listened on `http://localhost:5292`.
- No compiler errors or warnings were shown in that startup output.

Conclusion:
- Current startup path is viable in the user's local environment.
- This was not executed from the Codex sandbox.

## 2. Files and Types Audited

### 2.1 Domain

- [`Domain/Entities/PE_Submission.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Entities\PE_Submission.cs)
  - `PE_Submission`
  - `TestResultItem`
  - `ExecutionTracking`
  - `SubmissionEvaluation`
- [`Domain/Entities/PEQuestion.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Entities\PEQuestion.cs)
  - `PEQuestion`
- [`Domain/Entities/CommonTypes.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Entities\CommonTypes.cs)
  - `CodeFile`
  - `TestCase`
  - `Hint`
- [`Domain/Enums/SubmissionProcessingStatus.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Enums\SubmissionProcessingStatus.cs)
- [`Domain/Enums/SubmissionVerdict.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Enums\SubmissionVerdict.cs)
- [`Domain/Enums/SubmissionMode.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Enums\SubmissionMode.cs)
- [`Domain/Entities/Exam.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Entities\Exam.cs)
- [`Domain/Entities/PracticeSession.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Entities\PracticeSession.cs)

### 2.2 Application

- [`Application/Services/PESubmissionService.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Services\PESubmissionService.cs)
  - `PESubmissionService`
- [`Application/Services/SubmissionGradingProcessor.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Services\SubmissionGradingProcessor.cs)
  - `SubmissionGradingProcessor`
- [`Application/Services/SubmissionResultEvaluator.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Services\SubmissionResultEvaluator.cs)
  - `SubmissionResultEvaluator`
- [`Application/Interfaces/IPESubmissionService.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Interfaces\IPESubmissionService.cs)
- [`Application/Interfaces/IPESubmissionRepository.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Interfaces\IPESubmissionRepository.cs)
- [`Application/Interfaces/ICodeExecutionClient.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Interfaces\ICodeExecutionClient.cs)
- [`Application/Interfaces/ISubmissionGradingProcessor.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Interfaces\ISubmissionGradingProcessor.cs)
- [`Application/Interfaces/ISubmissionResultEvaluator.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Interfaces\ISubmissionResultEvaluator.cs)
- [`Application/Models/CodeExecutionBatchRequest.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Models\CodeExecutionBatchRequest.cs)
- [`Application/Models/CodeExecutionBatchReceipt.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Models\CodeExecutionBatchReceipt.cs)
- [`Application/Models/CodeExecutionResult.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Models\CodeExecutionResult.cs)
- [`Application/Options/GradingWorkerOptions.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Options\GradingWorkerOptions.cs)
- [`Application/Options/GradingWorkerOptionsValidator.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Options\GradingWorkerOptionsValidator.cs)
- [`Application/DTOs/SubmissionDtos.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\DTOs\SubmissionDtos.cs)
- [`Application/DTOs/PESubmissionDto.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\DTOs\PESubmissionDto.cs)

### 2.3 Infrastructure

- [`Infrastructure/Persistence/PESubmissionRepository.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Persistence\PESubmissionRepository.cs)
  - `PESubmissionRepository`
- [`Infrastructure/Judge0/Judge0Client.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0Client.cs)
  - `Judge0Client`
- [`Infrastructure/Judge0/Judge0Options.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0Options.cs)
- [`Infrastructure/Judge0/Judge0OptionsValidator.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0OptionsValidator.cs)
- [`Infrastructure/Judge0/Judge0SourcePackageBuilder.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0SourcePackageBuilder.cs)
- [`Infrastructure/Judge0/Judge0SourcePackage.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0SourcePackage.cs)
- [`Infrastructure/Judge0/Judge0MultiFileProfileResolver.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0MultiFileProfileResolver.cs)
- [`Infrastructure/Judge0/Judge0MultiFileProfile.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0MultiFileProfile.cs)
- [`Infrastructure/Judge0/IJudge0SourcePackageBuilder.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\IJudge0SourcePackageBuilder.cs)
- [`Infrastructure/Judge0/IJudge0MultiFileProfileResolver.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\IJudge0MultiFileProfileResolver.cs)
- [`Infrastructure/Judge0/Judge0CreateBatchRequest.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0CreateBatchRequest.cs)
- [`Infrastructure/Judge0/Judge0SubmissionRequest.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0SubmissionRequest.cs)
- [`Infrastructure/Judge0/Judge0CreateSubmissionResponse.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0CreateSubmissionResponse.cs)
- [`Infrastructure/Judge0/Judge0BatchResultResponse.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0BatchResultResponse.cs)
- [`Infrastructure/Judge0/Judge0SubmissionResultResponse.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0SubmissionResultResponse.cs)
- [`Infrastructure/Judge0/Judge0StatusResponse.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0StatusResponse.cs)
- [`Infrastructure/Judge0/NullableDoubleJsonConverter.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\NullableDoubleJsonConverter.cs)
- [`Infrastructure/Judge0/Contracts/NullableIntJsonConverter.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Contracts\NullableIntJsonConverter.cs)
- [`Infrastructure/DI/Judge0DependencyInjection.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\DI\Judge0DependencyInjection.cs)
- [`Infrastructure/Data/DbContext.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Data\DbContext.cs)
- [`Infrastructure/Data/DbInitializer.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Data\DbInitializer.cs)

### 2.4 API

- [`API/Workers/Judge0Worker.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Workers\Judge0Worker.cs)
  - `Judge0Worker`
- [`API/Workers/DocumentProcessingWorker.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Workers\DocumentProcessingWorker.cs)
  - `DocumentProcessingWorker`
- [`API/Controllers/SubmissionController.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Controllers\SubmissionController.cs)
- [`API/Controllers/AdminAIController.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Controllers\AdminAIController.cs)
- [`API/Program.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Program.cs)
- [`API/appsettings.json`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\appsettings.json)
- [`API/appsettings.Development.json`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\appsettings.Development.json)

### 2.5 Tests and related artifacts

- [`BE_UnitTests/BE_UnitTests.csproj`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\BE_UnitTests\BE_UnitTests.csproj)
- [`BE_UnitTests/FESubmissionServiceTests.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\BE_UnitTests\FESubmissionServiceTests.cs)
- [`BE_UnitTests/SubmissionControllerTests.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\BE_UnitTests\SubmissionControllerTests.cs)
- [`BE_UnitTests/UnitTest1.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\BE_UnitTests\UnitTest1.cs)

### 2.6 Legacy and documentation artifacts inspected for comparison

- [`docs/FINAL_SCHEMA.md`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\docs\FINAL_SCHEMA.md)
- [`docs/judge-rewrite/APE_Managed_Judge_Mentor_Guide.md`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\docs\judge-rewrite\APE_Managed_Judge_Mentor_Guide.md)
- [`docs/judge-rewrite/03_APE_Judge0_Workflow_Setup.md`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\docs\judge-rewrite\03_APE_Judge0_Workflow_Setup.md)
- `application-build-*.txt`, `api-build-*.txt`, `domain-build-*.txt`, `infrastructure-build-*.txt`

## 3. Current End-to-End Workflow

### 3.1 Submit path

Current HTTP entrypoint:
- `POST /api/student/submissions/pe` in [`API/Controllers/SubmissionController.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Controllers\SubmissionController.cs)

Current flow:
1. Controller extracts authenticated `studentId`.
2. Controller calls `IPESubmissionService.SubmitAsync(...)`.
3. `PESubmissionService` loads `PracticeSession`.
4. Service checks ownership via `session.StudentId`.
5. Service loads `Exam`.
6. Service validates that `request.QuestionId` belongs to `exam.PeExamQuestions`.
7. Service loads `PEQuestion`.
8. Service validates:
   - question status is `Active`
   - question course matches exam course
   - at least one test case exists
   - requested language is allowed
   - submitted code files are non-empty and valid
9. Service gets latest attempt number from `PESubmissionRepository.GetLatestAttemptNumberAsync`.
10. Service constructs a domain `PE_Submission` with:
    - `Status = Pending`
    - `Execution = ExecutionTracking.Create()`
11. Service persists submission to Mongo.
12. Controller returns `202 Accepted` on success.

### 3.2 Worker path

Background entrypoint:
- [`API/Workers/Judge0Worker.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Workers\Judge0Worker.cs)

Current flow:
1. Worker loops continuously.
2. Worker queries pending submissions by status.
3. For each pending submission, worker calls `TryClaimPendingAsync`.
4. Repository atomically flips status from `Pending` to `Processing`.
5. Worker invokes `ISubmissionGradingProcessor.ProcessAsync(submissionId)`.
6. Processor reloads submission by id.
7. Processor loads `PEQuestion`.
8. If no provider tokens exist, processor builds a `CodeExecutionBatchRequest`.
9. Processor calls `ICodeExecutionClient.CreateBatchAsync`.
10. Processor saves returned execution tokens into the domain entity.
11. Processor polls `ICodeExecutionClient.GetBatchResultsAsync(tokens)` until all results are finished or polling limit is reached.
12. Processor passes question + provider results to `SubmissionResultEvaluator`.
13. Evaluator maps Judge0 status ids to `SubmissionVerdict` values and delegates scoring to `ISubmissionScoringService`.
14. Processor calls `submission.Complete(...)` and persists the updated submission.
15. On unrecoverable failure, processor calls `submission.MarkProcessingFailed(...)` and persists it.

### 3.3 Read path

Current read endpoints:
- `GET /api/student/submissions/pe/{id}`
- `GET /api/student/submissions/pe/session/{sessionId}`

Current read behavior:
- Authorization is enforced indirectly by loading the `PracticeSession` and comparing `session.StudentId` to the authenticated user.
- Hidden test case data is masked in response DTOs by `PESubmissionService.MapTestResult`.

## 4. Dependency Direction

Current dependency direction is mostly aligned with Clean Architecture:

- `API` -> `Application`, `Infrastructure`
- `Infrastructure` -> `Application`, `Domain`
- `Application` -> `Domain`
- `Domain` -> no project references

PE grading specific direction:
- `SubmissionController` -> `IPESubmissionService`
- `Judge0Worker` -> `IPESubmissionRepository`, `ISubmissionGradingProcessor`
- `SubmissionGradingProcessor` -> `IPESubmissionRepository`, `IPEQuestionRepository`, `ICodeExecutionClient`, `ISubmissionResultEvaluator`
- `Judge0Client` -> `ICodeExecutionClient`
- `PESubmissionRepository` -> Mongo `DbContext`

Important note:
- Application layer does not reference `Infrastructure` namespaces directly in the grading path.
- Provider-neutral abstraction exists as `ICodeExecutionClient`, but the data shape still reflects Judge0 semantics closely (`StatusId`, `StatusDescription`).

## 5. Current State Model and Transitions

### 5.1 Submission states in code

Source of truth:
- [`Domain/Enums/SubmissionProcessingStatus.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Enums\SubmissionProcessingStatus.cs)
- [`Domain/Entities/PE_Submission.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Domain\Entities\PE_Submission.cs)

Observed states used by current flow:
- `Pending`
- `Processing`
- `Completed`
- `Failed`

### 5.2 Actual transitions implemented

Implemented transitions:
- Create -> `Pending`
- `Pending` -> `Processing` through repository atomic claim
- `Processing` -> `Completed` through `submission.Complete(...)`
- `Pending` or `Processing` -> `Failed` through `submission.MarkProcessingFailed(...)`
- `Failed` -> `Pending` through `submission.ResetForManualRetry()` in domain, but no active application use case was found that invokes it

### 5.3 ExecutionTracking behavior

Domain object:
- `ExecutionTracking` inside `PE_Submission`

Fields:
- `Tokens`
- `AttemptCount`
- `LastAttemptAt`
- `LastError`

Behavior:
- Attempt count starts at 0 in `ExecutionTracking.Create()`.
- Repository claim increments `Execution.AttemptCount` when flipping to `Processing`.
- Additional execution retries increment `Execution.AttemptCount` again through `BeginAdditionalExecutionAttempt(...)`.

Important consistency note:
- `PE_Submission.AttemptCount` means student submission attempt number.
- `Execution.AttemptCount` means provider execution attempt/retry count.
- These are separate counters.

## 6. Provider Request/Response Models

### 6.1 Application-facing models

Current neutral-ish contracts:
- `CodeExecutionBatchRequest`
- `CodeExecutionBatchReceipt`
- `CodeExecutionResult`
- `ICodeExecutionClient`

Request model:
- `LanguageId`
- `SourceFiles`
- `TestCases`

Receipt model:
- `Tokens`

Result model:
- `Token`
- `StatusId`
- `StatusDescription`
- `StandardOutput`
- `StandardError`
- `CompileOutput`
- `Message`
- `TimeSeconds`
- `MemoryKb`

Observation:
- The abstraction is good enough to hide HTTP transport.
- It still leaks Judge0 response semantics through `StatusId`.

### 6.2 Judge0 HTTP request models

From [`Infrastructure/Judge0/Judge0SubmissionRequest.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Judge0\Judge0SubmissionRequest.cs):

Per testcase submission payload includes:
- `language_id`
- `source_code`
- `additional_files`
- `stdin`
- `expected_output`
- `cpu_time_limit`
- `wall_time_limit`
- `memory_limit`

Batch create payload:
- `submissions: IReadOnlyList<Judge0SubmissionRequest>`

Observation:
- The current provider contract sends `expected_output` to Judge0.
- Final scoring is still performed by APE.

### 6.3 Judge0 HTTP response models

Batch-create response:
- list of `Judge0CreateSubmissionResponse`
- uses `token`
- treats other extension data as validation errors

Batch-result response:
- `Judge0BatchResultResponse.Submissions`
- each item includes:
  - `token`
  - `status_id`
  - `status`
  - `stdout`
  - `stderr`
  - `compile_output`
  - `message`
  - `time`
  - `memory`

### 6.4 Multi-file packaging

Current behavior:
- Single-file submissions send inline `source_code`.
- Multi-file submissions are zipped and sent via `additional_files`.
- Extra `compile` and `run` scripts are inserted into the archive.
- Multi-file execution is remapped to configured `MultiFileLanguageId`.

Current language support in profile resolver:
- C (`50`)
- Java (`62`)

Java multi-file requirement:
- must include `Main.java`

## 7. Retry and Polling Behavior

### 7.1 Worker scheduling

Worker config from [`Application/Options/GradingWorkerOptions.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\Options\GradingWorkerOptions.cs):
- `IdleDelaySeconds = 2`
- `BatchSize = 10`
- `MaximumExecutionAttempts = 3`
- `InitialRetryDelaySeconds = 2`
- `PollingIntervalSeconds = 2`
- `MaximumPollingAttempts = 30`

### 7.2 Provider create-batch retry

In `SubmissionGradingProcessor.CreateExecutionBatchAsync(...)`:
- retries only on `CodeExecutionClientException` with `IsTransient = true`
- uses exponential backoff
- max retry count is tied to `Execution.AttemptCount < MaximumExecutionAttempts`

Backoff formula:
- `InitialRetryDelaySeconds * 2^(attempt-1)`
- capped at 30 seconds

### 7.3 Polling behavior

In `SubmissionGradingProcessor.PollUntilCompletedAsync(...)`:
- fetches results using the existing token list
- returns only when all provider results are finished
- polls every `PollingIntervalSeconds`
- hard stop at `MaximumPollingAttempts`
- transient polling errors increment a local `transientFailureCount`
- polling transient retries also use exponential backoff

### 7.4 Evaluator behavior

In `SubmissionResultEvaluator`:
- provider result count must match test case count
- unfinished provider results are treated as transient provider failure
- Judge0 status `13` is treated as transient provider internal error
- unsupported Judge0 status ids are treated as non-transient client errors

## 8. Atomic Claim Behavior

Atomic claim implementation exists in:
- [`Infrastructure/Persistence/PESubmissionRepository.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Persistence\PESubmissionRepository.cs)

Mechanism:
- `FindOneAndUpdateAsync`
- filter requires:
  - matching `Id`
  - current `Status == Pending`

Updates applied atomically:
- set `Status = Processing`
- set `ProcessingStartedAt`
- clear `CompletedAt`
- clear `FinalVerdict`
- clear `ProcessingError`
- increment `Execution.AttemptCount`
- set `Execution.LastAttemptAt`
- unset `Execution.LastError`

Assessment:
- This is a real atomic claim.
- It prevents double-claim of the same pending submission across concurrent workers.

Limitation:
- It does not support lease expiration or reclaiming stale `Processing` items.
- It only claims `Pending`.

## 9. Restart Recovery Behavior

Current restart recovery is partial.

What exists:
- Worker, after processing all current `Pending`, also queries `Processing` submissions and re-invokes `ProcessAsync(...)`.
- `SubmissionGradingProcessor` resumes polling if `submission.Execution.HasTokens` is already true.

What this means:
- If the app restarts after tokens were persisted, the worker can resume polling existing tokens.

Important limitation:
- There is no lease owner, lease expiry, heartbeat, or stale-processing timeout.
- Any `Processing` record with no tokens persists as a generic processing item.
- Recovery for `Processing` items depends on the processor logic, not on a durable queue/lease system.

Failure window still present:
- provider batch may succeed
- process may die before token persistence
- current system has no provider idempotency key or replay reconciliation mechanism

## 10. Mongo Persistence and Index State

### 10.1 Current repository behavior

`PESubmissionRepository` currently supports:
- create
- get by id
- get by session
- get latest attempt number by `(SessionId, QuestionId)`
- get by status set
- atomic pending claim
- full document replace update

### 10.2 Current runtime initializer

Current runtime initializer:
- [`Infrastructure/Data/DbInitializer.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Infrastructure\Data\DbInitializer.cs)

Actual behavior:
- returns `Task.CompletedTask`
- does not create indexes
- does not seed data

### 10.3 Legacy initializer artifact

Legacy artifact:

It contains historical index intent for PE submissions:
- `IX_PE_Submission_SessionQuestion`
- `IX_PE_Submission_WorkerQueue`
- `UX_PE_Submission_Attempt`

Assessment:
- The empty current `DbInitializer.cs` and the much larger legacy initializer are **repository consistency findings only**.
- They do not prove runtime failure.
- They do prove that current runtime code no longer provisions the old indexes automatically.

### 10.4 Mongo index conclusion

Verified from current runtime code:
- No active index creation code is present in `DbInitializer.cs`.

Not verified:
- Whether indexes already exist in the live MongoDB database.

## 11. Configuration and Secrets

### 11.1 Code execution configuration

Current config section in [`API/appsettings.json`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\appsettings.json):
- `Judge0:BaseUrl`
- `Judge0:TimeoutSeconds`
- `Judge0:MaximumBatchSize`
- `Judge0:MaximumSourceFiles`
- `Judge0:MaximumArchiveBytes`
- `Judge0:MultiFileLanguageId`
- `Judge0:ApiKey`
- `Judge0:ApiKeyHeaderName`
- `Judge0:DefaultHeaders`

Current default value observed:
- `BaseUrl = http://localhost:2358`

Assessment:
- Current code is still configured around a self-hosted/local Judge0-compatible endpoint by default.
- The infrastructure adapter itself is header-configurable and can point to an external provider.
- The default shipped configuration still assumes localhost.

### 11.2 Worker configuration

Config section:
- `GradingWorker`

Bound and validated in:
- `Application.DependencyInjection`
- `GradingWorkerOptionsValidator`

### 11.3 Secret exposure finding

`API/appsettings.json` currently contains concrete values for:
- MongoDB connection string
- JWT secret
- Google client id

Assessment:
- This is a repository security/configuration finding.
- It is not directly a Judge0 rewrite bug, but it is relevant to deployment and environment separation.

## 12. Duplicated or Legacy Implementations

### 12.1 Runtime code vs legacy artifacts

Legacy or historical artifacts present:
- `03_APE_Judge0_Workflow_Setup.md`
- multiple `*-build*.txt` files
- `docs/judge-rewrite/APE_Managed_Judge_Mentor_Guide.md`

These files contain older compile failures, schema shapes, and architecture plans.

### 12.2 Placeholder worker

[`API/Workers/DocumentProcessingWorker.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\API\Workers\DocumentProcessingWorker.cs):
- explicitly marked as legacy/source-compatibility placeholder
- not registered in current `Program.cs`

### 12.3 DTO drift / stale DTO family

[`Application/DTOs/PESubmissionDto.cs`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\Application\DTOs\PESubmissionDto.cs) defines `PESubmissionResultDto` with stale property names such as:
- `SubmitTime`
- `EarnedScore`
- string `Status`
- `Mode` as string
- `TestCaseResults`

Current active controller/service path uses instead:
- `SubmissionAcceptedDto`
- `PESubmissionSummaryDto`
- `PESubmissionDetailDto`

Assessment:
- `PESubmissionResultDto` appears legacy or at least not the active read model for current PE endpoints.

### 12.4 Build-log artifacts are not current compiler state

Files such as:
- `application-build-next.txt`
- `application-build-fixed.txt`
- `api-build-after-cleanup.txt`

contain historical compiler errors about removed fields like:
- `PE_Submission.StudentId`
- `SubmitTime`
- `EarnedScore`

These are **legacy build-log findings**, not current compiler evidence, because the user-supplied current full solution build succeeded.

## 13. Compiler Errors, Warnings, and Legacy Build-Log Findings

### 13.1 Restore/network failure

Observed in Codex sandbox only:
- `NU1301` against `api.nuget.org:443`
- restore/network blocked by sandbox policy

This is separate from compiler state.

### 13.2 Current compiler errors

Current verified status from user-supplied local full solution build:
- No compiler errors shown.
- Build succeeded for all projects:
  - `Domain`
  - `Application`
  - `Infrastructure`
  - `API`
  - `BE_UnitTests`

### 13.3 Current compiler warnings

Current verified status from user-supplied local full solution build:
- No warnings shown in supplied output.

Important scope note:
- This is evidence of "none shown in supplied command output".
- It is not a claim that the repository is warning-free under all environments, analyzers, or build settings.

### 13.4 Legacy build-log findings

Historical files in the repo show previous error families including:
- stale references to `PE_Submission.StudentId`
- stale references to `SubmitTime`, `EarnedScore`, `TotalTestCase`
- older broken `Judge0Worker` versions
- DTO/domain mismatch errors in question controllers

These findings remain useful as repository-history signals:
- there was a recent or ongoing migration in PE submission shape
- some docs and helper files lag behind current code

They are **not** current compiler failures based on the latest user-provided build evidence.

## 14. Current Test Coverage

### 14.1 Verified executed tests

From user-supplied `dotnet test .\APE_Core.sln --no-restore` output:
- total tests executed: 1
- passed: 1
- failed: 0
- skipped: 0

### 14.2 Repository test files

Existing test project:
- [`BE_UnitTests/BE_UnitTests.csproj`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\BE_UnitTests\BE_UnitTests.csproj)

Observed test files:
- `UnitTest1.cs`
- `FESubmissionServiceTests.cs`
- `SubmissionControllerTests.cs`

### 14.3 Effective coverage reality

`UnitTest1.cs`:
- contains a single empty MSTest method
- this is the most likely one executed test

`FESubmissionServiceTests.cs`:
- almost entirely commented out

`SubmissionControllerTests.cs`:
- almost entirely commented out

PE grading specific conclusion:
- there is effectively **no active automated test coverage** for:
  - `PESubmissionService`
  - `SubmissionGradingProcessor`
  - `SubmissionResultEvaluator`
  - `Judge0Worker`
  - `Judge0Client`
  - `PESubmissionRepository` concurrency behavior

## 15. Code vs Documentation Differences

### 15.1 `docs/FINAL_SCHEMA.md` differs from current code

The documentation still describes PE submission fields such as:
- `StudentId`
- `ExamId`
- `SubmitTime`
- `EarnedScore`
- `TotalTestCase`
- `Judge0Tokens`

Current code in `PE_Submission` uses:
- no `StudentId`
- no `ExamId`
- `SubmittedAt`
- `QuestionScore`
- `TotalTestCases`
- `Execution.Tokens`

Conclusion:
- Current code and schema doc are out of sync.
- For PE submission shape, code is the source of truth.

### 15.2 Managed Judge guide differs from current implementation

[`docs/judge-rewrite/APE_Managed_Judge_Mentor_Guide.md`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\docs\judge-rewrite\APE_Managed_Judge_Mentor_Guide.md) describes a target architecture with:
- stronger lease model
- case-indexed tokens
- provider-neutral enums
- external managed provider assumptions
- recommended no-`expected_output` provider contract

Current code still has:
- no lease owner/expiry
- token list without explicit case-index mapping
- Judge0-specific `StatusId`
- `expected_output` sent to provider
- localhost default configuration

Conclusion:
- The guide is aspirational/design-forward, not a description of current implemented behavior.

### 15.3 Setup docs assume self-host

[`docs/judge-rewrite/03_APE_Judge0_Workflow_Setup.md`](C:\Users\FPT SHOP\Desktop\APE_2\APE_BE\docs\judge-rewrite\03_APE_Judge0_Workflow_Setup.md) and appsettings defaults still reflect self-host/local workflow assumptions.

Current code compatibility:
- can support an external Judge0-compatible provider by changing config
- is not yet rewritten around that assumption by default

## 16. Keep / Replace / Delete Recommendations

### 16.1 Keep

Keep as strong foundations:
- `PE_Submission` domain state machine
- `PESubmissionService` validation flow
- `PESubmissionRepository.TryClaimPendingAsync(...)`
- `SubmissionGradingProcessor` orchestration shape
- `SubmissionResultEvaluator` separation from HTTP and persistence
- `ICodeExecutionClient` as abstraction boundary
- `Judge0SourcePackageBuilder` path/archive validation
- hidden test masking in `PESubmissionService` DTO mapping

### 16.2 Replace

Replace or refactor in rewrite scope:
- `Judge0Options` default localhost assumption
- provider result contract based on raw `StatusId`
- token persistence model from plain list to structured token mapping if managed provider semantics require stronger ordering guarantees
- worker recovery model from status-only reclaim to real lease/recovery model
- startup config handling for secrets committed in appsettings

### 16.3 Delete or archive after rewrite is complete

Candidates to archive, not necessarily immediate deletion:
- stale PE submission DTO family in `PESubmissionDto.cs` if confirmed unused
- legacy initializer artifact after migration knowledge is preserved elsewhere
- historical build log text files after their information has been captured in stable documentation
- self-host-specific docs once external managed-provider migration is complete

## 17. Focused Findings

1. **Current full solution builds locally.**
   - This is verified from user-supplied `dotnet build .\APE_Core.sln` output.

2. **Current API startup path works locally.**
   - Startup, DB/AI initialization, worker startup, and listen URL were all reported successful by the user.

3. **Sandbox build failure is environmental, not source-based.**
   - Codex sandbox cannot access NuGet signature endpoint, causing `NU1301`.

4. **Current PE grading flow is functional but still shaped around Judge0 semantics.**
   - The application boundary hides HTTP, but `StatusId` and request/response fields remain Judge0-specific.

5. **Atomic claim exists, lease recovery does not.**
   - Pending claims are safe against double-claim.
   - Reclaiming stuck `Processing` work is not modeled robustly.

6. **Restart recovery is partial.**
   - Resumes if execution tokens were already persisted.
   - Does not solve pre-persist crash window after provider batch creation.

7. **Index provisioning is not present in active runtime code.**
   - Current `DbInitializer.cs` is empty.
   - Legacy index definitions exist only in `.legacy.txt`.

8. **Test coverage for PE grading is effectively absent.**
   - Repository contains one active trivial passing test.
   - PE grading tests are commented out or stale.

9. **Documentation and code are materially out of sync.**
   - Especially around PE submission schema and the managed Judge design target.

10. **Default configuration still points to localhost Judge0.**
    - This conflicts with the stated migration away from self-host assumptions.

## 18. Unresolved Questions Supported by Code Evidence

1. Are the old Mongo indexes still present in the live database?
   - Code evidence: current `DbInitializer.cs` does not create them.
   - Unknown because live DB contents were not inspected.

2. Is `Application/DTOs/PESubmissionDto.cs` still used anywhere meaningful?
   - Code evidence: active controller path uses `SubmissionDtos.cs` models instead.
   - The file looks stale but was not proven dead purely by this audit.

3. Is provider ordering of batch results guaranteed strongly enough for current evaluator assumptions?
   - Code evidence: evaluator assumes `executionResults[index]` corresponds to `question.TestCases[index]`.
   - Client reconstructs results by token order, which is better than raw provider list order, but token-to-case mapping is implicit by original request order rather than explicit case ids.

4. Is sending `expected_output` to the provider a hard product requirement or just inherited Judge0 behavior?
   - Code evidence: current `Judge0SubmissionRequest` includes `expected_output`.
   - The managed-provider design guide recommends APE-local result comparison.

5. Is multi-file language support intentionally limited to C and Java?
   - Code evidence: `Judge0MultiFileProfileResolver` supports only language ids `50` and `62`.
   - Any future managed provider migration may need broader support.

6. Are secrets intentionally committed for local development, or should these be moved fully out of repo?
   - Code evidence: concrete MongoDB and JWT values are present in `API/appsettings.json`.

## 19. Bottom Line

Current repository state is better than the legacy artifacts suggest:
- local full solution build succeeds
- local tests succeed
- local API startup succeeds
- current PE grading architecture is coherent enough to rewrite incrementally

The main rewrite pressure points are not "make it compile" but:
- remove self-host/default-local assumptions
- strengthen recovery and lease semantics
- reduce Judge0-specific leakage in the execution contract
- restore trustworthy tests around PE grading
- reconcile code, schema docs, and legacy artifacts

