# PE Code Content Validation Audit

## 1. Current PE flow

### 1.1 Run / Check Code flow

Current run endpoint exists.

| Step | Production type | File | Method / signature | Notes |
|---|---|---|---|---|
| HTTP entry | `SubmissionController` | [API/Controllers/SubmissionController.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/API/Controllers/SubmissionController.cs) | `Task<IActionResult> RunPE(PECodeRunInputDto request, CancellationToken cancellationToken)` | Route: `POST api/student/submissions/pe/run` |
| Request DTO | `PECodeRunInputDto` | [Application/DTOs/SubmissionDtos.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/DTOs/SubmissionDtos.cs) | `QuestionId`, `Stdin`, `Files` | No session id, no persisted run id |
| Question load | `IPEQuestionRepository` | [Application/Interfaces/IPEQuestionRepository.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Interfaces/IPEQuestionRepository.cs) | `Task<PEQuestion?> GetByIdAsync(...)` | Directly called by controller |
| Execution contract build | `CodeExecutionBatchRequest` | [Application/Models/CodeExecutionBatchRequest.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Models/CodeExecutionBatchRequest.cs) | `static Create(...)` | Built inline in controller |
| Provider call | `ICodeExecutionClient` | [Application/Interfaces/ICodeExecutionClient.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Interfaces/ICodeExecutionClient.cs) | `CreateBatchAsync`, `GetBatchResultsAsync` | Controller polls inline |
| Result mapping | `SubmissionController` private helpers | [API/Controllers/SubmissionController.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/API/Controllers/SubmissionController.cs) | `ResolveRunVerdict`, `MapExecutionStatus`, `NormalizeOutput` | Uses visible sample cases only |
| Response DTO | `PECodeRunResultDto` | [Application/DTOs/SubmissionDtos.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/DTOs/SubmissionDtos.cs) | sample results + optional custom run result | Returns visible expected output |

Observed behavior from implementation:

- `RunPE` is synchronous and controller-heavy.
- No `PESubmissionService` involvement.
- No `PE_Submission` document is created.
- No Mongo persistence for run code, run verdict, or run-time content validation.
- It only executes visible sample cases plus optional custom stdin.
- It ignores requested language selection and currently resolves language with `question.ResolveLanguageId(null)`.

### 1.2 Submit flow

| Step | Production type | File | Method / signature | Notes |
|---|---|---|---|---|
| HTTP entry | `SubmissionController` | [API/Controllers/SubmissionController.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/API/Controllers/SubmissionController.cs) | `Task<IActionResult> SubmitPE(PESubmissionInputDto request, CancellationToken cancellationToken)` | Route: `POST api/student/submissions/pe` |
| Request DTO | `PESubmissionInputDto` | [Application/DTOs/SubmissionDtos.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/DTOs/SubmissionDtos.cs) | `SessionId`, `QuestionId`, `RequestedLanguageId`, `Files` | Multi-file capable |
| Application service | `PESubmissionService` | [Application/Services/PESubmissionService.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Services/PESubmissionService.cs) | `Task<ApiResponse<SubmissionAcceptedDto>> SubmitAsync(...)` | Validates session, exam, question, language |
| Aggregate create | `PE_Submission` | [Domain/Entities/PE_Submission.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Domain/Entities/PE_Submission.cs) | `static Create(...)` | Starts in `Pending` |
| Repository | `IPESubmissionRepository` / `PESubmissionRepository` | [Application/Interfaces/IPESubmissionRepository.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Interfaces/IPESubmissionRepository.cs), [Infrastructure/Persistence/PESubmissionRepository.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Infrastructure/Persistence/PESubmissionRepository.cs) | `GetLatestAttemptNumberAsync`, `TryCreateAsync` | Attempt allocation uses read-then-insert retry |
| Response DTO | `SubmissionAcceptedDto` | [Application/DTOs/SubmissionDtos.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/DTOs/SubmissionDtos.cs) | submission id, status, attempt, submittedAt | Returns `202 Accepted` |

Observed behavior from implementation:

- Student code is persisted in `PE_Submission.SubmittedCode`.
- `SubmissionAttemptNumber` is `PE_Submission.AttemptCount`.
- Initial aggregate status is `Pending`.
- Attempt allocation uses `GetLatestAttemptNumberAsync + 1` and retries `TryCreateAsync` up to 3 times on unique-index conflict.

### 1.3 Background grading flow

| Step | Production type | File | Method / signature | Notes |
|---|---|---|---|---|
| Worker | `Judge0Worker` | [API/Workers/Judge0Worker.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/API/Workers/Judge0Worker.cs) | hosted background service | Registered in `Program.cs` |
| Queue source | `IPESubmissionRepository` | [Application/Interfaces/IPESubmissionRepository.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Interfaces/IPESubmissionRepository.cs) | `GetByStatusesAsync`, `TryAcquirePendingAsync`, `TryReclaimExpiredProcessingAsync` | Mongo-backed polling worker |
| Processor | `SubmissionGradingProcessor` | [Application/Services/SubmissionGradingProcessor.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Services/SubmissionGradingProcessor.cs) | `Task ProcessAsync(string submissionId, string leaseOwner, CancellationToken cancellationToken = default)` | Lease-aware grading orchestration |
| Provider | `ICodeExecutionClient` / `Judge0Client` | [Application/Interfaces/ICodeExecutionClient.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Interfaces/ICodeExecutionClient.cs), [Infrastructure/Judge0/Judge0Client.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Infrastructure/Judge0/Judge0Client.cs) | create batch + poll batch | Polling, not callback |
| Result evaluator | `SubmissionResultEvaluator` | [Application/Services/SubmissionResultEvaluator.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Services/SubmissionResultEvaluator.cs) | `SubmissionEvaluation Evaluate(...)` | Final verdict decided in APE |
| Terminal persistence | `PESubmissionRepository` | [Infrastructure/Persistence/PESubmissionRepository.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Infrastructure/Persistence/PESubmissionRepository.cs) | `TryPersistExecutionStateAsync`, `TryPersistTerminalStateAsync` | CAS on active lease |

Current async state flow:

`Pending -> Processing -> Completed`

or

`Pending -> Processing -> Failed`

Manual retry exists only from `Failed -> Pending` via aggregate method `ResetForManualRetry()`. No code path in current HTTP flow was found that exposes manual retry for students.

### 1.4 Result retrieval flow

| Endpoint | Production type | File | Method | Notes |
|---|---|---|---|---|
| `GET api/student/submissions/pe/{id}` | `SubmissionController` | [API/Controllers/SubmissionController.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/API/Controllers/SubmissionController.cs) | `GetPESubmission(...)` | Returns masked hidden case details |
| `GET api/student/submissions/pe/session/{sessionId}` | `SubmissionController` | same | `GetPESubmissionsBySession(...)` | Returns summaries |
| Detail DTO | `PESubmissionDetailDto` | [Application/DTOs/SubmissionDtos.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/DTOs/SubmissionDtos.cs) | includes `SubmittedCode`, `TestResults` | Hidden test data nulled |

### 1.5 AI Mentor flow

| Step | Production type | File | Method / signature | Notes |
|---|---|---|---|---|
| HTTP entry | `SubmissionController` | [API/Controllers/SubmissionController.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/API/Controllers/SubmissionController.cs) | `RequestCodeMentor`, `GetLatestMentorFeedback`, `GetMentorFeedbackHistory` | Separate from grading |
| Service | `CodeMentorService` | [Application/Services/CodeMentorService.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Application/Services/CodeMentorService.cs) | `RunAsync(...)` | Loads submission, question, course |
| Prompt/artifacts | `DbFirstAIArtifactCatalogService` | [Infrastructure/Services/DbFirstAIArtifactCatalogService.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Infrastructure/Services/DbFirstAIArtifactCatalogService.cs) | `GetPromptAsync`, `GetArtifactAsync` | DB-first with file fallback outside prod |
| Feedback persistence | `IAIMentorFeedbackRepository`, `IAIUsageLogRepository` | repository registrations in [API/Program.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/API/Program.cs) | stored after AI call | Advisory only |

Observed behavior:

- AI Mentor is manually requested, not automatically triggered by grading.
- It receives source code, problem statement, policy, rubric.
- It does not currently build or inject a deterministic Judge0 evidence object even though the `code_mentor` prompt template contains `{{judge0_evidence_json}}`.
- Mentor output does not affect submission pass/fail.

## 2. Exact integration points

### 2.1 Best current insertion points for a Code Requirement Validator

1. `SubmissionController.RunPE(...)`
   - Best fit for fast synchronous feedback when student presses Run.
   - Current downside: no persistence, no audit trail, no reuse by async grading.

2. `PESubmissionService.SubmitAsync(...)`
   - Best fit for validating whether the submitted code package is structurally eligible before enqueueing grading.
   - Good place to persist validation request/result if a new submission field is introduced.

3. `SubmissionGradingProcessor.ProcessAsync(...)`
   - Best fit for async or heavier validation that may share lifecycle with provider grading.
   - Already has retry, lease, failure handling, and terminal persistence.

4. `CodeMentorService.RunAsync(...)`
   - Best fit only for surfacing deterministic rule violations in advisory feedback.
   - Not suitable as source of truth for pass/fail because current mentor path is optional and AI-based.

### 2.2 Seams already present in provider contract

- `CodeExecutionBatchRequest`
- `CodeExecutionCaseRequest`
- `CodeExecutionBatchReceipt`
- `CodeExecutionResult`
- `CodeExecutionState`

These are provider-neutral application contracts and are the right seam if validation ever needs structural harness execution through Judge0.

## 3. Existing reusable data

### 3.1 `PEQuestion` data already persisted

From [Domain/Entities/PEQuestion.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Domain/Entities/PEQuestion.cs):

- `CourseId`
- `SourceDocumentIds`
- `LegacyChunkIds`
- `TopicTags`
- `Difficulty`
- `Title`
- `Description`
- `SkeletonCode`
- `SolutionCode`
- `TestCases`
- `Hints`
- `AllowedLanguageIds`
- `DefaultLanguageId`
- `Status`
- `IsPublic`
- `Source`
- `SourceScope`
- `OwnerUserId`
- `CreatedBy`
- `CreatedAt`
- `QuestionFingerprint`
- `LastModifiedBy`
- `LastModifiedAt`
- `DisabledReason`
- `DisabledBy`
- `DisabledAt`

From [Domain/Entities/CommonTypes.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Domain/Entities/CommonTypes.cs):

- `CodeFile`: `Filename`, `Content`, `IsReadOnly`
- `TestCase`: `Input`, `ExpectedOutput`, `IsHidden`, `IsSample`, `TimeLimitMs`, `MemoryLimitKb`

### 3.2 `PE_Submission` data already persisted

From [Domain/Entities/PE_Submission.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Domain/Entities/PE_Submission.cs):

- submitted source files
- language id
- topic tags
- attempt count
- timestamps
- processing status
- provider execution tracking
- provider token mapping
- verdict
- score
- runtime and memory
- per-test results
- processing error

This means the repository already persists enough execution context for:

- deterministic re-checking after submit
- audit of final hidden/visible result outcomes
- future storage of validation results if new fields are added

### 3.3 AI artifact data already persisted

From [Infrastructure/Data/DbContext.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Infrastructure/Data/DbContext.cs) and [Infrastructure/Data/AIArtifactDbSeeder.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/Infrastructure/Data/AIArtifactDbSeeder.cs):

- `AIRuleArtifacts` collection exists.
- Prompt/policy/rubric artifacts are seeded into Mongo.
- `DbFirstAIArtifactCatalogService` reads prompt/policy/rubric from Mongo first.

Reusable artifacts exist for AI generation/review/mentor flows, including:

- `question-generation-review-policy.json`
- `code-mentor-policy.json`
- `ai-rubrics/JAVA_OOP_PE.json`
- `ai-rubrics/C_PE.json`

### 3.4 What is reusable for validator MVP

Reusable today:

- `PEQuestion.Description`
- `PEQuestion.TopicTags`
- `PEQuestion.SkeletonCode`
- `PEQuestion.SolutionCode`
- `PEQuestion.AllowedLanguageIds`
- `PEQuestion.TestCases`
- `PE_Submission.SubmittedCode`
- multi-file Judge0 packaging via `additional_files`
- hidden/visible testcase flags

Not yet reusable as machine-executable requirement metadata:

- no explicit per-question contract like `RequiredConstructs`, `ForbiddenApis`, `StructuralRequirements`, `AntiCheatRules`, `ValidatorProfile`

## 4. Missing data / contracts

### 4.1 Missing per-question machine-readable requirement contract

No current `PEQuestion` field was found that explicitly stores:

- required `for` / `while`
- required function / array usage
- anti constant-output heuristic
- required class / private field / constructor
- inheritance / interface / override requirements
- forbidden built-in sort
- required linked-node / stack / queue structure

Current repository stores descriptive/question-generation metadata, but not a validation contract the runtime can deterministically execute for each question.

### 4.2 Missing run-time persistence for RunPE

`RunPE` currently does not persist:

- source code snapshot
- validation result
- provider tokens
- rule violations
- audit trail

So any validator added only in `RunPE` would currently be ephemeral unless a new persistence model is added.

### 4.3 Missing explicit bridge between generation rules and runtime validation

AI generation/review artifacts exist, but the runtime submission flow does not fetch a per-question executable requirement profile from Mongo.

That means:

- generation-side rubric/policy can influence how questions are written
- but grading/run-time code cannot deterministically re-check those same requirements without a new contract or extraction step

### 4.4 Missing Judge0 evidence object for mentor prompt

The prompt template expects `judge0_evidence_json`, but `CodeMentorService` does not pass it. That weakens any plan that wants mentor and validator to align around the same deterministic evidence.

## 5. Feasibility matrix

| Capability | Existing support | Required changes | Complexity | Risk | Recommended for MVP |
|---|---|---|---|---|---|
| C require `for` / `while` / function / array presence | No explicit rule metadata; code text available | Add requirement metadata + source scan | Medium | Medium bypass risk | Yes |
| Constant-output heuristic | Submitted code and testcase data exist | New heuristic contract + scanner or harness | Medium | High false positive/false negative risk | Limited |
| Java required class presence | Multi-file Java files available | Add metadata + source scan/AST | Medium | Medium | Yes |
| Java private field presence | Java source available | Add metadata + source scan/AST | Medium | Medium | Yes |
| Java constructor presence | Java source available | Add metadata + source scan/AST | Medium | Medium | Yes |
| Java inheritance requirement | Java source available | Add metadata + source scan/AST | Medium | Medium | Yes |
| Java interface implementation | Java source available | Add metadata + source scan/AST | Medium | Medium | Yes |
| Java method override | Java source available | Add metadata + source scan/AST | Medium | Medium-high | Partial |
| Forbidden Java API (e.g. sort) | Source text available | Add forbidden API metadata + source scan | Low-Medium | Medium bypass via aliases/wrappers | Yes |
| Custom Node structure | Multi-file Java supported | Add metadata + AST or harness | High | High | No |
| Custom stack / queue structure | Multi-file Java supported | Add metadata + AST or harness | High | High | No |
| Loop actually affects output | Judge0 execution exists | New harness strategy + metadata | High | High | No |
| Polymorphism actually used at runtime | Multi-file Java + Judge0 available | Structural harness or reflection harness + metadata | High | High | No |
| Algorithm complexity | No static analyzer; Judge0 runtime only | New analyzer or benchmark strategy | High | Very high | No |

## 6. Option comparison

### Option A: Regex / simple source scanning

How it fits current code:

- easiest to insert into `RunPE` and `SubmitAsync`
- no new infrastructure service is required
- directly uses existing `CodeFile.Content`

Strengths:

- fastest MVP path
- low latency for Run
- works without touching Judge0
- good for filename/class/keyword/forbidden API checks

Weaknesses:

- easy to bypass with comments, dead code, aliases, formatting tricks
- weak for semantic Java OOP checks
- weak for anti-constant-output and algorithm/data-structure intent

Fit by language:

- C: acceptable for basic construct presence
- Java: acceptable for class/member keywords, weaker for semantics

Architecture impact:

- low

### Option B: AST / parser service

How it fits current code:

- no current parser dependency was found in the solution
- would require new dependencies/services/contracts

Strengths:

- much better semantic precision
- stronger for Java OOP structure rules
- stronger for forbidden API and member-level checks

Weaknesses:

- new infrastructure and dependency footprint
- language-specific parser maintenance
- higher delivery risk for three-month project scope

Fit by language:

- C: possible but needs parser introduction
- Java: strong, but not currently present

Architecture impact:

- medium to high

### Option C: Structural test harness via Judge0

How it fits current code:

- current `Judge0SourcePackageBuilder` already supports multi-file `additional_files`
- Java and C multi-file packaging already exist
- this makes harness-based validation architecturally possible

Strengths:

- can validate runtime-observable structure/use more robustly than regex
- could support reflection or compile-time harness checks for Java
- reuses existing external sandbox

Weaknesses:

- higher latency, especially for Run
- requires generating/sending harness artifacts
- requires careful security/visibility handling
- still needs machine-readable metadata to know which harness to generate

Fit by language:

- C: possible for some harness patterns
- Java: possible for reflection/structure checks if entrypoint/profile constraints are respected

Architecture impact:

- medium

### Recommended direction from current codebase

Best current fit is a hybrid:

- Option A for synchronous MVP validation in `RunPE` and/or pre-submit validation
- selective Option C later for stronger Java structural scenarios
- Option B only if the project accepts new parser dependencies and broader refactor scope

## 7. Risks and limitations

1. **Main blocker**: no persisted per-question machine-readable code requirement metadata.
2. `RunPE` is not persisted, so validator results there are transient only.
3. `RunPE` logic is controller-local, which makes shared reuse with submit grading awkward.
4. Java multi-file flow effectively assumes Judge0 multi-file packaging with `Main.java` entry conventions; complex package-based structures may need extra design.
5. `PEQuestion.SolutionCode` is persisted, but current runtime flow does not use it for validator contracts; sending it externally would raise security and leakage risk.
6. AI generation rubrics exist, but they are not equivalent to deterministic runtime validation rules.
7. Committed secrets exist in `appsettings*.json`; this is a deployment/security blocker, though not a logic blocker for validator design.
8. Current attempt allocation is still read-then-insert retry, so any future validation persistence added before insert must avoid widening that race.

## 8. Minimal viable scope

Recommended MVP from current architecture:

1. Add per-question machine-readable requirement metadata in `PEQuestion` or linked artifact.
2. Start with synchronous source-based validation for:
   - required keywords/constructs
   - required filenames/class names
   - forbidden APIs
3. Surface this validator first on `RunPE` for quick feedback.
4. Reuse the same validator from `SubmitAsync` or `SubmissionGradingProcessor` so final grading can record deterministic violations.
5. Keep advanced semantic/runtime-structure validation out of MVP:
   - true anti-constant-output proof
   - actual polymorphism usage
   - custom data-structure proof
   - complexity proof

MVP feasibility based on current implementation: **Medium**.

Reason:

- execution and persistence seams are good
- multi-file provider support exists
- but the essential requirement metadata contract does not yet exist

## 9. Open questions

1. Should code-requirement violations be advisory on `RunPE`, blocking on `Submit`, or both?
2. Should requirement metadata live directly on `PEQuestion` or in `AIRuleArtifacts` linked by stable key?
3. For Java structural rules, is `Main.java` + no package declaration an acceptable product constraint?
4. Should validator output become part of `PE_Submission` history for audit and mentor reuse?
5. Should mentor consume deterministic validator/Judge0 evidence in a shared normalized payload?
6. Is the product goal to check surface presence only, or to enforce semantic usage strongly enough to justify a later AST/harness phase?

## 10. Tests currently relevant to this audit

Relevant automated evidence found:

- [BE_UnitTests/SubmissionControllerCodingPracticeTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/SubmissionControllerCodingPracticeTests.cs)
  - submit/detail/session routes
  - no meaningful `RunPE` coverage found here
- [BE_UnitTests/PESubmissionServiceCodingPracticeTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/PESubmissionServiceCodingPracticeTests.cs)
  - submit validation
  - hidden result masking
- [BE_UnitTests/Judge0ClientAdapterTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/Judge0ClientAdapterTests.cs)
  - confirms expected output is omitted from provider payload
  - confirms token/result mapping and safe logging
  - confirms DI/config behavior
- [BE_UnitTests/CodeExecutionContractTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/CodeExecutionContractTests.cs)
  - contract invariants and evaluator behavior
- [BE_UnitTests/SubmissionGradingLeaseFlowTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/SubmissionGradingLeaseFlowTests.cs)
  - lease-aware grading flow
  - legacy token compatibility
- [BE_UnitTests/SubmissionGradingReliabilityTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/SubmissionGradingReliabilityTests.cs)
  - deadline, retry, renewal, polling behavior
- [BE_UnitTests/CodeMentorServiceTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/CodeMentorServiceTests.cs)
  - mentor request/ownership/quota/persistence
- [BE_UnitTests/QuestionGenerationReviewServiceTests.cs](/abs/path/C:/Users/FPT%20SHOP/Desktop/APE_2/APE_BE/BE_UnitTests/QuestionGenerationReviewServiceTests.cs)
  - generation/review artifacts and source-scope behavior

Coverage gaps relevant to validator planning:

- no dedicated automated test coverage for current `RunPE` execution semantics
- no tests for machine-readable code requirement metadata because that feature does not exist yet
- no tests proving current mentor prompt receives Judge0 evidence, because current service does not pass it

## 11. Bottom line

- **Run endpoint** exists and is the most natural place for quick validator feedback, but it is synchronous and non-persistent.
- **Submit + worker** flow is the most natural place for auditable final validation, because it already persists code, execution, and results.
- **Judge0 integration** is already strong enough to support future harness-based checks, especially for multi-file Java/C, but it still needs an explicit requirement contract to drive those checks.
- **Current code requirement metadata persisted:** Partial.
  - descriptive/question-generation-related metadata exists
  - executable per-question validation metadata does not
