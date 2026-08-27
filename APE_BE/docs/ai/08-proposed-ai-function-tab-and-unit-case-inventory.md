# Proposed AI Function Tab And Unit Case Inventory

Updated: 2026-08-05

## Purpose

This note provides:

1. a cleaned AI-only proposal for the workbook `Function` tab
2. a normalized unit-test inventory per `F#`

It is intended to be the working draft before the team edits the real Excel workbook and rebuilds `BE_UnitTests`.

## Scope Decision

This proposal includes:

- AI ingest pipeline
- AI retrieval and generation
- AI retrieval browsing
- AI billing support used by AI flows
- AI Code Mentor
- AI admin/runtime reportable routes already exposed publicly

It does not automatically elevate internal helper seams such as `AIPromptService.Render` into workbook rows.

## Proposed Function Tab - AI Clean Version

Recommended ordering follows the runtime flow first, then support/admin surfaces.

| Proposed F | Class Name | Function Name | Description | Pre-Condition | Action |
|---|---|---|---|---|---|
| F25 | AIVndBillingService | EnsureMinimumBalanceAsync | Loads the effective AI billing configuration and rejects protected AI usage when the student's wallet balance is below the configured minimum threshold. | A student identity is supplied and billing config can be resolved. | Keep |
| F26 | AIVndBillingService | ChargeAsync | Calculates the VND charge from reported AI cost, deducts wallet balance without going negative, records absorbed loss when needed, and persists the billing transaction. | A charge request is supplied for an existing student and billing config can be resolved. | Keep |
| F32 | DocumentService | UploadAsync | Validates upload ownership and scope, checks duplicate file and normalized-content reuse, runs preview extraction precheck, gatekeeper, full extraction, extracted-content normalization with structure detection, chunking/embedding/tagging, extraction-draft updates, and finalizes billing timing for BYOS uploads. | Authenticated caller, target course, and supported file stream are supplied. | Keep and update |
| F33 | RetrievalPlannerService | GetSystemSourceDocumentsAsync | Returns completed active system documents for a course with retrieval-derived chunk, chapter, and topic summary metadata. | A course identifier is supplied. | Keep |
| F34 | AIRetrievalController | GetSystemCourseDocuments | Validates authenticated caller context, forwards the course document request to retrieval planner, and returns the wrapped system document list response. | Authorized request reaches the route. | Keep |
| F41 | FileExtractionService | ExtractPreviewTextAsync | Extracts supported document content and compacts it into a bounded preview payload used for precheck and early validation stages. | A supported document stream and file name are supplied. | Keep |
| F42 | FileExtractionService | ExtractTextAsync | Dispatches to the supported TXT, DOCX, PPTX, or PDF extractor and returns the full extraction result contract or rejects unsupported file types. | A document stream and file name are supplied. | Keep |
| F43 | AIGatekeeperService | ValidateAsync | Validates whether extracted content belongs to a supported academic subject using AI when enabled and heuristic fallback when needed. | Extracted content is available for the selected course or subject hint. | Keep |
| F44 | AIExtractedContentService | NormalizeAsync | Cleans extracted content, preserves image placeholders, optionally reinjects AI-derived vision detail, and applies AI-assisted structure detection to return normalized markdown and learner-facing structure hints. | Extracted-content payload is available for normalization. | Keep and update |
| F45 | AIEmbeddingTaggingService | CreateChunksAsync | Transforms normalized content into chunk drafts, batches embedding generation with fallback behavior, applies deterministic and AI-assisted topic tagging, filters tags against the active taxonomy, and emits retrieval-ready knowledge chunks. | Normalized content and document identity are available. | Keep and update |
| F46 | AIQuestionGenerationController | GenerateReviewFromSystemPool | Validates and normalizes the SYSTEM generation request, maps scope inputs into the shared generation workflow request, and dispatches the generation-review pipeline. | Authorized SYSTEM request is supplied with valid generation inputs. | Keep |
| F47 | AIQuestionGenerationController | GenerateReviewFromByosPool | Validates and normalizes the BYOS generation request, enforces BYOS scope rules, maps inputs into the shared generation workflow request, and dispatches the generation-review pipeline. | Authorized BYOS request is supplied with valid generation inputs. | Keep |
| F48 | RetrievalPlannerService | PlanAsync | Validates retrieval scope, resolves chapters and topics, retrieves scoped chunks, scores and ranks them, assembles a bounded context pack with coverage and shortfall metadata, and reuses or rebuilds cached packs when appropriate. | Scoped retrieval request is supplied with user identity, source scope, subject, question type, and target scope inputs. | Keep and update |
| F49 | QuestionGenerationReviewService | RunAsync | Validates source selection, resolves retrieval or context-pack input, generates FE or PE questions, repairs invalid output when allowed, applies deterministic and optional LLM review, handles grounded shortfall, persists accepted drafts, updates audit metadata, and charges accepted AI usage. | Normalized generation request is supplied with exactly one source among contextPackId, retrieval, or chunks. | Keep and update |
| F50 | RetrievalPlannerService | GetSystemDocumentChapterSummaryAsync | Loads the requested system document, validates course scope, retrieves its chunks, and returns ordered chapter summaries with covered topics and structure samples. | Authorized caller supplies courseId and system documentId. | Keep |
| F51 | AIRetrievalController | GetSystemDocumentChapters | Resolves authenticated user context, forwards course and document chapter-summary lookup, and wraps the response. | Authorized request reaches the route. | Keep |
| F52 | RetrievalPlannerService | GetByosDocumentChapterSummaryAsync | Loads the requested BYOS document, enforces owner-only access, retrieves its chunks, and returns ordered chapter summaries for the owned document. | Authenticated student supplies a BYOS document identifier. | Keep |
| F53 | StudentDocumentController | GetByosChapters | Resolves authenticated student context, forwards BYOS chapter-summary lookup, and maps unauthorized access to the student API contract. | Authorized BYOS request reaches the route. | Keep |
| F54 | RetrievalPlannerService | GetSystemDocumentChapterTopicSummaryAsync | Loads the requested system document, validates scope, filters chunks to the selected chapter, and aggregates chapter-scoped topic summary data. | Authorized caller supplies courseId, documentId, and chapterKey. | Keep |
| F55 | AIRetrievalController | GetSystemDocumentChapterTopics | Resolves authenticated user context, forwards the system chapter-topic summary request, and wraps the response. | Authorized request reaches the route. | Keep |
| F56 | RetrievalPlannerService | GetByosDocumentChapterTopicSummaryAsync | Loads the requested BYOS document, enforces owner-only access, filters chunks to the selected chapter, and aggregates BYOS chapter topic summary data. | Authenticated student supplies documentId and chapterKey. | Keep |
| F57 | StudentDocumentController | GetByosChapterTopics | Resolves authenticated student context, forwards the BYOS chapter-topic summary request, and maps unauthorized access to the student API contract. | Authorized BYOS request reaches the route. | Keep |
| F58 | RetrievalPlannerService | GetSystemTopicSummaryAsync | Aggregates system-scope topic counts, document counts, token estimates, and sample chapter keys across retrievable chunks for the selected course. | Authorized caller supplies courseId. | Keep |
| F59 | AIRetrievalController | GetSystemCourseTopics | Resolves authenticated user context, forwards the system topic-summary request, and wraps the response. | Authorized request reaches the route. | Keep |
| F60 | RetrievalPlannerService | GetByosDocumentTopicSummaryAsync | Loads the requested BYOS document, enforces owner-only access, retrieves its chunks, and aggregates document-level topic summary data. | Authenticated student supplies BYOS documentId. | Keep |
| F61 | StudentDocumentController | GetByosTopics | Resolves authenticated student context, forwards the BYOS topic-summary request, and wraps the response. | Authorized BYOS request reaches the route. | Keep |
| F62 | RetrievalPlannerService | GetSystemDocumentMultiChapterTopicSummaryAsync | Loads the requested system document, normalizes the selected chapter keys, filters chunks to those chapters, and returns multi-chapter topic summary data. | Authorized caller supplies courseId, documentId, and at least one chapter key. | Keep |
| F63 | AIRetrievalController | GetSystemDocumentMultiChapterTopics | Normalizes and de-duplicates chapter keys, resolves authenticated user context, forwards the multi-chapter summary request, and rejects empty effective chapter selection. | Authorized request reaches the route. | Keep |
| F64 | RetrievalPlannerService | GetByosDocumentMultiChapterTopicSummaryAsync | Loads the requested BYOS document, enforces owner-only access, normalizes selected chapter keys, filters chunks to those chapters, and returns BYOS multi-chapter topic summary data. | Authenticated student supplies documentId and at least one chapter key. | Keep |
| F65 | StudentDocumentController | GetByosMultiChapterTopics | Normalizes and de-duplicates chapter keys, resolves authenticated student context, forwards the BYOS multi-chapter summary request, and rejects empty effective chapter selection. | Authorized BYOS request reaches the route. | Keep |
| F86 | CodeMentorService | RunAsync | Validates the authenticated student and owned PE submission, enforces mentor quota and minimum wallet balance, renders the mentor prompt, executes the current mentor analysis flow, persists feedback and usage, and charges only after successful mentor completion. | Authenticated student owns an existing PE submission and meets minimum balance. | Keep and update |
| F87 | SubmissionController | RequestCodeMentor | Resolves authenticated student context, forwards the mentor request to CodeMentorService.RunAsync, and maps unauthorized, not-found, and bad-request outcomes to the current API contract. | Authorized mentor request reaches the route. | Keep |
| F88 | CodeMentorService | GetLatestAsync | Loads the requested PE submission, enforces ownership, and returns the latest mentor feedback record for that submission when available. | Authenticated student owns the target PE submission. | Keep |
| F89 | SubmissionController | GetLatestMentorFeedback | Resolves authenticated student context, forwards the latest mentor-feedback request, and maps unauthorized or not-found outcomes to the current API contract. | Authorized latest-feedback request reaches the route. | Keep |
| F90 | CodeMentorService | GetHistoryAsync | Loads the requested PE submission, enforces ownership, clamps the requested history size, and returns newest-first mentor feedback history. | Authenticated student owns the target PE submission. | Keep |
| F91 | SubmissionController | GetMentorFeedbackHistory | Resolves authenticated student context, forwards the mentor-history request, and maps unauthorized or not-found outcomes to the current API contract. | Authorized history request reaches the route. | Keep |
| F100 | AIVndBillingService | GetConfigAsync | Loads the active AI VND billing configuration and falls back to default values when no persisted setting exists. | Billing configuration is queried. | Keep |
| F101 | AIVndBillingService | UpsertConfigAsync | Validates and persists the AI billing configuration with audit metadata. | Authorized admin mutation request is supplied. | Keep |
| F102 | AIVndBillingService | RefundAsync | Loads the billing transaction, restores deducted balance on first refund, and keeps repeated refund requests idempotent. | Authorized admin refund request is supplied for an existing transaction. | Keep |
| F103 | AIProviderModelCatalogService | ListModelsAsync | Validates the requested provider, checks provider enablement and API key readiness, calls the provider model catalog, and normalizes the result payload. | Authorized admin requests model discovery for a configured provider. | Keep |
| F104 | AdminAIController | GetCreditTransactions | Loads paginated AI credit transactions and returns the parsed admin response projection. | Authorized admin request reaches the route. | Keep |
| F105 | AdminAIController | GetBillingTransactions | Loads paginated AI VND billing transactions and returns the wrapped admin response. | Authorized admin request reaches the route. | Keep |
| F106 | AdminAIController | GetBillingSummary | Loads aggregated billing summary rows and returns the wrapped admin summary payload. | Authorized admin request reaches the route. | Keep |
| F107 | AdminAIController | GetCreditSummary | Loads aggregated credit summary rows, computes derived averages, and returns the wrapped admin summary payload. | Authorized admin request reaches the route. | Keep |
| F108 | AdminAIController | RefundCreditTransaction | Validates refund reason, requires current admin identity, and forwards the refund request to the active credit policy service. | Authorized admin mutation request reaches the route. | Keep |
| F109 | AdminAIController | GetUsageLogs | Loads paginated AI usage logs and projects parsed payload plus agent-role metadata into the admin response. | Authorized admin request reaches the route. | Keep |
| F110 | AdminAIController | GetUsageSummary | Loads aggregated AI usage summary rows and computes current average token, average cost, and fallback-rate projections. | Authorized admin request reaches the route. | Keep |
| F111 | AdminAIController | GetArtifactUsageSummary | Loads aggregated artifact-usage summary rows and computes derived averages for the admin response. | Authorized admin request reaches the route. | Keep |
| F112 | AdminAIController | GetRuntimeHealth | Builds the runtime health projection using current agent enablement, provider readiness, and API-key presence. | Authorized admin request reaches the route. | Keep |
| F113 | AdminAIController | GetMentorFeedbacks | Loads paginated mentor-feedback rows and returns the current admin list projection. | Authorized admin request reaches the route. | Keep |
| F114 | AdminAIController | GetProviderCredentials | Loads the provider credential inventory and returns masked provider credential DTOs. | Authorized admin request reaches the route. | Keep |
| F115 | AdminAIController | CreateProviderCredentials | Requires current admin identity and delegates provider credential creation to the active admin service. | Authorized admin mutation request reaches the route. | Keep |
| F116 | AdminAIController | UpdateProviderCredentialsByRoute | Requires current admin identity, binds route providerName, and delegates provider credential update to the active admin service. | Authorized admin mutation request reaches the route. | Keep |
| F117 | AdminAIController | DeleteProviderCredentials | Requires current admin identity, delegates provider credential deletion, and returns the wrapped deleted marker payload. | Authorized admin mutation request reaches the route. | Keep |
| F118 | AdminAIController | UpsertProviderCredentials | Requires current admin identity and delegates batch provider credential upsert to the active admin service. | Authorized admin mutation request reaches the route. | Keep |
| F119 | AdminAIController | ReloadProviders | Reloads the provider-settings resolver cache and returns the wrapped reload marker payload. | Authorized admin request reaches the route. | Keep |
| F120 | AdminAIController | ListAgents | Loads the current AI agent inventory and returns the wrapped admin list payload. | Authorized admin request reaches the route. | Keep |
| F121 | AdminAIController | GetAgent | Loads the route-bound AI agent detail and maps missing agent to the current not-found contract. | Authorized admin request reaches the route. | Keep |
| F122 | AdminAIController | UpsertAgent | Requires current admin identity, binds route agent role, and delegates AI agent upsert to the active admin service. | Authorized admin mutation request reaches the route. | Keep |
| New F | AIQuestionGenerationController | GenerateReview | Accepts the generic generation-review request, validates the authenticated caller context, and dispatches the shared generation-review workflow without system/BYOS-specific wrapper shaping. | Authorized generation request reaches the generic route. | Add |
| New F | AIRetrievalController | Plan | Accepts the retrieval planning request, resolves authenticated caller context, forwards to RetrievalPlannerService.PlanAsync, and wraps the response. | Authorized retrieval planning request reaches the route. | Add |
| New F | RetrievalPlannerService | GetContextPackAsync | Loads an existing context pack by identifier, validates caller access to the underlying scope, and returns the current stored pack view. | Authenticated caller supplies a valid contextPackId. | Add |
| New F | AIRetrievalController | GetContextPack | Resolves authenticated caller context, forwards context-pack lookup, and wraps the response. | Authorized request reaches the route. | Add |
| New F | AIRetrievalController | ListContextPacks | Lists current context packs for the requested scope or filters and returns the wrapped response. | Authorized request reaches the route. | Add |
| New F | RetrievalPlannerService | MarkStaleAsync | Marks the targeted context pack as stale so the next generation request rebuilds retrieval context instead of reusing the current pack. | Authenticated caller supplies a valid contextPackId. | Add |
| New F | AIRetrievalController | MarkStale | Resolves authenticated caller context, forwards stale-marking request, and returns the wrapped response. | Authorized mutation request reaches the route. | Add |
| New F | AdminAIController | GetUserUsageSummary | Loads aggregated AI usage summary grouped by user and returns the current admin response projection. | Authorized admin request reaches the route. | Add |

## Proposed Unit Test Inventory Per F

The goal here is not to overfit the workbook with fake cases. Each `F#` gets only the cases that match its real seam.

### Group A - AI Billing Support

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F25 | 4 | `2N / 1A / 1B` | minimum balance passes at threshold, fails below threshold, rejects missing user |
| F26 | 6 | `3N / 1A / 2B` | zero-cost waived, normal deduction, absorbed loss, no negative wallet, ceil rounding, missing user/transaction dependency handling |

### Group B - AI Upload / Ingest

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F32 | 8 | `3N / 3A / 2B` | system upload happy path, BYOS happy path, duplicate by file hash, duplicate by normalized hash, minimum balance guard, unsupported file, empty normalized content, refund-after-finalization-failure path |
| F41 | 4 | `2N / 1A / 1B` | preview compaction for supported file, empty extraction handling, unsupported type propagation, preview length boundary |
| F42 | 4 | `2N / 1A / 1B` | supported TXT/DOCX/PDF/PPTX extraction, unsupported type rejection, empty content edge |
| F43 | 5 | `2N / 2A / 1B` | supported subject, ambiguous fallback accepted, unsupported domain rejected, unparseable AI fallback, low-signal content boundary |
| F44 | 6 | `2N / 3A / 1B` | cleanup happy path, placeholder preservation, structure detection shaping, AI drift fallback, empty raw text fallback, noisy-title filtering boundary |
| F45 | 6 | `2N / 2A / 2B` | chunk emission happy path, embedding mismatch fallback, heuristic tagging fallback, unsupported tag filtering, dense split boundary, empty normalized content edge |

### Group C - Generation Request Wrappers

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F46 | 3 | `1N / 1A / 1B` | valid SYSTEM mapping, invalid request rejected, scope normalization boundary |
| F47 | 3 | `1N / 1A / 1B` | valid BYOS mapping, BYOS ownership/scope validation failure, chapter/topic normalization boundary |
| New F - GenerateReview | 2 | `1N / 1A` | generic route forwards valid request, malformed/unauthorized request fails correctly |

### Group D - Retrieval Planning

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F48 | 8 | `3N / 3A / 2B` | chapter/topic scope planning, BYOS ownership guard, exact-topic outranking broad support, reference-only exclusion, cache reuse, stale rebuild, thin-scope shortfall metadata, chapter expansion boundary |
| New F - AIRetrievalController.Plan | 2 | `1N / 1A` | controller forwarding success, bad request / auth mapping |
| New F - GetContextPackAsync | 3 | `1N / 1A / 1B` | owned pack returned, foreign or missing pack rejected, stale/active metadata boundary |
| New F - GetContextPack | 2 | `1N / 1A` | controller forwarding success, unauthorized/not-found mapping |
| New F - ListContextPacks | 2 | `1N / 1A` | list returns filtered data, invalid filter or auth path handled |
| New F - MarkStaleAsync | 3 | `1N / 1A / 1B` | mark active pack stale, reject missing pack, already-stale boundary remains idempotent |
| New F - MarkStale | 2 | `1N / 1A` | controller mutation success, unauthorized/not-found mapping |

### Group E - Generation / Review Core

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F49 | 10 | `4N / 4A / 2B` | accepted FE run, accepted PE run, invalid JSON repair, duplicate rejection, grounded shortfall accepted-with-warning, too-thin scope needs_revision, mixed difficulty request handling, accepted-only charging, exactly-one-source validation, count/difficulty boundary |

### Group F - Retrieval Browsing

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F33 | 2 | `1N / 1A` | system document listing success, invalid course/no data handling |
| F34 | 2 | `1N / 1A` | controller forwarding success, auth/bad route handling |
| F50 | 3 | `1N / 1A / 1B` | chapter summary success, invalid course/document rejection, empty chapter result boundary |
| F51 | 2 | `1N / 1A` | controller forwarding success, auth/not-found mapping |
| F52 | 3 | `1N / 1A / 1B` | owned BYOS chapter summary success, foreign document rejection, empty summary boundary |
| F53 | 2 | `1N / 1A` | controller forwarding success, unauthorized mapping |
| F54 | 3 | `1N / 1A / 1B` | system chapter topic summary success, invalid chapter rejection, empty-topic boundary |
| F55 | 2 | `1N / 1A` | controller forwarding success, auth/not-found mapping |
| F56 | 3 | `1N / 1A / 1B` | owned BYOS chapter topic success, foreign doc rejection, empty-topic boundary |
| F57 | 2 | `1N / 1A` | controller forwarding success, unauthorized mapping |
| F58 | 2 | `1N / 1A` | course topic summary success, invalid course/no chunk handling |
| F59 | 2 | `1N / 1A` | controller forwarding success, auth handling |
| F60 | 3 | `1N / 1A / 1B` | owned BYOS topic summary success, foreign doc rejection, empty-topic boundary |
| F61 | 2 | `1N / 1A` | controller forwarding success, unauthorized mapping |
| F62 | 3 | `1N / 1A / 1B` | multi-chapter system topic summary success, empty chapter selection rejection, deduplication boundary |
| F63 | 2 | `1N / 1A` | controller forwarding success, empty effective chapter list rejected |
| F64 | 3 | `1N / 1A / 1B` | multi-chapter BYOS success, foreign doc rejection, chapter dedup boundary |
| F65 | 2 | `1N / 1A` | controller forwarding success, empty effective chapter list rejected |

### Group G - Code Mentor

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F86 | 8 | `3N / 3A / 2B` | owned submission success, quota exceeded, minimum balance guard, missing submission, charge-after-success only, malformed AI payload handling, history boundary, per-submission quota boundary |
| F87 | 3 | `1N / 2A` | controller forwarding success, unauthorized, not-found/bad-request mapping |
| F88 | 3 | `1N / 1A / 1B` | latest feedback success, foreign submission rejected, no-feedback boundary |
| F89 | 2 | `1N / 1A` | controller forwarding success, unauthorized/not-found mapping |
| F90 | 3 | `1N / 1A / 1B` | history success, foreign submission rejected, take clamp boundary |
| F91 | 2 | `1N / 1A` | controller forwarding success, unauthorized/not-found mapping |

### Group H - AI Admin / Runtime Config

| F | Target Case Count | Required N/A/B Shape | Core test intent |
|---|---:|---|---|
| F100 | 3 | `1N / 1A / 1B` | persisted config read, missing setting fallback, malformed setting boundary |
| F101 | 3 | `1N / 1A / 1B` | valid upsert, invalid rate/minimum rejected, overwrite boundary |
| F102 | 3 | `1N / 1A / 1B` | refund success, missing transaction rejected, repeat refund idempotency |
| F103 | 5 | `2N / 2A / 1B` | provider model list success, unsupported provider, disabled provider, missing API key, normalized payload boundary |
| F104-F113 | 1-2 each | mostly `1N / 1A` | list/summary route forwarding, repository projection, invalid auth/input mapping |
| F114-F119 | 2 each | `1N / 1A` | provider credential CRUD wrapper behavior, missing identity rejection, route binding correctness |
| F120-F122 | 2 each | `1N / 1A` | AI agent list/detail/upsert wrapper behavior and missing identity/not-found mapping |
| New F - GetUserUsageSummary | 2 | `1N / 1A` | user usage summary success, invalid auth/input mapping |

## Recommended Execution Order

Use this order when rebuilding tests:

1. `F25-F26`
2. `F32`
3. `F41-F45`
4. `F46-F49`
5. new retrieval/context-pack Fs
6. `F33-F65`
7. `F86-F91`
8. `F100-F122`

## Recommended Next Step

Use this note to do two things in sequence:

1. update the workbook `Function` tab to the cleaned AI inventory above
2. create one `BE_UnitTests` class per high-value seam group:
   - billing
   - ingest
   - retrieval planner
   - generation/review
   - retrieval browsing
   - mentor
   - admin/runtime config

After that, each class can be mapped back into the workbook `F#` matrix cleanly.
