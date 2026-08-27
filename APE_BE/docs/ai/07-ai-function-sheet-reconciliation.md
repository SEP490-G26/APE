# AI Function Sheet Reconciliation

Updated: 2026-08-05

## Purpose

This note reconciles the current `Function` sheet in `Report5.1_Unit Test.xls.xlsx` against the actual backend code.

Goal:

- keep AI `F#` entries that are still real in code
- fix descriptions or ownership where the sheet is stale
- add missing AI functions that already exist in production code
- separate reportable workflow seams from internal support seams

## Summary

The current workbook already contains a large part of the AI surface, but it is **not yet a clean source of truth**.

Main issues:

1. some AI functions are present and valid, but need wording updates
2. some AI functions exist in code but are missing from the sheet
3. some internal AI runtime seams exist in code but should be explicitly marked as either:
   - reportable new `F#`, or
   - internal support only, covered indirectly
4. the workbook contains a few non-AI duplicate rows outside this scope, which signals that the sheet needs a careful consistency pass

## Existing AI Functions To Keep

These functions already exist in the workbook and are still active in code.

### AI upload / ingest

- `F32` - `DocumentService.UploadAsync`
- `F41` - `FileExtractionService.ExtractPreviewTextAsync`
- `F42` - `FileExtractionService.ExtractTextAsync`
- `F43` - `AIGatekeeperService.ValidateAsync`
- `F44` - `AIExtractedContentService.NormalizeAsync`
- `F45` - `AIEmbeddingTaggingService.CreateChunksAsync`

### AI generation

- `F46` - `AIQuestionGenerationController.GenerateReviewFromSystemPool`
- `F47` - `AIQuestionGenerationController.GenerateReviewFromByosPool`
- `F48` - `RetrievalPlannerService.PlanAsync`
- `F49` - `QuestionGenerationReviewService.RunAsync`

### Retrieval browsing

- `F33` - `RetrievalPlannerService.GetSystemSourceDocumentsAsync`
- `F34` - `AIRetrievalController.GetSystemCourseDocuments`
- `F50` - `RetrievalPlannerService.GetSystemDocumentChapterSummaryAsync`
- `F51` - `AIRetrievalController.GetSystemDocumentChapters`
- `F52` - `RetrievalPlannerService.GetByosDocumentChapterSummaryAsync`
- `F53` - `StudentDocumentController.GetByosChapters`
- `F54` - `RetrievalPlannerService.GetSystemDocumentChapterTopicSummaryAsync`
- `F55` - `AIRetrievalController.GetSystemDocumentChapterTopics`
- `F56` - `RetrievalPlannerService.GetByosDocumentChapterTopicSummaryAsync`
- `F57` - `StudentDocumentController.GetByosChapterTopics`
- `F58` - `RetrievalPlannerService.GetSystemTopicSummaryAsync`
- `F59` - `AIRetrievalController.GetSystemCourseTopics`
- `F60` - `RetrievalPlannerService.GetByosDocumentTopicSummaryAsync`
- `F61` - `StudentDocumentController.GetByosTopics`
- `F62` - `RetrievalPlannerService.GetSystemDocumentMultiChapterTopicSummaryAsync`
- `F63` - `AIRetrievalController.GetSystemDocumentMultiChapterTopics`
- `F64` - `RetrievalPlannerService.GetByosDocumentMultiChapterTopicSummaryAsync`
- `F65` - `StudentDocumentController.GetByosMultiChapterTopics`

### AI billing / mentor / admin config

- `F25` - `AIVndBillingService.EnsureMinimumBalanceAsync`
- `F26` - `AIVndBillingService.ChargeAsync`
- `F86` - `CodeMentorService.RunAsync`
- `F87` - `SubmissionController.RequestCodeMentor`
- `F88` - `CodeMentorService.GetLatestAsync`
- `F89` - `SubmissionController.GetLatestMentorFeedback`
- `F90` - `CodeMentorService.GetHistoryAsync`
- `F91` - `SubmissionController.GetMentorFeedbackHistory`
- `F100` - `AIVndBillingService.GetConfigAsync`
- `F101` - `AIVndBillingService.UpsertConfigAsync`
- `F102` - `AIVndBillingService.RefundAsync`
- `F103` - `AIProviderModelCatalogService.ListModelsAsync`
- `F104` - `AdminAIController.GetCreditTransactions`
- `F105` - `AdminAIController.GetBillingTransactions`
- `F106` - `AdminAIController.GetBillingSummary`
- `F107` - `AdminAIController.GetCreditSummary`
- `F108` - `AdminAIController.RefundCreditTransaction`
- `F109` - `AdminAIController.GetUsageLogs`
- `F110` - `AdminAIController.GetUsageSummary`
- `F111` - `AdminAIController.GetArtifactUsageSummary`
- `F112` - `AdminAIController.GetRuntimeHealth`
- `F113` - `AdminAIController.GetMentorFeedbacks`
- `F114` - `AdminAIController.GetProviderCredentials`
- `F115` - `AdminAIController.CreateProviderCredentials`
- `F116` - `AdminAIController.UpdateProviderCredentialsByRoute`
- `F117` - `AdminAIController.DeleteProviderCredentials`
- `F118` - `AdminAIController.UpsertProviderCredentials`
- `F119` - `AdminAIController.ReloadProviders`
- `F120` - `AdminAIController.ListAgents`
- `F121` - `AdminAIController.GetAgent`
- `F122` - `AdminAIController.UpsertAgent`

## Important Correction: F41 Is Still Active

`F41` must currently stay.

Reason:

- `DocumentService.UploadAsync` still calls `_extractor.ExtractPreviewTextAsync(...)`
- that preview extraction is used for the precheck stage before gatekeeper
- so from the current code perspective, `F41` is a real active seam

If the team wants to remove `F41`, the code must be changed first. The sheet alone cannot declare it removed.

## Existing Functions That Need Description Updates

These `F#` entries should stay, but their wording should be updated to match the current AI flow more closely.

### F32 - DocumentService.UploadAsync

Current wording is broadly correct, but should explicitly mention:

- preview extraction precheck
- gatekeeper
- normalized structure detection through extracted-content AI
- chunking/embedding/tagging
- extraction draft lifecycle
- billing timing for BYOS

### F44 - AIExtractedContentService.NormalizeAsync

Description should make clear that this is no longer only "cleanup":

- it now also owns the AI-assisted structure detection path
- it is effectively the extraction-to-structured-content seam

### F45 - AIEmbeddingTaggingService.CreateChunksAsync

Description should mention:

- chunk drafting
- embedding batching / fallback
- auto-tagging
- taxonomy filtering

### F48 - RetrievalPlannerService.PlanAsync

Description should mention:

- context-pack assembly
- shortfall / thin-scope warning metadata
- cache reuse and stale rebuild behavior

### F49 - QuestionGenerationReviewService.RunAsync

Description should mention current behavior explicitly:

- FE and PE generation
- repair / retry path
- rule review and optional reviewer path
- grounded shortfall behavior
- mixed difficulty generation support
- accepted-only persistence and charging

### F86 - CodeMentorService.RunAsync

Description should mention:

- current mentor flow is active
- Judge0 execution evidence is not yet the finalized integrated source in the current runtime

## Missing AI Functions That Should Be Added

These functions exist in code and are public/reportable enough that the workbook should add them as new `F#` entries.

### High priority additions

1. `AIQuestionGenerationController.GenerateReview`
   - generic generation-review route exists in code
   - current workbook only includes system/byos specialized routes

2. `AIRetrievalController.Plan`
   - retrieval planning controller wrapper exists
   - current workbook only tracks service `PlanAsync`

3. `AIRetrievalController.GetContextPack`
   - public route exists

4. `AIRetrievalController.ListContextPacks`
   - public route exists

5. `AIRetrievalController.MarkStale`
   - public route exists

6. `RetrievalPlannerService.GetContextPackAsync`
   - public service seam exists

7. `RetrievalPlannerService.MarkStaleAsync`
   - public service seam exists

8. `AdminAIController.GetUserUsageSummary`
   - public admin route exists
   - missing while other admin usage summary routes are already listed

### Recommended additions if admin smoke routes are in scope

9. `AdminAIController.SmokeGatekeeper`
10. `AdminAIController.SmokeExtractedContent`
11. `AdminAIController.SmokeEmbedding`
12. `AdminAIController.SmokeRetrievalPlan`
13. `AdminAIController.SmokeQuestionGenerationReview`
14. `AdminAIController.SmokeCodeMentor`

These are real routes, but whether they deserve their own `F#` depends on submission scope. If the workbook covers admin runtime validation, they should be added.

## Internal Support Seams Present In Code But Not Mandatory As Standalone F-Sheets

These functions are real and important, but they are good candidates to remain internal support seams unless the team wants deeper AI-runtime unit coverage in the workbook.

- `AIFeatureRoutingService.ResolveText`
- `AIFeatureRoutingService.ResolveEmbedding`
- `AIPromptService.Render`
- `AIExecutionService.ExecuteTextAsync`
- `AIExecutionService.ExecuteEmbeddingAsync`
- `CodeMentorService.RunSmokeAsync`

Recommendation:

- write direct unit tests for them if needed
- but do not automatically make them workbook `F#` entries unless the team wants to expose the AI runtime internals in the official submission inventory

## Recommended Decision For Function Sheet

### Keep and update

Keep all current AI `F#` entries from:

- `F25-F26`
- `F32-F65`
- `F86-F91`
- `F100-F122`

but update descriptions for:

- `F32`
- `F44`
- `F45`
- `F48`
- `F49`
- `F86`

### Add missing reportable functions

At minimum, add new `F#` rows for:

- `AIQuestionGenerationController.GenerateReview`
- `AIRetrievalController.Plan`
- `AIRetrievalController.GetContextPack`
- `AIRetrievalController.ListContextPacks`
- `AIRetrievalController.MarkStale`
- `RetrievalPlannerService.GetContextPackAsync`
- `RetrievalPlannerService.MarkStaleAsync`
- `AdminAIController.GetUserUsageSummary`

### Optional additions

Add admin smoke routes only if admin runtime validation belongs to official workbook scope.

## Final Position

The AI `Function` inventory should now be treated as:

1. keep current AI rows that still match code
2. correct stale wording
3. add missing public/reportable AI routes and service seams
4. keep internal runtime helpers outside workbook `F#` unless the team explicitly wants that depth

This gives a stable base for:

- rebuilding AI unit-test matrices
- updating `UC_Coverage`
- preventing future mismatch between workbook and backend source
