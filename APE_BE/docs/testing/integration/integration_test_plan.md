# APE API Integration Test Plan

## Scope baseline

- Branch: `hoanganh`
- HEAD: `66b91a8f80c1787d5804901ec1bae8d4701cee53`
- Frozen implemented endpoint scope: `144`
- Excluded from Report 5.2.1 planning: `8` diagnostic/runtime/smoke endpoints

This document converts the frozen Step 1 API inventory into a risk-based Step 2 HTTP integration test matrix. It does not create test code, modify production code, or change either Excel workbook.

## Workbook compatibility

Reference workbook inspection was read-only and used only for report structure and wording conventions:

- top-level sheets for `Cover`, `Test Cases`, `Test Statistics`, and business modules
- table-oriented testcase layout
- concise business-oriented descriptions
- preconditions and reproducible procedures written as short step sequences

No business truth was taken from the reference workbook. Current executable code remains the source of truth.

## Final module grouping

| Module | Implemented endpoints | P0 | P1 | P2 |
| ------ | --------------------: | -: | -: | -: |
| Authentication | 4 | 3 | 1 | 0 |
| Admin User Management | 3 | 0 | 2 | 1 |
| Course Management | 8 | 0 | 3 | 5 |
| Document & Retrieval | 27 | 2 | 14 | 11 |
| Question Bank & Review | 22 | 0 | 12 | 10 |
| AI Question Generation | 5 | 0 | 4 | 1 |
| Exam Setup & Delivery | 15 | 2 | 8 | 5 |
| Practice & Submission | 17 | 11 | 5 | 1 |
| Wallet & Payment | 7 | 2 | 4 | 1 |
| Profile & Gamification | 7 | 0 | 3 | 4 |
| Admin AI Management | 29 | 0 | 2 | 27 |

## Risk-based test design notes

- P0 flows are covered with deeper workflow, authorization, persistence, or async cases.
- P1 flows generally have one success path plus one important failure, ownership, or persistence check.
- P2 flows are usually covered by one representative executable scenario unless the current code has a distinct business risk.
- Generic `401 Unauthorized` checks were not duplicated against every protected endpoint. Representative framework-level auth tests were used, while ownership-sensitive endpoints received route-specific business authorization scenarios.

## External dependency strategy

### Core integration

Core integration tests should use:

- real ASP.NET Core host
- real middleware/auth pipeline
- real controllers/services/repositories
- real isolated test MongoDB
- deterministic adapter overrides for:
  - Google token validation
  - Judge0 execution/grading
  - PayOS
  - AI text/embedding providers
  - file storage

### Live smoke

Small optional live smoke cases are planned only for:

- Google login wiring
- Judge0 synchronous run
- PayOS topup session creation
- AI generation provider wiring

These smoke cases should remain outside the main deterministic CI suite.

## Async workflow focus

### PE submission

- `POST /api/student/submissions/pe`
- `GET /api/student/submissions/pe/{id}`
- `GET /api/student/submissions/pe/session/{sessionId}`
- `GET /api/student/submissions/session/{sessionId}`

Planned proof points:

- accepted HTTP contract
- pending persistence
- eventual terminal grading state
- retrieval through API-visible endpoints only

### Wallet completion

- `POST /api/student/wallet/topups/create`
- `POST /api/payments/payos/webhook`
- `GET /api/student/wallet/topups/{paymentId}`

Planned proof points:

- pending payment creation
- webhook-driven completion
- exact-once wallet credit
- idempotent replay behavior

## Deterministic seed-data plan

Minimum later Step 3 seed set:

- `AdminUser`
- `StudentA`
- `StudentB`
- `InactiveUser`
- `ActiveCourse`
- `InactiveCourse`
- `SystemDocument`
- `StudentOwnedByosDocument`
- `KnowledgeChunks`
- `ContextPack`
- `FEQuestionActive`
- `FEQuestionDraftStudentOwned`
- `PEQuestionActive`
- `PEQuestionDraftStudentOwned`
- `PublicExam`
- `PrivateOwnedExam`
- `PracticeSessionOwnedByStudentA`
- `PracticeSessionOwnedByStudentB`
- `PendingPESubmission`
- `CompletedPESubmission`
- `PendingPayment`
- `CompletedPayment`
- `RefundableAIBillingTransaction`
- `RefundableAICreditTransaction`
- `AIProviderCredentialConfig`
- `AIAgentConfig`
- `AIRuleArtifact`

## Minimum Step 3 infrastructure requirements

- `WebApplicationFactory` for the API startup project
- isolated MongoDB per test run or per suite
- deterministic database reset/cleanup
- helper auth strategy for Admin, Student A, Student B, and inactive user
- deterministic overrides for Google, Judge0, PayOS, AI providers, and file storage
- reusable seed builders for documents, questions, exams, sessions, submissions, payments, and admin AI data
- async polling helper for eventual-state API checks
- HTTP assertion helpers for current `ApiResponse` envelope patterns

## Planning status

- All testcases are planning-only.
- Execution result status is still unknown for every case.
- No workbook fields were filled.
- No integration test code exists yet in this step.
