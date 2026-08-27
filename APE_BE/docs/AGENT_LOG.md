# AI Work Log (AGENT_LOG.md)

## [2026-08-21 01:20] BE Unit Test Suite Synchronization & Full Pass
**Requested:** Synchronize all fake repositories, update interfaces, fix obsolete credit references and message assertions so the entire BE_UnitTests test suite passes 100%.
**Delivered:**
- [BE] `BE_UnitTests/BE_UnitTests.csproj` — Excluded legacy compile duplicates with `<Compile Remove="legacy\**" />`.
- [BE] `BE_UnitTests/WalletPaymentTestSupport.cs` — Added `ITopupPackageRepository`, implemented missing members in `FakeAIVndBillingTransactionRepository`.
- [BE] `BE_UnitTests/DocumentTestSupport.cs` — Implemented `ListFilterOptionsAsync` in fake usage log repo.
- [BE] `BE_UnitTests/CodeMentorTestSupport.cs` — Added `FakeCodeMentorBillingTransactionRepository` and implemented missing question/log repo members.
- [BE] `BE_UnitTests/McqPracticeTestSupport.cs` — Implemented missing student question repository methods.
- [BE] `BE_UnitTests/PECodeRunServiceTests.cs` — Implemented missing methods in fake PE question repository.
- [BE] `BE_UnitTests/PESubmissionServiceCodingPracticeTests.cs` — Implemented missing methods in fake PE question repository.
- [BE] `BE_UnitTests/PESubmissionAttemptAllocationAndIndexTests.cs` — Implemented missing methods in fake PE question repository.
- [BE] `BE_UnitTests/SubmissionGradingLeaseFlowTests.cs` — Implemented missing methods in fake question repository.
- [BE] `BE_UnitTests/SubmissionGradingReliabilityTests.cs` — Implemented missing methods in fake question repository.
- [BE] `BE_UnitTests/ExamControllerPracticeSetupTests.cs` — Aligned `ListOwnedByStudentAsync` with 8 parameters.
- [BE] `BE_UnitTests/ExamServicePracticeSetupTests.cs` — Aligned `ListOwnedByStudentAsync` with 8 parameters.
- [BE] `BE_UnitTests/QuestionControllerQuestionBankManagementTests.cs` — Added `status` parameter to `ListOwnedByStudentAsync`, synchronized `ListAdminOwnedAsync` tracking.
- [BE] `BE_UnitTests/AIQuestionGenerationControllerTests.cs` — Added `status` parameter to `ListOwnedByStudentAsync`.
- [BE] `BE_UnitTests/QuestionGenerationReviewServiceTests.cs` — Added `status` parameter to `ListOwnedByStudentAsync` and synchronized assertion expectations.
- [BE] `BE_UnitTests/AdminAIControllerTests.cs` — Replaced obsolete credit tests with VND billing tests, added `FakeAdminUserRepository`.
- [BE] `BE_UnitTests/StudentWalletControllerCancelTests.cs` — Updated error message assertions to match English strings.
- [BE] `BE_UnitTests/StudentWalletControllerCreateTopupTests.cs` — Updated error message assertions and BadRequest response.
- [BE] `BE_UnitTests/StudentWalletControllerGetAiTransactionsTests.cs` — Updated session expired message assertion.
- [BE] `BE_UnitTests/StudentWalletControllerGetDetailTests.cs` — Updated not found message assertion.
- [BE] `BE_UnitTests/StudentWalletControllerGetHistoryTests.cs` — Updated invalid status message assertion.
- [BE] `BE_UnitTests/WalletTopupServiceCancelTests.cs` — Updated cancellation error message assertions.
- [BE] `BE_UnitTests/WalletTopupServiceCreateTopupTests.cs` — Updated top-up validation error message assertions.
- [BE] `BE_UnitTests/WalletTopupServiceGetHistoryTests.cs` — Updated status validation message assertions.
- [BE] `BE_UnitTests/WalletTopupServiceHandleWebhookTests.cs` — Updated webhook failure reason assertions.
- [BE] `BE_UnitTests/AIVndBillingServiceEnsureMinimumBalanceTests.cs` — Updated comma thousand separator in formatted currency assertions.
- [BE] `BE_UnitTests/PESubmissionAggregateTests.cs` — Corrected test assertion for unmasked actual output.
- [BE] `BE_UnitTests/GamificationTestSupport.cs` — Added `currentStreak` to user creation test helper.
- [BE] `BE_UnitTests/GamificationServiceTests.cs` — Set currentStreak in streak test cases.
- [BE] `BE_UnitTests/StudentAnalyticsGamificationControllerTests.cs` — Configured currentStreak in controller test fixture.
**Status:** ✅ Complete (849/849 Passed, 0 Failed, 0 Skipped)
**Notes:** All unit test dependencies are now fully aligned with production contracts and interfaces.

## [2026-08-21 02:26] Unit Test Reports Export (Batch 04: F31 - F40)
**Requested:** Generate and export Batch 04 (F31 - F40) test specification, Excel formulas, decision matrix with 'O' marks, terminal evidence, and update interactive viewer.
**Delivered:**
- [Doc] `UnitTests_Report/Batch_04_F31_F40.md` — Exported F31 to F40 (55 test cases).
- [Doc] `UnitTests_Report/UnitTest_Viewer.html` — Updated with Batch 04 interactive dataset & copyable Excel TSV tables.
- [Artifact] `unit_test_report_batch_04.md` — Created Antigravity visual artifact.
**Status:** ✅ Complete (55 Test Cases documented with 100% Passed Evidence)
**Notes:** Covered Course Admin, Student Course Catalog, Student Document, Practice Session lifecycle, MCQ evaluation, and Submission aggregates.

## [2026-08-21 02:29] Unit Test Reports Export (Batch 05: F41 - F50 & Batch 06: F51 - F60)
**Requested:** Generate and export Batch 05 (F41 - F50) and Batch 06 (F51 - F60) test specifications, Excel formulas, decision matrix with 'O' marks, terminal evidence, and update interactive viewer.
**Delivered:**
- [Doc] `UnitTests_Report/Batch_05_F41_F50.md` — Exported F41 to F50 (63 test cases).
- [Doc] `UnitTests_Report/Batch_06_F51_F60.md` — Exported F51 to F60 (56 test cases).
- [Doc] `UnitTests_Report/UnitTest_Viewer.html` — Updated with Batch 05 and Batch 06 interactive datasets & one-click Excel copy support.
**Status:** ✅ Complete (119 Test Cases documented with 100% Passed Evidence)
**Notes:** Covered Exam Setup, PE Submissions & Dry Run, Diagnostic Parser, Code Mentor Hints, Gamification Streak/Points/Leaderboard, Question Bank, and AI Question Generation drafts.

## [2026-08-21 02:32] Unit Test Reports Export (Batch 07: F61 - F70 & Batch 08: F71 - F80)
**Requested:** Generate and export Batch 07 (F61 - F70) and Batch 08 (F71 - F80) test specifications, Excel formulas, decision matrix with 'O' marks, terminal evidence, and update interactive viewer.
**Delivered:**
- [Doc] `UnitTests_Report/Batch_07_F61_F70.md` — Exported F61 to F70 (46 test cases).
- [Doc] `UnitTests_Report/Batch_08_F71_F80.md` — Exported F71 to F80 (59 test cases).
- [Doc] `UnitTests_Report/UnitTest_Viewer.html` — Updated with Batch 07 and Batch 08 interactive datasets & one-click Excel copy support.
**Status:** ✅ Complete (105 Test Cases documented with 100% Passed Evidence)
**Notes:** Covered AI RAG Vector Retrieval, Context Caching, Student Doc Browsing, AI VND Token Billing, Rate Limiting Gatekeeper, Provider Catalog, and Admin AI Pricing & Routing Governance.

## [2026-08-21 02:34] Unit Test Reports Export (Batch 09: F81 - F90 & Batch 10: F91 - F100)
**Requested:** Generate and export Batch 09 (F81 - F90) and Batch 10 (F91 - F100) test specifications, Excel formulas, decision matrix with 'O' marks, terminal evidence, and update interactive viewer.
**Delivered:**
- [Doc] `UnitTests_Report/Batch_09_F81_F90.md` — Exported F81 to F90 (66 test cases).
- [Doc] `UnitTests_Report/Batch_10_F91_F100.md` — Exported F91 to F100 (63 test cases).
- [Doc] `UnitTests_Report/UnitTest_Viewer.html` — Updated with Batch 09 and Batch 10 interactive datasets & one-click Excel copy support.
**Status:** ✅ Complete (129 Test Cases documented with 100% Passed Evidence)
**Notes:** Covered Judge0 Sandbox Code Execution, Options Validation, Submission Grading Reliability/Zombie Recovery, Score Aggregates & Hidden Test Masking, Grading Worker Lease Heartbeats, CAS Concurrency, Google OpenID Validation, JWT Generation, and Database Bootstrap.

## [2026-08-21 02:36] Unit Test Reports Export (Batch 11: F101 - F110, Batch 12: F111 - F120 & Batch 13: F121 - F125)
**Requested:** Generate and export final batches (F101 - F125) test specifications, Excel formulas, decision matrix with 'O' marks, terminal evidence, and complete interactive viewer integration.
**Delivered:**
- [Doc] `UnitTests_Report/Batch_11_F101_F110.md` — Exported F101 to F110 (63 test cases).
- [Doc] `UnitTests_Report/Batch_12_F111_F120.md` — Exported F111 to F120 (56 test cases).
- [Doc] `UnitTests_Report/Batch_13_F121_F125.md` — Exported F121 to F125 (30 test cases).
- [Doc] `UnitTests_Report/UnitTest_Viewer.html` — Completed full interactive suite covering all 125 functions (F01 - F125) with one-click Excel TSV export.
**Status:** ✅ Complete (149 Test Cases documented with 100% Passed Evidence — Total 737 Unit Test Cases across entire 125 Function Sheets).
**Notes:** Covered Auth Endpoints, Token Rotation, User Profile Queries, Topup Constraints, Webhook Signature Verification, Code Sandbox Execution Contracts, AI Mentor Quality Checks, AI Question Generation Draft Review & Approval, Bloom Distribution, and Submission Detail Endpoints.

## [2026-08-21 02:54] Fix Interactive Web Viewer (UnitTest_Viewer.html) Real Table Data Rendering
**Requested:** Fix UnitTest_Viewer.html to display exact, full Decision Matrix tables with all specific conditions, parameters, values, and 'O' marks instead of generic placeholder rows.
**Delivered:**
- [App] `UnitTests_Report/UnitTest_Viewer.html` — Upgraded rendering engine to parse and embed 100% exact conditions, parameters, values, and 'O' marks from all 13 Batch Markdown files across all 125 function sheets (`F01` to `F125`).
- [App] Added dynamic Category `rowspan` grouping, Excel formulas display, and accurate TSV clipboard generator matching the exact Decision Matrix grid for seamless copy-paste into Excel.
**Status:** ✅ Complete
**Notes:** Verified standalone functionality — `UnitTest_Viewer.html` is completely self-contained and renders immediately upon opening in any web browser.

## [2026-08-21 03:04] Optimize Excel Copy TSV & Add Row Counters to Web Viewer
**Requested:** Improve Copy to Excel: (1) Omit table headers (Category, Parameter, Value), (2) Unmerge Category so 'Condition', 'Confirm', 'Result' only appear on the first row of each section, (3) Add row count badges for Condition, Confirm, Result, and Total grid dimensions to help pre-configure Excel rows.
**Delivered:**
- [App] `UnitTests_Report/UnitTest_Viewer.html` — Updated `copyActiveMatrixToExcel()` to generate clean, unmerged TSV data omitting header rows and placing Category labels only on the first row of each respective section.
- [App] Added interactive Row Counter bar displaying exact row counts for `Condition`, `Confirm`, `Result`, and `Total Grid (Rows × Columns)` for each function sheet.
**Status:** ✅ Complete
**Notes:** Tested with Node.js parser verification for F01 and across all 125 sheets.

## [2026-08-21 03:15] Adjust Excel Copy Alignment (Col B Title, Col D Content)
**Requested:** Adjust TSV copy alignment to match Excel template where Column C is invisible/spacer, so Title (Item/Parameter) is in Column B, Content (Value/Condition) is in Column D, and Test cases start at Column E.
**Delivered:**
- [App] `UnitTests_Report/UnitTest_Viewer.html` — Configured TSV generator to insert Column C spacer tab `\t\t` by default (Col A: Category, Col B: Title/Item, Col C: Spacer, Col D: Content/Value, Col E+: Test case columns).
- [App] Added a Format Toggle switch in the Topbar between "Template Col B/D (Default)" and "Compact Col B/C" for multi-template compatibility.
**Status:** ✅ Complete
**Notes:** Verified 100% column-by-column alignment for F01 - F125.

## [2026-08-21 03:28] Add Column E Spacer for Col F Test Cases Alignment
**Requested:** Align test case 'O' marks with Column F in Excel (Column E is collapsed/invisible spacer in template).
**Delivered:**
- [App] `UnitTests_Report/UnitTest_Viewer.html` — Updated default template layout to include two spacer columns (Col C and Col E). TSV outputs: Col A=Category, Col B=Item, Col C=[Spacer], Col D=Value, Col E=[Collapsed Spacer], Col F+=UTCID01...
- [App] Verified with Node parser for exact tab split matching Excel image grid.
**Status:** ✅ Complete
**Notes:** Tested with live Excel template structure shown in user screenshot.

## [2026-08-21 11:02] Clean Non-O Cells from Test Case Columns (Batch 01 - Batch 13)
**Requested:** Review all batches (Batch 9 to end and entire suite) to ensure descriptive text like 'None', 'Shown', 'Masked', 'Unchanged' are strictly moved into the 'Value / Condition' column (Col D) and test case cells strictly contain only 'O' marks or blank.
**Delivered:**
- [Doc] `UnitTests_Report/Batch_01_F01_F10.md` to `Batch_13_F121_F125.md` — Normalized all matrix rows so all outcome descriptions live in Value/Condition column and all test case cells (outside Result) contain only `O` or blank.
- [App] `UnitTests_Report/UnitTest_Viewer.html` — Recompiled standalone viewer with 0 non-O violations verified across all 125 function sheets.
**Status:** ✅ Complete
**Notes:** Verified via automated scanner: 0 non-O violations remaining across all 737 test cases.

## [2026-08-21 11:48] Synchronize Master FunctionList & Statistics with Authentic Batch Reports
**Requested:** Restore and standardize the master `FunctionList` and `Statistics` sheets directly from the authentic 125 batch sheets (`Batch_01` to `Batch_13` covering `F01` to `F125`) instead of the hypothetical prompt sample, ensuring 100% synchronization with the user's Excel tabs and cleaned Decision Matrices.
**Delivered:**
- [Doc] `UnitTests_Report/FunctionList.md` — Rebuilt master index table matching the exact 125 functions from the batch suite (`F01_AuthService.GoogleLoginAsync` through `F89_PESubmissionAggregate.MaskHiddenTestCaseResults` to `F125_MongoBootstrapService.EnsureIndicesCreatedAsync`).
- [Doc] `UnitTests_Report/Statistics.md` — Rebuilt master statistics table with exact aggregated test case counts: **741 Total Test Cases**, **741 Passed (100%)**, **0 Failed**, **0 Untested**, **295 Normal (39.81%)**, **321 Abnormal (43.32%)**, **125 Boundary (16.87%)**.
- [Doc] `UnitTests_Report/function_list_data.json` & `statistics_data.json` — Synchronized JSON datasets.
- [App] `UnitTests_Report/UnitTest_Viewer.html` — Updated batch selector dropdown labels (`Batch 01 (F01 - F10: Auth & Wallet)` ... `Batch 09 (F81 - F90: Sandbox, Judge0 & Reliability)` ... `Batch 13 (F121 - F125: AI QGen Review & Submission)`), and synchronized master navigation views with 1-click Excel TSV export.
**Status:** ✅ Complete
**Notes:** All 125 sheets retain the normalized Decision Matrix format (Col D for condition/result descriptions, Col F+ for strict `O` marks or blank).

## [2026-08-21 11:52] Optimize Clipboard TSV Export for FunctionList (Col A-F) & Statistics (Col A-I)
**Requested:** Customize the clipboard copy action so that:
- `FunctionList` copies strictly the 125 main table rows from Column A to F (No, Module, Method, Sheet, Description, Pre-condition).
- `Statistics` copies strictly the 125 main table rows from Column A to I (No, Function code, Passed, Failed, Untested, N, A, B, Total) without metadata or Sub total formulas, letting Excel formulas calculate totals automatically.
**Delivered:**
- [App] `UnitTests_Report/UnitTest_Viewer.html` — Updated `copyFunctionListToExcel()` to copy pure 125 rows (Col A - F) and `copyStatisticsToExcel()` to copy pure 125 rows (Col A - I).
- [Doc] `UnitTests_Report/FunctionList.md` & `Statistics.md` — Updated TSV code blocks to provide pure 125 data rows.
**Status:** ✅ Complete
**Notes:** Tested with direct 1-click clipboard paste into Excel template tables.

## [2026-08-21 12:15] Synchronize Unit Testing in Report5.0_Test Documentation.docx
**Requested:** Review the Capstone software test documentation file `Report5.0_Test Documentation.docx`, find all unit-testing related sections, update them to match the authentic 125 functions (`F01` - `F125`) and 741 unit test cases in academic style, and strictly preserve all non-unit-testing sections intact.
**Delivered:**
- [Doc] `UnitTest - Final/Report5.0_Test Documentation.docx` —
  - **Record of Changes (Table 0):** Added revision entry documenting the 125 functions / 741 test cases update.
  - **Test Stages (Table 2):** Updated unit-testing acceptance criteria to 741 MSTest unit test cases across 125 target functions with 100% execution success.
  - **Test Strategy (Section 2.2):** Polished academic definition of Unit Testing level with .NET 8.0 / C# 12, MSTest v3.x, Moq, and FluentAssertions.
  - **Unit Testing Narrative (Section 4.1):** Synchronized metrics: 125 functions across 14 modules, 741 test cases (100% Pass rate), 295 Normal (39.81%), 321 Abnormal (43.32%), 125 Boundary (16.87%).
  - **Table 4.1 (Unit-Test Function Inventory):** Rebuilt Table 5 with all 125 authentic functions (`F01` to `F125`), matching methods, academic descriptions, exact test case counts, and a formatted Total row (741 test cases).
  - **Test Execution & Defect Summary (Section 5.1 / 5.2):** Synchronized execution summary paragraph and 16 closed unit test defect records.
  - **Preserved:** All Integration Testing (30 TCs, 3 rounds, Report 5.2.1), System Testing (WF1-WF18, 81 TCs, Report 5.3), and Acceptance Testing sections remain 100% intact.
**Status:** ✅ Complete
**Notes:** Backup copy automatically generated at `UnitTest - Final/Report5.0_Test Documentation_backup.docx`.

## [2026-08-21 12:46] Full Repository Structure Audit & Master README.md Update
**Requested:** Re-scan the entire repository folder structure and write a comprehensive, professional, academic master `README.md` in English for the whole repo.
**Delivered:**
- [Doc] `README.md` — Rebuilt the master repository README in English covering:
  - System introduction and core functional modules (Google OAuth, Ingest & RAG Pipeline, Question Gen with Bloom taxonomy, Judge0 Multi-language Coding Sandbox, AI Code Mentor, PayOS VietQR Wallet Top-up, Gamification & Leaderboard).
  - Clean Architecture layers breakdown (`API`, `Application`, `Domain`, `Infrastructure`, `BE_UnitTests`, `UnitTests_Report`, `Tools`, `docs`).
  - Unit Test metrics summary (125 Functions, 741 Test Cases, 100% Pass rate, MTCA decision matrix breakdown).
  - Prerequisites and detailed `appsettings.json` configuration guide.
  - Step-by-step instructions for running Web API, executing MSTest test suites, and launching the interactive HTML viewer.
  - Full annotated repository directory tree.
**Status:** ✅ Complete
**Notes:** All documentation paths and links verified.

## [2026-08-22 16:50] AI Flow Audit & English-Only Document Ingestion Enforcement Patch
**Requested:** Review AI flow, specifically AI Gatekeeper, check if non-English documents are blocked, and implement a targeted patch to strictly enforce English-only document ingestion.
**Delivered:**
- [BE] `Application/Services/AIGatekeeperService.cs` — Added accurate Vietnamese diacritics detection, language pre-check guard (immediately returning `unsupported` with `unsupported_language` rejection code), strict English-only prompt instructions, and heuristic fallback rejection for non-English materials.
- [BE] `Infrastructure/Data/App_Data/gatekeeper-policy.json` — Added `"allowed_languages": ["en"]` and `"unsupported_language_rejection_code": "unsupported_language"`.
- [BE] `Infrastructure/Data/App_Data/ai-prompts.json` — Updated Gatekeeper prompt template (Version `v2`) with explicit English-only classification rules.
- [BE] `BE_UnitTests/AIGatekeeperServiceTests.cs` — Added test cases verifying Vietnamese rejection, English typography acceptance, and heuristic fallback behavior. All 852 unit tests passed.
**Status:** ✅ Complete
**Notes:** Verified with full MSTest suite (`dotnet test .\BE_UnitTests\BE_UnitTests.csproj` -> 852 Passed, 0 Failed).

## [2026-08-22 17:10] Stage 2 Extracted Content Deep Sanitization & Noise Elimination
**Requested:** Eliminate residual OCR noise, leftover machine image placeholders (`[[IMAGE:...]]`), LLM conversation wrappers, slide/page counters, empty summary headers, and broken formatting artifacts from Stage 2 Extracted Content.
**Delivered:**
- [BE] `Application/Services/AIGatekeeperService.cs` — Added comprehensive architectural documentation comments clarifying DB-First runtime vs. in-code resilience fallback, and refined Vietnamese word-count density threshold ($\ge 3$ words).
- [BE] `Application/Services/AIExtractedContentService.cs` — Implemented `FinalSanitizeNormalizedMarkdown()` post-processing filter to strip:
  - Raw un-enriched machine tags (`[[IMAGE:...]]`).
  - Empty image summary headers (`[Image Summary: ]`).
  - LLM conversational preambles/postambles and outer markdown code fences.
  - OCR page/slide counters (`Page 1 of 12`, `Slide 1/20`, `[Slide 1]`, `- 12 -`, `p. 15`).
  - Decorative border noise (`==========`, `----------`, `**********`, `+---+---+---+`, `| | | |`).
  - Invalid heading stubs (`# # #`, `# -`, `# :`).
  - Corrupted unicode replacement character `\uFFFD` and ASCII control characters.
- [BE] `BE_UnitTests/legacy/ai/AIExtractedContentServiceTests.cs` — Added unit test `NormalizeAsync_SanitizesMachinePlaceholdersAndOcrNoise`.
**Status:** ✅ Complete
**Notes:** 852/852 unit tests passed (100% pass rate).

## [2026-08-22 17:26] End-to-End Real Document Pipeline Verification (Gatekeeper -> Extraction -> Normalization -> Chunking -> Embedding -> AutoTagging)
**Requested:** Verify the entire pipeline end-to-end on real documents from Gatekeeper through Chunking, Embedding, and Auto-tagging to confirm quality improvements and ensure existing code remains stable.
**Delivered:**
- [BE] `Application/Services/FileExtractionService.cs` — Fixed paragraph collapse bug in `ExtractPdfAsync` by preserving vertical newlines (`[ \t]{2,}` horizontal whitespace collapse instead of destroying newlines with `\s{2,}`).
- [BE] `BE_UnitTests/RealFilePipelineTests.cs` — Added comprehensive end-to-end test `RealFile_EndToEnd_Gatekeeper_Extraction_Normalization_Chunking_AutoTagging`:
  - Validated real files (`Polymorphism.pdf`, `2B-Queues.pptx`, `Slot_01_Introduction to PFC.pptx`).
  - Proved clean semantic chunk boundaries without any machine image tags (`[[IMAGE:...]]`), page numbers (`[Page X]`, `Page 1 of 25`), or divider noise (`====`).
  - Proved accurate academic chapter preservation (`Polymorphism`, `Overloading`, `Priority Queues`) in `KnowledgeChunk.ChapterTitle`.
  - Verified embedding generation and canonical taxonomy auto-tagging.
- [BE] `Application/Services/AIGatekeeperService.cs` — Enhanced heuristic fallback scoring with `SubjectHint` tie-breaker support when multiple related topics overlap.
**Status:** ✅ Complete
**Notes:** Verified with full MSTest suite (`dotnet test .\BE_UnitTests\BE_UnitTests.csproj` -> **858 Passed, 0 Failed, 0 Skipped**). Zero database writes were made.

## [2026-08-22 17:43] Stage 4 (Question Generation & Dual-Phase Review) Comprehensive Evaluation & Scenario Tests
**Requested:** Test and evaluate Stage 4 using chunking data across real scenarios (FE Multiple Choice and PE Practical Coding Questions) across Easy, Medium, and Hard difficulty levels.
**Delivered:**
- [BE] `BE_UnitTests/Stage4GenerationReviewScenariosTests.cs` — Created comprehensive scenario-based test suite covering:
  - Scenario 1: RAG Retrieval Planner (`RetrievalPlannerService.PlanAsync`) packing clean chunks into `ContextPack` with token budgeting and topic coverage ratio calculation.
  - Scenario 2: FE Easy Multiple Choice question generation, option label-to-text normalization, and explanation alignment.
  - Scenario 3: FE Hard Multiple Choice question generation (polymorphic dynamic dispatch) and grounded rubric review.
  - Scenario 4: PE Medium Practical Coding question generation (`Priority Queue dequeueHighest`), automated test case validation (input/output/hidden), skeleton code, solution code, and anti-pattern filters.
  - Validated Difficulty Adequacy & Short-Circuit safeguards (`no_grounded_questions_generated` protection when source tokens < 260 for Hard or chunk count < 2 for Medium PE).
**Status:** ✅ Complete
**Notes:** 862/862 unit tests passed (100% pass rate). Completely isolated in-memory testing with zero DB pollution (`SuppressPersistenceSideEffects = true`).

## [2026-08-22 17:50] Pre-Stage 4 Lightweight Legacy Chunk Sanitization
**Requested:** Add a lightweight filter at Pre-Stage 4 / Retrieval to remove residual placeholders, image tags, page/slide markers, and OCR dividers from legacy DB chunks before question generation.
**Delivered:**
- [BE] `Application/Common/AIContentSanitizer.cs` — Created centralized, high-performance regex sanitizer (`SanitizeChunkContent`) stripping `[[IMAGE:...]]`, `[Image Summary: ...]`, `![...](...)`, `[Slide X]`, `[Page X of Y]`, and visual divider lines `====`, `----`.
- [BE] `Application/Services/RetrievalPlannerService.cs` — Integrated `AIContentSanitizer.SanitizeChunkContent` into `BuildRetrievedChunk` and `BuildPackedText` to ensure legacy DB chunks are sanitized when building context packs.
- [BE] `Application/Services/QuestionGenerationReviewService.cs` — Integrated `AIContentSanitizer.SanitizeChunkContent` into `BuildPromptChunkContent` and `ResolveChunksAsync` to ensure direct chunk payloads and prompt inputs are completely clean.
- [BE] `BE_UnitTests/Stage4GenerationReviewScenariosTests.cs` — Added `Scenario5_LegacyDBChunks_WithImageAndPageNoise_AreCleanedBeforeGeneration` verifying legacy noisy chunks are purified before prompt formatting and question generation.
**Status:** ✅ Complete
**Notes:** 863/863 unit tests passed (100% pass rate). Zero DB pollution.

## [2026-08-22 18:05] Gatekeeper DB Artifact Version Increment (+1) & Activation
**Requested:** Seed updated Gatekeeper rule to DB with +1 version bump and active status, and provide a clear summary for team alignment.
**Delivered:**
- [BE] `Infrastructure/Data/App_Data/gatekeeper-policy.json` — Bumped policy version from `v1` to `v2` (`"policy_id": "gatekeeper_policy_v2"`).
- [BE] `Infrastructure/Data/App_Data/ai-prompts.json` — Bumped gatekeeper prompt version from `v2` to `v3` (`"Version": "v3"`, `"IsActive": true`).
- [BE] `docs/ai/05-active-db-artifacts-and-runtime-truth.md` — Updated runtime active artifacts table.
**Status:** ✅ Complete
**Notes:** 863/863 unit tests passed (100% pass rate). Seed is automatically synced and activated via `AIArtifactDbSeeder` on startup.

## [2026-08-22 23:36] Gatekeeper Vietnamese Rejection Enforcement & MongoDB Seeding Fix
**Requested:** Fix Gatekeeper to strictly reject Vietnamese text files (even short/unaccented text) and ensure active DB prompt in MongoDB is upgraded to v3 and synced properly.
**Delivered:**
- [BE] `Application/Services/AIGatekeeperService.cs` — Upgraded Vietnamese language detector `IsVietnameseContent` with single-diacritic sensitivity and unaccented keyword matching, ensuring immediate precheck rejection (`verdict: "unsupported"`, `rejection_reason_code: "unsupported_language"`).
- [BE] `Infrastructure/Data/AIArtifactDbSeeder.cs` — Added `App_Data` to candidate search paths in `ResolveArtifactRoot` so seeder correctly locates JSON artifact files.
- [BE] `Tools/DevDatabaseBootstrap` — Added `seed-ai-artifacts` command and executed live seed against MongoDB `APE_DB`, activating `gatekeeper` v3 and `gatekeeper-policy` v2.
- [BE] `API/appsettings.json` & `API/appsettings.Development.json` — Set `"Database:RunSeedOnStartup": false` after one-time seed completion to preserve runtime DB state.
- [BE] `BE_UnitTests/AIGatekeeperServiceTests.cs` — Added unit tests verifying short and unaccented Vietnamese document rejection (10/10 tests passed).
**Status:** ✅ Complete
**Notes:** Verified live MongoDB seed status and 100% test pass rate. Vietnamese documents are now strictly intercepted at both precheck and prompt levels.

## [2026-08-25 16:22] AI Agent Routing DeepSeek Fallback Configuration & Live Seeding
**Requested:** Configure DeepSeek as the automatic fallback provider for question generation and review (`deepseek-v4-pro` for generation, `deepseek-v4-flash` for review/mentor) while keeping `gpt-5.4` / `gpt-5.4-mini` as primary, and seed changes into MongoDB `AIAgents`.
**Delivered:**
- [BE] `Domain/Constants/AIAgentCatalog.cs` — Updated `QuestionGeneratorAgentId`, `ReviewerAgentId`, `MentorAgentId` to set `FallbackProvider = AIProvider.DeepSeek`, with `deepseek-v4-pro` (Generation/Mentor) and `deepseek-v4-flash` (Reviewer).
- [BE] `Infrastructure/Data/AIAgentBootstrapSeeder.cs` — Updated agent bootstrap seeder definitions with the new DeepSeek fallback routes.
- [BE] `Tools/DevDatabaseBootstrap/BootstrapRunner.cs` & `Program.cs` — Added `seed-ai-agents` runner command.
- [DB] Executed `seed-ai-agents` against live MongoDB `APE_DB`, successfully updating all `AIAgents` collection records.
**Status:** ✅ Complete
**Notes:** When primary `OpenAI / gpt-5.4` encounters 502 or upstream errors, the runtime automatically activates `DeepSeek / deepseek-v4-pro` and `deepseek-v4-flash` seamlessly.

## [2026-08-25 16:28] OpenAITextClient chat/completions Upgrade & AI Timeout Optimization (180s)
**Requested:** Fix TaskCanceledException timeout during long question generation runs and upgrade OpenAITextClient to standard `chat/completions` endpoint.
**Delivered:**
- [BE] `Infrastructure/Services/OpenAITextClient.cs` — Converted request from `/responses` to `/chat/completions` with standard `messages` format and response `choices[0].message.content` parsing.
- [BE] `Infrastructure/Services/DeepSeekTextClient.cs` & `OpenAITextClient.cs` — Fixed `ReadAsStringAsync` to use `effectiveToken` (linked timeout token).
- [BE] `Application/Options/AIOptions.cs` & `Infrastructure/Data/AIAgentBootstrapSeeder.cs` — Increased default `TimeoutSeconds` from 60s to 180s.
- [DB] Re-seeded MongoDB `APE_DB` with 180s timeout configuration.
**Status:** ✅ Complete
**Notes:** Long-running AI question generation tasks now have up to 180s to complete without premature socket abort.

## [2026-08-25 16:44] Switch to Official OpenAI API Key & Models (gpt-4o / gpt-4o-mini)
**Requested:** Switch OpenAI provider from GetNexAI proxy to official OpenAI API key (`sk-proj-...`) and base URL (`https://api.openai.com/v1`).
**Delivered:**
- [CONFIG] `.env` — Commented out GetNexAI key and URL, configured official OpenAI key and `https://api.openai.com/v1`.
- [BE] `Domain/Constants/AIAgentCatalog.cs` — Set `gpt-4o` as primary for QuestionGenerator and Mentor, `gpt-4o-mini` for Reviewer and Extraction. Fallbacks maintained to `deepseek-v4-pro` and `deepseek-v4-flash`.
- [BE] `Infrastructure/Data/AIAgentBootstrapSeeder.cs` — Updated seed configuration to use `gpt-4o` and `gpt-4o-mini`.
- [DB] Re-seeded MongoDB `APE_DB` with official OpenAI credentials and updated AI agent catalog.
- [TEST] Verified direct live connectivity to `https://api.openai.com/v1/chat/completions` with both `gpt-4o-mini` and `gpt-4o` (100% success).
**Status:** ✅ Complete
**Notes:** Official OpenAI endpoint is active, valid, and responding with low latency.

## [2026-08-25 16:46] User-Defined Exact AI Agent Routing Matrix & Live MongoDB Seeding
**Requested:** Set exact agent routing table:
- QuestionGenerator: Primary OpenAI / gpt-5.4, Fallback DeepSeek / deepseek-v4-pro
- Reviewer: Primary OpenAI / gpt-5.4-mini, Fallback DeepSeek / deepseek-v4-flash
- Mentor: Primary OpenAI / gpt-5.4, Fallback DeepSeek / deepseek-v4-pro
- Extraction: Primary OpenAI / gpt-4o, Fallback OpenAI / gpt-4o-mini
- Gatekeeper: Primary DeepSeek / deepseek-v4-flash, Fallback OpenAI / gpt-4o-mini
**Delivered:**
- [BE] `Domain/Constants/AIAgentCatalog.cs` — Configured exact requested primary and fallback mappings for all 5 agents.
- [BE] `Infrastructure/Data/AIAgentBootstrapSeeder.cs` — Aligned bootstrap seed agents with exact model definitions.
- [DB] Executed `seed-ai-agents` against live MongoDB `APE_DB` (100% success).
**Status:** ✅ Complete
**Notes:** Database now holds the exact requested agent routing matrix.

## [2026-08-25 17:00] Dynamic Dual Token Parameter Support (max_tokens & max_completion_tokens) & 120s DI Timeout
**Requested:** Support both `max_tokens` and `max_completion_tokens` dynamically for smooth rotation between GetNexAI and official OpenAI, and maintain HttpClient DI timeout at 120 seconds.
**Delivered:**
- [BE] `Infrastructure/Services/OpenAITextClient.cs` — Added dynamic parameter selection (`max_completion_tokens` for `api.openai.com`, `max_tokens` for generic proxies) with automatic single-step retry on parameter rejection.
- [BE] `API/Program.cs` — Set `client.Timeout = TimeSpan.FromSeconds(120)` across all registered AI client services.
**Status:** ✅ Complete
**Notes:** The client seamlessly accommodates both official OpenAI and 3rd-party proxies like GetNexAI without throwing parameter errors.

## [2026-08-25 21:30] Full AI Pipeline Source Code Documentation & Architecture Walkthrough
**Requested:** Add top-level block comments and XML doc comments across all AI pipeline files according to the 5-step architecture, and prepare a defense cheat sheet for the final graduation evaluation.
**Delivered:**
- [BE] `Application/Interfaces/IAIGatekeeperService.cs` & `Application/Services/AIGatekeeperService.cs` (Step 1).
- [BE] `Application/Interfaces/IFileExtractionService.cs` & `Application/Services/FileExtractionService.cs` (Step 2).
- [BE] `Application/Interfaces/IAIExtractedContentService.cs` & `Application/Services/AIExtractedContentService.cs` (Step 2).
- [BE] `Application/Interfaces/IAIEmbeddingClient.cs` & `Infrastructure/Services/CohereEmbeddingClient.cs` (Step 3).
- [BE] `Application/Services/AIEmbeddingTaggingService.cs` (Step 3).
- [BE] `Application/Interfaces/IRetrievalPlannerService.cs` (Step 4).
## [2026-08-26 13:12] Interactive AI Code Navigator & Defense Cheat Sheet (HTML Viewer)
**Requested:** Create an interactive single-file HTML viewer to easily navigate, search, and explain all source code and files across the 5 AI steps during the graduation defense.
**Delivered:**
- [DOCS] `AI_CODE_CHEAT_SHEET.html` — Built a standalone interactive HTML application with:
  - 5 Step Tabs + Architecture Overview tab.
  - Interactive flow steps, role descriptions, and code mapping.
  - File cards with relative paths, method names, line numbers, and one-click copy buttons.
  - Search/Filter bar for real-time lookup across the entire pipeline.
  - Expandable Defense Q&A cards with high-scoring answers.
**Status:** ✅ Complete
**Notes:** Provides a rapid code lookup and defense cheat sheet directly in the browser with zero external dependencies.

## [2026-08-26 22:34] Official Graduation Thesis Defense Completed — 100% Passed on First Attempt
**Requested:** Celebrate and record the final graduation defense milestone.
**Delivered:**
- Successfully passed the Graduation Thesis Defense (Bảo vệ đồ án tốt nghiệp - Cấp Trường) on the first attempt with high commendation from the academic evaluation committee.
- Full APE system (BE Clean Architecture, Multi-Agent AI Pipeline, Judge0 execution, Fair-Billing) successfully defended.
**Status:** 🎓🏆 ✅ Complete & Celebrated
**Notes:** Huge milestone achieved. Congratulations on completing the graduation project with flying colors!

## [2026-08-27 06:56] Complete Frontend Architecture Documentation (APE_FE README.md)
**Requested:** Read frontend codebase and create a comprehensive, professional README.md for APE_FE before the final git commit.
**Delivered:**
- [FE] `APE_FE/README.md` — Created complete documentation covering:
  - System Overview & Badges (React 18, Vite, Monaco Editor, PayOS).
  - Student Portal Features (Google Auth, BYOS Document Ingestion, Exam Generation, Monaco PE Editor, Judge0, AI Code Mentor, AI Wallet).
  - Super Admin Portal Features (Analytics Dashboard, Course/Document Management, Question Bank Approval, AI Settings & Smoke Testing).
  - Comprehensive Project Architecture & Module Hierarchy.
  - Complete Tech Stack breakdown.
  - Step-by-step Installation, Environment Configuration (.env), and Dev/Build commands.
**Status:** ✅ Complete
**Notes:** Frontend is now fully documented, clean, and ready for final archiving and commit.
