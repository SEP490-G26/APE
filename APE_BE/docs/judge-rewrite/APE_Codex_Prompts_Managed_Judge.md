# APE — Codex Prompts cho Managed Judge Workflow

> Dán **một prompt mỗi lần** vào Codex trong VS Code.
>
> Không chạy prompt implementation tiếp theo nếu phase hiện tại chưa build/test xanh.
>
> Codex phải inspect code thật, không được dựa mù quáng vào tài liệu.

---

# P00 — Audit only, không sửa source

```text
You are acting as a senior .NET 8 backend architect auditing the APE repository.

Repository context:
- Solution: APE_Core.sln
- Clean Architecture projects: API, Application, Domain, Infrastructure
- MongoDB persistence
- PE submissions are graded asynchronously
- The repository may contain an existing Judge0 workflow, merge leftovers, duplicate services, stale documentation, and nullable warnings
- We are replacing self-hosted Judge0 assumptions with a managed external Judge0-compatible provider
- Do not assume this summary is accurate; inspect the actual repository

Task:
1. Inspect all code related to:
   - PE_Submission
   - PEQuestion, TestCase, CodeFile
   - PESubmissionService
   - SubmissionGradingProcessor
   - SubmissionResultEvaluator
   - Judge0Worker or other background workers
   - ICodeExecutionClient or equivalent
   - Judge0 clients, contracts, options, and DI
   - PESubmissionRepository and Mongo indexes
   - Program.cs
   - appsettings files
   - tests
2. Create docs/judge-rewrite/00_CURRENT_STATE_AUDIT.md.
3. Include:
   - exact files and types
   - current end-to-end workflow
   - dependency direction
   - current state transitions
   - provider request/response models
   - retry and polling behavior
   - atomic claim behavior
   - restart recovery behavior
   - configuration and secrets
   - duplicated or legacy implementations
   - relevant compile errors and warnings
   - current test coverage
   - code versus documentation differences
   - keep/replace/delete recommendations
4. Run dotnet build on the solution and record the result.
5. Do not edit any .cs, .csproj, json, or runtime configuration file.
6. Do not fix issues yet.

Chat output:
- concise audit summary
- build result
- created document path
- unresolved questions supported by code evidence
```

---

# P01 — Architecture document only

```text
Read:
- docs/judge-rewrite/00_CURRENT_STATE_AUDIT.md
- Domain/Entities/PE_Submission.cs
- current code-execution implementation
- repository interfaces and worker

Create docs/judge-rewrite/01_MANAGED_JUDGE_ARCHITECTURE.md only.
Do not change source code.

Target workflow:
Submission API
-> PESubmissionService
-> Mongo Pending submission
-> grading worker
-> atomic lease
-> SubmissionGradingProcessor
-> provider-neutral ICodeExecutionClient
-> managed Judge0 adapter
-> persist execution tokens
-> poll results
-> SubmissionResultEvaluator
-> Completed or Failed.

Document:
- responsibilities by layer
- actual types to retain
- proposed provider-neutral contracts
- state machine
- processing lease
- token-to-testcase mapping
- idempotency and failure windows
- transient versus permanent errors
- polling deadline
- hidden testcase policy
- configuration and secrets
- migration from current code
- MVP deployment with one ECS task
- optional future SQS worker
- explicit non-goals

Do not invent a new class if an equivalent already exists. Identify proposed renames separately.
```

---

# P02 — Strengthen PE_Submission domain model

```text
Act as a domain-model specialist.

Inspect:
- docs/judge-rewrite/00_CURRENT_STATE_AUDIT.md
- docs/judge-rewrite/01_MANAGED_JUDGE_ARCHITECTURE.md
- Domain/Entities/PE_Submission.cs
- Domain enums and exceptions
- Mongo serialization requirements
- existing domain tests

Goal:
Strengthen the PE_Submission model for a resumable managed code-execution workflow without changing unrelated entities.

Required capabilities:
- Pending -> Processing -> Completed/Failed
- lease owner, acquired time, and expiry time
- provider name
- execution tokens mapped to testcase CaseIndex
- prevent duplicate token values
- prevent duplicate CaseIndex values
- attach tokens only while Processing
- complete or fail only through valid transitions
- explicit manual retry/reset behavior
- UTC validation consistent with the project
- MongoDB deserialization remains supported
- no HTTP or Judge0 status IDs in Domain

Instructions:
1. Show a minimal change plan before editing.
2. Reuse existing value objects and methods where possible.
3. Do not add public setters only to satisfy MongoDB.
4. Add focused unit tests for every invariant.
5. Do not edit HTTP client code.
6. Build and test Domain/Application.
7. Report all changed files and persistence migration concerns.

Acceptance:
- invalid transitions use the project-standard domain exception
- completed and failed submissions cannot be claimed normally
- token mapping is deterministic by CaseIndex
- no nullable warning suppression
```

---

# P03 — Provider-neutral Application contracts

```text
Inspect the completed domain changes and all existing code-execution interfaces/models.

Goal:
Make Application independent from Judge0-specific HTTP contracts.

Create or refactor the minimum required contracts:
- ICodeExecutionClient
- CodeExecutionBatchRequest
- CodeExecutionCase
- CodeExecutionBatchReceipt
- CodeExecutionToken
- CodeExecutionResult
- CodeExecutionState
- CodeExecutionProviderHealth
- CodeExecutionClientException

Constraints:
- Application must not reference Infrastructure.
- Do not expose Judge0 status_id outside Infrastructure.
- Include testcase CaseIndex in requests, tokens, and results.
- Prefer not sending ExpectedOutput to the external provider; APE compares output.
- Reuse current interfaces if they already satisfy the requirement.
- Do not implement the HTTP adapter in this task.
- Add validation and focused tests.
- Build Domain and Application.

Before editing:
- show the file-level plan.

After editing:
- list changed files;
- list breaking contract changes;
- list call sites that still need migration.
```

---

# P04 — Managed Judge0 HTTP adapter

```text
Act as an infrastructure integration engineer.

Read:
- audit and architecture documents
- provider-neutral Application contracts
- existing Judge0 code
- existing tests
- the provider's documented batch API behavior

Implement a managed Judge0 adapter in Infrastructure.

Requirements:
1. Implement ICodeExecutionClient using a typed HttpClient.
2. Configuration section: CodeExecution:Judge0.
3. Options:
   - BaseUrl
   - ProviderName
   - TimeoutSeconds
   - MaximumBatchSize
   - configurable Headers dictionary
4. Do not hard-code localhost, X-Auth-Token, RapidAPI headers, or any secret.
5. Create submissions using the batch endpoint.
6. Retrieve results using batch tokens.
7. Use Base64 consistently for source, stdin, and returned output.
8. Never send PEQuestion.SolutionCode.
9. Prefer not sending ExpectedOutput. If the existing implementation sends it, remove it unless a verified provider requirement prevents that.
10. Map provider statuses to Application CodeExecutionState only inside Infrastructure.
11. Validate:
    - request batch size
    - response item count
    - missing tokens
    - duplicate tokens
    - malformed Base64
    - unknown statuses
12. Error classification:
    - 400/401/403/404/422 = permanent
    - 408/429/5xx/network timeout = transient
    - parse Retry-After
13. Never log headers, API keys, source, stdin, expected output, or hidden test data.
14. Add tests with a fake HttpMessageHandler. Do not call a live provider.
15. Register through an Infrastructure DI extension.
16. Run Infrastructure tests and full solution build.

Do not modify worker orchestration in this task.
```

---

# P05 — MongoDB atomic processing lease

```text
Act as a MongoDB concurrency specialist.

Inspect:
- PE_Submission persistence mapping
- IPESubmissionRepository
- PESubmissionRepository
- existing atomic claim logic
- indexes
- integration test setup

Implement a lease-based atomic processing contract.

Required behavior:
- query Pending and expired Processing candidates
- atomic acquire by submission ID
- renew only when the same WorkerId owns the lease
- active lease cannot be stolen
- expired lease can be reclaimed
- Completed and Failed cannot be automatically claimed
- all operations accept CancellationToken
- all timestamps are UTC
- nullable repository results use correct async/await semantics

Suggested operations:
- GetProcessingCandidatesAsync
- TryAcquireForProcessingAsync
- RenewLeaseAsync
- UpdateAsync

Concurrency test:
Run two acquire operations at the same time; exactly one must succeed.

Add or document indexes:
- Status + LeaseExpiresAt + SubmittedAt
- SessionId + QuestionId + AttemptNumber

Do not expose IMongoCollection outside Infrastructure.
Do not make grading decisions inside the repository.
Run build and tests.
```

---

# P06 — SubmissionGradingProcessor

```text
Act as an application orchestration specialist.

Inspect:
- PE_Submission domain methods
- provider-neutral ICodeExecutionClient
- repository lease operations
- PEQuestion and TestCase
- SubmissionResultEvaluator
- GradingWorker options

Implement or refactor SubmissionGradingProcessor.

Rules:
1. Load submission and verify Processing status and lease ownership.
2. Load PEQuestion.
3. Treat missing question or empty testcases as permanent failure.
4. If tokens already exist:
   - do not call CreateBatchAsync;
   - resume polling.
5. If tokens do not exist:
   - build provider-neutral request;
   - call CreateBatchAsync;
   - validate complete CaseIndex coverage;
   - persist tokens immediately.
6. Poll until:
   - all results are terminal;
   - absolute deadline;
   - cancellation.
7. Renew lease during long processing.
8. Retry transient errors with bounded exponential backoff and jitter.
9. Honor RetryAfter when available.
10. Do not retry permanent errors.
11. Use SubmissionResultEvaluator for final verdict and score.
12. Persist Completed or Failed.
13. Never log source code, hidden input, expected output, hidden actual output, or secrets.
14. Use TimeProvider and a testable delay abstraction if needed.
15. Do not modify controllers.

Required tests:
- create batch then complete
- resume existing tokens
- 429 then success
- 401 permanent failure
- 5xx retries exhausted
- polling deadline
- cancellation
- missing result
- duplicate CaseIndex
- lease lost during processing
```

---

# P07 — Thin grading worker

```text
Act as a background-processing engineer.

Inspect the existing Judge0Worker and the new lease/processor contracts.

Goal:
Keep the worker thin, durable, and safe.

Requirements:
- generate a unique WorkerId for the process
- use IServiceScopeFactory
- query a bounded candidate batch
- atomically acquire each submission
- call ISubmissionGradingProcessor
- isolate exceptions per submission
- respect graceful shutdown
- do not convert shutdown cancellation into grading failure
- idle delay when no work
- structured logs with SubmissionId and WorkerId
- no Judge0 HTTP details
- no scoring logic
- no direct Mongo collection access
- no in-memory Channel as the only durable source
- worker enable/disable option
- add focused tests where practical

Run full build and tests.
Report whether multiple ECS tasks are now safe; if not, state the exact remaining limitation.
```

---

# P08 — API and hidden-result safety

```text
Inspect:
- SubmissionController
- PESubmissionService
- submission DTOs and mappings
- authentication claim conventions
- ApiResponse
- ExceptionMiddleware

Implement the minimum API changes for the async managed workflow.

Requirements:
- valid PE submit persists Pending and returns HTTP 202 Accepted
- response includes SubmissionId and ProcessingStatus
- GET submission checks student ownership or explicit admin policy
- Pending, Processing, Completed, and Failed are representable
- completed response is sanitized
- hidden testcase input, expected output, and actual output are null or absent
- no SolutionCode
- provider tokens are not exposed to students
- preserve existing ApiResponse conventions
- keep controllers thin
- update OpenAPI examples if present
- add tests
- do not change unrelated routes
```

---

# P09 — DI, options, and AWS secret readiness

```text
Inspect Program.cs and all DI extension classes.

Refactor only grading/code-execution registration into project-consistent extension methods, for example:
- AddManagedCodeExecution(configuration)
- AddGradingWorkflow(configuration)

Requirements:
- one ICodeExecutionClient registration
- one hosted worker registration
- startup options validation
- BaseUrl required when worker is enabled
- no committed secret
- configurable headers through .NET configuration
- appsettings contains non-secret defaults only
- environment-variable binding supports:
  CodeExecution__Judge0__BaseUrl
  CodeExecution__Judge0__Headers__<HeaderName>
- AWS ECS can inject Secrets Manager values as environment variables
- do not add AWS SDK only to read ECS-injected secrets
- reduce Program.cs only within this module
- build full solution

Create:
docs/judge-rewrite/02_CONFIGURATION_AND_SECRETS.md

Document:
- local User Secrets
- environment variables
- ECS Secrets Manager injection
- secret rotation requiring new task/deployment
```

---

# P10 — Full workflow tests

```text
Add or complete tests for the managed grading workflow.

Required matrix:
- Accepted
- Wrong Answer
- Compilation Error
- Runtime Error
- Time Limit Exceeded
- Provider Internal Error
- 429 then success
- 5xx retries exhausted
- 401 immediate failure
- malformed JSON
- malformed Base64
- missing token
- duplicate token
- missing CaseIndex
- duplicate CaseIndex
- polling deadline
- cancellation
- existing-token restart/resume
- concurrent claim
- expired lease recovery
- active lease protection
- hidden testcase DTO
- unauthorized access

Rules:
- no live Judge0 calls in automated tests
- no long real Thread.Sleep/Task.Delay
- use fake time/delay where possible
- tests must be deterministic
- run dotnet build and dotnet test for the solution
- report uncovered behavior
```

---

# P11 — Controlled legacy cleanup

```text
The new workflow is implemented. Perform controlled cleanup.

First run build and tests. If not green, do not delete legacy code.

Search for:
- old Judge0Service implementations
- localhost:2358
- hard-coded authentication headers
- GradingJob or Channel queue
- duplicate workers
- duplicate options
- old status strings
- obsolete token fields
- self-host DI registrations
- stale tests
- merge markers and build artifact text files

For each item:
- show references
- decide migrate, delete, or archive
- make the smallest safe change

Move self-host documentation to:
docs/archive/judge0-self-host/

Add:
Deprecated — APE currently uses a managed code-execution provider.

Create:
docs/judge-rewrite/03_MIGRATION_COMPLETION_REPORT.md

Include:
- removed files and types
- retained compatibility code
- configuration migration
- Mongo data migration concerns
- known limitations
- rollback plan
- final build/test result
```

---

# P12 — AWS readiness review, không deploy

```text
Act as an AWS deployment reviewer. Do not create or modify AWS resources.

Inspect:
- API Dockerfile
- health endpoints
- configuration
- logging
- worker behavior
- expected ECS task count
- MongoDB Atlas connectivity
- managed Judge0 secrets
- file storage dependencies

Target MVP:
- ECR
- ECS Express Mode/Fargate
- one desired task
- MongoDB Atlas
- AWS Secrets Manager
- CloudWatch Logs
- managed Judge0 provider

Create:
docs/deployment/AWS_MANAGED_JUDGE_READINESS.md

Document:
- required secret variables
- non-secret variables
- IAM permissions
- container port
- health check path
- startup/shutdown behavior
- outbound network requirements
- log configuration
- deployment steps
- rollback steps
- why desired count starts at 1
- exact criteria for adding SQS and a dedicated worker
- cost-control checklist

Do not add Terraform, CDK, or CloudFormation unless the repository already uses it and the user explicitly requests infrastructure as code.
```

---

# P13 — Reviewer prompt sau mỗi phase

```text
Review only the changes from the current phase as a strict senior .NET reviewer.

Check:
- Clean Architecture dependency direction
- domain invariants
- concurrency and MongoDB atomicity
- async and CancellationToken usage
- nullable reference types
- idempotency and restart recovery
- hidden data leakage
- secret leakage
- retry storms
- provider coupling
- test determinism and coverage
- duplicate DI registrations
- unrelated changes

Do not edit code.

Return:
1. blocking issues
2. important issues
3. minor issues
4. missing tests
5. exact file and line references
6. whether the phase is safe to commit
```

---

# P14 — Fix reviewed findings

```text
Address only the blocking and important findings from the previous review.

Constraints:
- do not refactor unrelated code
- preserve public contracts unless a finding requires change
- add or update tests for each fix
- run relevant tests and full solution build
- list changed files
- explain each fix
- identify any finding intentionally not fixed and why
```

---

# Prompt để Codex kiểm tra build mà không tự sửa

```text
Do not edit files.

Run:
- dotnet restore APE_Core.sln
- dotnet build APE_Core.sln --no-restore
- dotnet test APE_Core.sln --no-build

Group failures by project and root cause.
For each failure, provide:
- exact file and line
- whether it was introduced by the current phase
- minimum recommended fix
- dependency order for fixing

Do not apply fixes.
```

---

# Prompt tạo commit summary

```text
Do not edit files.

Summarize the current staged and unstaged changes for a Git commit.

Output:
- concise commit title using conventional style
- changed files grouped by layer
- behavior changed
- tests added
- configuration changes
- migration or rollback concern
- known limitation
- commands used to verify

Do not claim a test passed unless its command output confirms it.
```
