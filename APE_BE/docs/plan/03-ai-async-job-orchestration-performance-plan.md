# AI Async Job Orchestration Performance Plan

## Status

Status: deferred architecture patch.

This plan is intentionally deferred until the AI feature set is functionally stable enough that the team is no longer changing core business semantics every few days.

This patch should be opened only after:

- document ingestion flow is considered behaviorally stable
- chapter detection and downstream retrieval quality are no longer in active churn
- question generation request/response contract is stable enough for FE handoff
- billing semantics are clear enough to support reservation and settlement
- Code Mentor request shape is stable enough to be moved behind job orchestration

## Patch Summary

This patch converts the current heavy AI flows from **synchronous request-bound execution** into **DB-backed asynchronous job execution**.

Today, several AI operations still execute inside the HTTP request:

- document upload ingestion
- AI extracted content normalization and chapter detection
- chunking, embedding, and autotagging
- question generation and review
- code mentor analysis

The target architecture is:

- API receives request
- API validates and persists a job
- API returns `jobId` immediately
- background worker claims and processes the job
- worker persists progress and final result
- FE polls job state or listens for progress updates

This is not a business-logic patch. It is an execution-model patch focused on:

- performance
- scalability
- operational resilience
- user experience under long-running AI calls

## Why This Patch Exists

### Current pain

The current synchronous design creates several production risks:

1. request duration can exceed acceptable UX limits
2. multiple concurrent users can pile up long-running AI calls on the API process
3. API restart can interrupt long-running work without a durable recovery model
4. FE waits on one long HTTP response instead of a progress-driven workflow
5. concurrency control is weak at the workload level even if HTTP rate limiting exists

### Why this is not urgent right now

The project is still stabilizing AI business correctness:

- chapter detection
- retrieval grounding
- PE/FE quality
- review strictness
- mentor quality

If orchestration is moved too early, every business-logic patch becomes harder to debug, test, and roll out.

That is why this patch is explicitly positioned as a **post-stabilization performance patch**.

## Patch Scope

### In scope

- async job orchestration for heavy AI features
- durable DB-backed queue/job state
- worker claim and lease semantics
- progress tracking
- per-job-type concurrency control
- per-user concurrent-running limits
- billing reservation/settlement flow for async jobs
- FE polling contract for long-running AI actions
- retries, failure states, and resumability semantics

### Out of scope

- redesigning AI generation quality rules
- changing artifact DB-first strategy
- changing question generation business semantics
- changing chapter detection semantics
- changing judge0 grading architecture beyond integration points
- websocket-first real-time UX if polling is sufficient for phase 1

## Functional Areas Affected

This patch affects almost every layer of the system.

### Backend

- API controllers
- application services
- background workers
- DB schema and repositories
- billing flow
- AI usage logging
- ownership and authorization checks
- observability and diagnostics

### Frontend

- request flow for upload/generation/mentor
- progress UI
- result loading flow
- failure and retry UX
- refresh-safe state restoration by `jobId`

### Operations

- worker lifecycle
- deployment model
- scaling strategy
- timeout strategy
- monitoring
- dead-letter or stuck-job recovery

## Expected Impact

### Positive impact

- long AI operations stop blocking HTTP request threads
- much better behavior under multiple concurrent users
- lower timeout risk
- better crash recovery
- easier workload throttling by feature type
- better progress visibility for users
- better path to multi-instance deployment

### Negative or risky impact

- large BE refactor
- FE contract and UX changes
- billing complexity increases
- new failure modes:
  - duplicate job execution
  - stuck running jobs
  - partial billing settlement
  - stale polling state
  - race conditions around result persistence

### Change size assessment

This is a **large patch**.

Relative size estimate:

- business logic impact: medium
- architecture impact: very high
- BE refactor impact: very high
- FE impact: high
- rollout risk: high

This should be treated as a dedicated phase, not a small enhancement.

## Current Baseline

### What already exists in the repo

- background worker pattern already exists for Judge0
- MongoDB persistence exists and is already the source of truth for AI runtime configuration
- AI usage logging and billing already exist in synchronous mode
- FE already handles several AI result payloads, though mostly through direct response flows

### What is missing

- durable AI job queue for ingestion/generation/mentor
- shared claim/lease job abstraction for AI workloads
- persisted progress states for long-running AI jobs
- reservation-based async billing flow
- FE job polling contract
- per-user workload concurrency control at AI-job level

## Design Principles

1. DB-first remains mandatory.
2. Job orchestration must be durable across API restart.
3. The patch must not silently double-charge users.
4. A job must be claimable by only one worker at a time.
5. Result persistence must remain the source of truth, not in-memory worker state.
6. The first phase should prefer polling over websocket complexity unless polling proves insufficient.
7. Rollout must be phased and reversible.

## Target Architecture

## 1. Job Model

Introduce a shared AI job collection, for example `AI_Jobs`.

Each job should include at minimum:

- `JobId`
- `JobType`
  - `document_ingestion`
  - `question_generation`
  - `code_mentor`
- `SourceScope`
  - `BYOS`
  - `SYSTEM`
- `RequestedByUserId`
- `RequestedByEmail` or audit display identity if needed
- `CourseId`
- `DocumentId` if known at submit time
- `SubmissionId` if mentor job
- `PayloadJson`
- `Status`
  - `queued`
  - `running`
  - `succeeded`
  - `failed`
  - `cancelled`
- `Stage`
  - job-specific current stage
- `ProgressPercent`
- `AttemptCount`
- `WorkerId`
- `LeaseUntil`
- `Priority`
- `CreatedAt`
- `StartedAt`
- `CompletedAt`
- `ErrorCode`
- `ErrorSummary`
- `ResultRef`
- `BillingReservationRef`
- `Metadata`

## 2. Worker Model

Preferred direction:

- one shared base claim/lease abstraction
- one worker per major AI flow:
  - `DocumentIngestionWorker`
  - `QuestionGenerationWorker`
  - `CodeMentorWorker`

Alternative:

- one generic `AIJobWorker` dispatching by `JobType`

Recommended choice:

- separate workers by flow type

Reason:

- each workload has different latency, retry semantics, and progress stages

## 3. Claim And Lease

Workers must claim jobs atomically in DB:

- select eligible `queued` job
- transition to `running`
- set `WorkerId`
- set `LeaseUntil`
- increment `AttemptCount`

If a worker crashes:

- lease expires
- another worker can reclaim the job

This must be modeled like the Judge0 atomic claim approach, not by in-memory queue only.

## 4. Progress Model

Each job type must have explicit stages.

### Document ingestion stages

- `queued`
- `precheck`
- `gatekeeper`
- `extract_text_and_images`
- `vision_enrichment`
- `normalize_and_structure`
- `chapter_detection`
- `chunking`
- `embedding`
- `tagging`
- `persist_document`
- `billing_settlement`
- `completed`

### Question generation stages

- `queued`
- `precheck`
- `resolve_scope`
- `retrieve_context`
- `generate`
- `repair_if_needed`
- `rule_review`
- `llm_review`
- `refill_if_needed`
- `persist_questions`
- `billing_settlement`
- `completed`

### Code mentor stages

- `queued`
- `precheck`
- `load_submission`
- `collect_judge0_evidence`
- `mentor_analysis`
- `persist_feedback`
- `billing_settlement`
- `completed`

## 5. Result Persistence Model

The job is not the business result itself.

The job stores state and references.

Final results should still live in feature-specific collections/entities:

- document ingestion result -> `Document`, extraction draft, chunks, context artifacts
- question generation result -> persisted FE/PE question entities and generation run records
- code mentor result -> `AIMentorFeedback`

`AI_Jobs.ResultRef` should point to those persisted entities.

## API Contract Changes

## Phase 1 contract style

Replace long-running synchronous endpoints with:

- `POST` create job
- `GET` query job
- optional `GET` fetch result by job

### Examples

#### Document upload

Current shape:

- `POST /api/student/documents/byos`

Target shape:

- `POST /api/student/documents/byos/jobs`
- returns:
  - `jobId`
  - `status`
  - `createdAt`
  - optional initial `documentDraftId`

#### Question generation

Current shape:

- sync generation route returns final generated set

Target shape:

- `POST /api/ai/question-generation/jobs`
- returns `jobId`

#### Code mentor

Target shape:

- `POST /api/submissions/{submissionId}/mentor/jobs`

#### Shared polling

- `GET /api/ai-jobs/{jobId}`

Should return:

- ownership-safe job summary
- status
- stage
- progress
- error summary
- result reference if completed

## FE Impact And Required Changes

This patch requires significant FE changes.

### FE must stop assuming final result comes from the original POST response

Instead:

1. submit job
2. receive `jobId`
3. navigate to progress state
4. poll job status
5. fetch or display final result when completed

### FE views that will be affected

- BYOS upload flow
- system upload/admin ingestion flow if exposed
- question generation result flow
- mentor request flow

### FE UX requirements

- clear `queued/running/completed/failed` states
- stage-specific progress text
- refresh-safe recovery by `jobId`
- retry button where business-safe
- good messaging for:
  - queue delay
  - billing failure
  - scope-thin partial result
  - worker failure

## Billing Impact

This patch changes billing semantics materially.

### Current sync mental model

- request starts
- AI runs
- cost is calculated or charged near the end

### Async target model

Need three steps:

1. `precheck`
   - ensure user has enough balance to start
2. `reservation`
   - hold estimated max or safe bounded amount
3. `settlement`
   - when job completes, settle actual cost
   - release unused reserved amount

### Billing rules required

- no double charge on retry or reclaim
- no settlement before result persistence succeeds
- failed jobs may still incur partial AI usage cost if AI calls already happened
- product decision required:
  - charge actual consumed cost on failed jobs
  - or absorb under selected failure categories

This decision must be explicit before implementation.

## Concurrency Control

This is one of the core reasons this patch exists.

### Global limits

Need configurable limits such as:

- max concurrent document ingestion jobs
- max concurrent generation jobs
- max concurrent mentor jobs

### Per-user limits

Need configurable limits such as:

- max running ingestion jobs per user = 1
- max running generation jobs per user = 1
- max running mentor jobs per user = 1

Purpose:

- prevent one user from monopolizing the worker pool
- keep fairness under load

### Provider-aware throttling

Optional but recommended:

- separate concurrency limit by provider or feature
- e.g. tighter limit for expensive GPT generation than for cheap DeepSeek gating/tagging

## Retry And Recovery Strategy

## Retry categories

### Safe automatic retry

- transient HTTP/provider timeout
- rate limit
- network reset
- temporary DB lock contention

### Cautious retry

- provider malformed output
- parse failure
- temporary review parsing collapse

### Do not auto retry blindly

- invalid user payload
- authorization failure
- stable insufficient balance
- persistent grounding failure caused by thin scope

## Recovery behavior

If worker dies mid-job:

- reclaim after `LeaseUntil`
- continue from persisted stage if resumable
- otherwise restart from a safe stage boundary

This means jobs should persist enough stage data to avoid redoing expensive work unnecessarily where possible.

## Rollout Strategy

This patch must be phased.

Do not convert everything in one release.

## Phase 0 - Preparation

- finalize this plan
- confirm job schema
- confirm billing reservation policy
- confirm FE polling UX
- add instrumentation to current sync flows to capture duration baseline

## Phase 1 - Shared Infrastructure

- create `AI_Jobs` collection
- create repository and claim/lease abstraction
- create worker base infrastructure
- create shared job status DTOs and polling route
- add admin diagnostics for job inspection

## Phase 2 - Question Generation First

Question generation should be migrated first because it has:

- high business visibility
- large latency variance
- repeated user triggering
- clearer payload/result boundary than upload ingestion

Required steps:

1. create generation job payload model
2. move orchestration entry from controller/request to worker
3. keep current generation service as the inner business executor
4. persist generation result and link to job
5. update FE to poll generation progress
6. test billing reservation and settlement

## Phase 3 - Document Ingestion

This is the heaviest flow and should come after generation.

Required steps:

1. create ingestion job payload model
2. split `DocumentService` into:
   - request submit path
   - worker execution path
3. persist extraction stage progress
4. ensure duplicate-file checks still happen safely
5. ensure document ownership and result consistency stay correct
6. update FE upload flow to progress-driven UX

## Phase 4 - Code Mentor

Migrate after Judge0 integration is stable enough.

Required steps:

1. create mentor job payload model
2. define how judge0 evidence is loaded when worker runs
3. persist mentor output as usual
4. update FE mentor trigger to poll state

## Phase 5 - Hardening

- dead-letter or failed-job triage
- job cancellation policy
- resumability refinement
- admin requeue tools
- cleanup retention policy
- richer metrics and alerts

## Detailed Work Breakdown

## Backend tasks

### A. Job domain and persistence

- define `AI_Job` entity
- add repository interface
- add Mongo indexes:
  - `Status + JobType + CreatedAt`
  - `RequestedByUserId + Status`
  - `LeaseUntil`
  - `ResultRef`
- add atomic claim query/update method

### B. Shared orchestration infrastructure

- worker base class
- stage update helper
- job error mapping helper
- lease heartbeat helper if needed
- retry helper

### C. Controller/API changes

- add create-job endpoints
- add job query endpoint
- add result fetch endpoint if separate
- preserve authorization and ownership checks

### D. Business executor extraction

For each AI feature:

- separate pure business execution from HTTP concerns
- separate billing settlement from API response timing
- support worker-driven execution path

### E. Billing changes

- add reservation entity or reservation fields
- add precheck and settlement flow
- add recovery guard to prevent double settlement

### F. Observability

- stage timing logs by job
- queue wait time
- execution duration
- provider latency by feature
- failure reason categories

## Frontend tasks

### A. Job submission flow

- submit request
- receive `jobId`
- store local UI state

### B. Progress screens

- queued state
- running state
- stage text
- elapsed time
- failure message

### C. Completion flow

- completed result rendering
- partial-result rendering where applicable
- retry action if allowed

### D. State recovery

- reload page and continue polling by `jobId`
- recover recent running jobs from backend

## Data Migration And Compatibility

This patch should avoid rewriting old business result collections.

Preferred compatibility strategy:

- add job orchestration as a layer above current business entities
- do not migrate old FE/PE question records
- do not migrate old document records unless new references are needed

Temporary compatibility mode may be needed:

- keep sync routes for admin/probe/local smoke
- use feature flag to switch student flows to async route

## Risks

## Major risks

1. double execution of a job
2. double billing settlement
3. job stuck in `running`
4. FE polling stale or missing result
5. partial persistence where result is stored but job state is not updated, or vice versa
6. race conditions under multi-instance deployment

## Mitigations

- atomic claim semantics
- idempotent settlement logic
- lease expiry and stale-job recovery
- clear stage checkpoints
- job/result transaction-like update discipline where possible
- admin diagnostics for stuck jobs

## Testing Strategy

This patch needs more than unit tests.

## Unit tests

- claim/lease logic
- status transition guards
- retry classification
- billing reservation calculations
- per-user concurrency checks

## Integration tests

- create job -> worker claims -> result persists -> poll returns completed
- restart/reclaim scenario
- duplicate submit handling
- failed provider call -> retry or fail behavior
- billing reservation + settlement lifecycle

## System tests

- 5 to 20 concurrent generation requests
- concurrent upload + generation mix
- multi-user fairness under per-user limits
- restart API during running jobs
- simulate provider slowdown and verify queue behavior

## Load and performance acceptance

Need explicit acceptance targets before implementation.

Example targets:

- API create-job response: under 2 seconds
- FE poll route: under 500 ms typical
- no duplicate job execution under 2 worker instances
- no double charge under worker retry/reclaim
- system remains responsive under N concurrent AI jobs

## Rollback Plan

The patch must support rollback.

Rollback strategy:

- keep current synchronous business executors available behind internal service boundary
- gate async routes by feature flag
- if async rollout fails:
  - disable worker claim
  - route traffic back to sync path for limited flows

Important:

- rollback must not orphan billing reservations
- rollback must include stuck-job cleanup procedure

## Done Criteria

This patch is done only when:

- question generation async job flow works end-to-end in production-like conditions
- document ingestion async flow works end-to-end in production-like conditions
- mentor async flow works end-to-end after judge0 integration is stable
- FE has stable progress UX for all migrated flows
- billing reservation/settlement is correct and idempotent
- multi-instance worker claim is safe
- load tests show real improvement versus current sync model

## Recommended Execution Order

1. freeze AI feature contracts enough to stop major churn
2. finalize this architecture plan
3. implement shared job infrastructure
4. migrate question generation
5. stabilize and measure
6. migrate document ingestion
7. stabilize and measure
8. migrate code mentor
9. harden operations and monitoring

## Recommendation

Do not start this patch while:

- chapter detection semantics are still shifting fast
- generation response contract is still in frequent redesign
- FE screens for current AI flows are still actively being corrected

Start it when the team is ready to treat it as a dedicated performance/scalability phase rather than a side patch.
