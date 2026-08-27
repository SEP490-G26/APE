# AI Unit Test Strategy And Workbook Mapping

Updated: 2026-08-05

## Purpose

This note resets the AI Feature unit-test strategy around the actual backend seams and the real workbook format in `Report5.1_Unit Test.xls.xlsx`.

The previous weak point was treating "AI Feature" as a broad feature story. That does not match the workbook. The workbook is function-centric:

- `1 reportable production function = 1 F-sheet`
- each `F#` sheet is a coverage matrix, not a narrative test-case list
- each case must be classified as:
  - `N` = normal
  - `A` = abnormal
  - `B` = boundary

So the correct way to rewrite AI unit tests is:

1. identify the exact public/reportable function seams
2. order them by real business flow
3. define focused N/A/B cases per seam
4. only then fill workbook sheets and code tests

## Workbook Structure Observed

Relevant sheets:

- `UC_Coverage`
- `Function`
- `Statistics`
- `F1` ... `F122`

Observed template behavior:

- `UC_Coverage` maps UC -> F-sheet -> production methods
- `Function` is the canonical function inventory
- `Statistics` rolls up pass/fail/untested counts from each `F#`
- every `F#` sheet is a matrix with:
  - conditions / preconditions
  - inputs
  - confirm / return
  - exception / log rows
  - case type `N/A/B`
  - pass/fail result

## AI Unit Scope

### Core AI ingest and generation scope

These functions are the primary AI Feature unit-test scope and should be treated as the main rewrite set:

1. `F41` - `FileExtractionService.ExtractPreviewTextAsync`
2. `F42` - `FileExtractionService.ExtractTextAsync`
3. `F43` - `AIGatekeeperService.ValidateAsync`
4. `F44` - `AIExtractedContentService.NormalizeAsync`
5. `F45` - `AIEmbeddingTaggingService.CreateChunksAsync`
6. `F48` - `RetrievalPlannerService.PlanAsync`
7. `F49` - `QuestionGenerationReviewService.RunAsync`

### Retrieval browsing AI scope

These are still AI-related and should be covered, but as secondary retrieval read-model seams:

1. `F50` - `GetSystemDocumentChapterSummaryAsync`
2. `F52` - `GetByosDocumentChapterSummaryAsync`
3. `F54` - `GetSystemDocumentChapterTopicSummaryAsync`
4. `F56` - `GetByosDocumentChapterTopicSummaryAsync`
5. `F58` - `GetSystemTopicSummaryAsync`
6. `F60` - `GetByosDocumentTopicSummaryAsync`
7. `F62` - `GetSystemDocumentMultiChapterTopicSummaryAsync`
8. `F64` - `GetByosDocumentMultiChapterTopicSummaryAsync`

### Shared AI support scope

These are not "generation logic", but they belong to AI runtime economics and should be included if we want full AI functional coverage:

1. `F25` - `AIVndBillingService.EnsureMinimumBalanceAsync`
2. `F26` - `AIVndBillingService.ChargeAsync`
3. `F32` - `DocumentService.UploadAsync`

Reason:

- `F32` is the real orchestration seam for BYOS/system ingest
- `F25/F26` are reused directly in AI upload and mentor flows

### AI Code Mentor scope

If AI Feature includes mentor, treat it as a separate AI submodule:

1. `F86` - `CodeMentorService.RunAsync`
2. `F87` - `SubmissionController.RequestCodeMentor`
3. `F88` / `F89` - latest mentor feedback lookup
4. `F90` / `F91` - mentor history lookup

## Correct Business-Flow Order

The old AI test design became weak partly because cases were not aligned to the runtime flow. The rewrite should follow this order:

1. Upload orchestration entry:
   - `F32`
2. Extraction:
   - `F41`
   - `F42`
3. Gatekeeper:
   - `F43`
4. Normalization and structure detection:
   - `F44`
5. Chunking, embedding, tagging:
   - `F45`
6. Retrieval planning and context packing:
   - `F48`
7. Generation and review:
   - `F49`
8. Retrieval browsing surfaces:
   - `F50/F52/F54/F56/F58/F60/F62/F64`
9. Billing support:
   - `F25/F26`
10. Mentor:
   - `F86+`

For workbook execution and reporting, this order is the correct AI narrative from input to output.

## Coverage Strategy Per Function

### Minimum rule

For each reportable AI seam, do not stop at one happy-path case. Each function should have:

- at least `1 N`
- at least `1 A`
- at least `1 B` when the function has a meaningful threshold, cap, ownership edge, source ambiguity, token cap, duplicate branch, or count boundary

### Recommended baseline counts

These are planning targets, not rigid limits:

- `F41-F45`: `4-6` cases each
- `F48`: `6-8` cases
- `F49`: `8-12` cases
- `F50/F52/F54/F56/F58/F60/F62/F64`: `2-4` cases each
- `F25/F26`: `4-6` cases each
- `F86`: `6-10` cases
- thin controller wrappers such as `F46/F47/F87/F89/F91`: `2-4` cases each

## What 100 Percent AI Function Coverage Should Mean

For this workbook, "100 percent AI function coverage" should not mean every internal branch in every private helper.

It should mean:

1. every in-scope reportable AI production function has an `F-sheet`
2. every in-scope reportable AI production function has code-backed unit cases
3. every function has at least one meaningful normal case
4. every function has its key failure or guard path covered
5. every function with a real threshold or cap has at least one boundary case
6. workbook `Statistics` can show no AI in-scope function left untested

That is the defensible definition for the submission workbook.

## Function-Level Test Design Guidance

### F41 / F42 - Extraction

Focus:

- supported file types
- unsupported file rejection
- empty content
- preview compaction behavior
- parser metadata preservation

Do not mix:

- downstream gatekeeper
- normalization
- document persistence

### F43 - Gatekeeper

Focus:

- supported subject accepted
- ambiguous content fallback
- unparseable AI response fallback
- unsupported domain rejection
- empty/low-signal input behavior

### F44 - Normalize

Focus:

- cleanup of extraction noise
- placeholder preservation
- structure candidate filtering
- AI drift fallback
- empty raw text fallback

This is one of the highest-risk AI seams and needs careful direct tests.

### F45 - Chunking / Embedding / Tagging

Focus:

- chunk emission from normalized structure
- embedding fallback on mismatch/failure
- heuristic tagging fallback
- filtering unsupported topic noise
- chunk boundary behavior

### F48 - Retrieval Planner

Focus:

- scope validation
- chapter/topic resolution
- ownership guard for BYOS
- ranking quality at the planner seam
- context-pack cache reuse
- stale cache invalidation
- thin-scope shortfall metadata

### F49 - Generation / Review

Focus:

- exactly-one-source validation
- FE accepted flow
- PE accepted flow
- invalid JSON repair
- duplicate rejection
- shortfall/warning behavior
- mixed difficulty request handling
- billing timing for accepted output

This is the most important AI unit seam after normalization and retrieval.

### F50-F64 - Retrieval summaries

Focus:

- owner/scope guard
- chapter filtering
- topic aggregation
- empty selection handling
- chapter key normalization and deduplication

These should stay small and sharp. Do not inflate them into integration tests.

### F25 / F26 - Billing

Focus:

- minimum balance thresholds
- charge rounding behavior
- absorbed loss tracking
- no negative balance
- idempotent or safe failure behavior where applicable

### F86+ - Mentor

Focus:

- owner-only access
- quota per submission
- minimum balance guard
- charge only after success
- feedback persistence
- missing evidence / malformed AI payload handling

## Recommendations For Workbook Rewrite

### 1. Separate AI test pack from generic backend pack

Even if the workbook is shared, internally we should treat AI as four sub-packs:

1. AI ingest
2. AI retrieval and generation
3. AI billing support
4. AI code mentor

This makes execution and reporting much clearer.

### 2. Rewrite by function seam, not by UC narrative

The workbook already proves that UCs can map to the same `F#`.
So test design should start from `Function` rows, then map back up to UCs.

### 3. Use direct unit code as the source of truth

Yes, unit test requires code.

For this workbook, the sheet should reflect executable backend unit tests. The ideal loop is:

1. define F-sheet case inventory
2. implement direct unit tests in `BE_UnitTests`
3. run tests
4. mark workbook results from actual execution

### 4. Avoid mixing integration or system concerns

Do not put these into unit sheets unless the seam directly owns them:

- full HTTP auth stack
- database round-trip verification across many services
- real external provider behavior
- end-to-end upload through controllers plus persistence plus AI provider

Those belong to integration/system testing.

## Proposed Next Step

Before writing new AI unit tests, do these in order:

1. confirm final AI unit scope list
2. rewrite the AI `F#` sheets inventory in the workbook order above
3. rebuild `BE_UnitTests` for the AI seams only
4. map each code test class back to one `F#`
5. then fill execution results into the workbook

## Practical Answer To Three Common Questions

### How many AI unit test cases should there be?

There is no single fixed number imposed by the workbook.

A practical target for solid AI coverage is:

- core AI seams: around `35-50` direct unit cases
- retrieval browsing seams: around `16-24`
- billing support: around `8-12`
- mentor: around `12-20`

So the total AI unit pack will likely land around `55-85` cases depending on whether mentor is included.

That is a realistic range for high coverage without creating fake or redundant cases.

### Can the sheet be auto-filled?

Partially yes.

Possible:

- export case ids
- export mapped function inventory
- export pass/fail counts from unit test results
- generate a workbook-friendly draft table or CSV for manual paste

Still likely manual:

- final wording polish inside `F#` sheets
- defect IDs
- execution metadata

So a semi-automated fill is realistic; full trustworthy auto-fill is harder.

### Does unit test require code?

Yes.

For this project, unit-test evidence should come from executable backend tests in `BE_UnitTests`, not only from written scenarios in Excel.
