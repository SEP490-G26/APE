# Statistics - Unit Test Report Summary & Metrics

## UNIT TEST REPORT

**Project Name:** APE - AI-Powered Practice & Programming Evaluation System  
**Project Code:** SEP490_G26  
**Document Code:** SEP490_G26_Test Report_vx.x  
**Creator:** Developer  
**Reviewer/Approver:** Tester  
**Issue Date:** 2026-08-21  
**Notes:** Full test release covering all 125 functions across 14 Backend Modules  

---

## Summary Statistics Table (F01 - F125)

| No | Function code | Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | `F01_AuthService.GoogleLoginAsync` | **9** | 0 | 0 | 3 | 5 | 1 | **9** |
| 2 | `F02_AuthService.RefreshTokenAsync` | **10** | 0 | 0 | 1 | 8 | 1 | **10** |
| 3 | `F03_AuthService.RevokeTokenAsync` | **7** | 0 | 0 | 2 | 4 | 1 | **7** |
| 4 | `F04_UserService.GetProfileAsync` | **8** | 0 | 0 | 3 | 3 | 2 | **8** |
| 5 | `F05_UserController.GetUsers` | **7** | 0 | 0 | 3 | 2 | 2 | **7** |
| 6 | `F06_UserController.GetUserById` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 7 | `F07_UserController.UpdateUserStatus` | **7** | 0 | 0 | 2 | 4 | 1 | **7** |
| 8 | `F08_WalletTopupService.GetConstraints` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 9 | `F09_WalletTopupService.GetPackagesAsync` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 10 | `F10_WalletTopupService.CreateTopupAsync` | **8** | 0 | 0 | 2 | 5 | 1 | **8** |
| 11 | `F11_WalletTopupService.GetHistoryAsync` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 12 | `F12_WalletTopupService.GetByIdAsync` | **5** | 0 | 0 | 2 | 3 | 0 | **5** |
| 13 | `F13_WalletTopupService.CancelTopupAsync` | **6** | 0 | 0 | 2 | 4 | 0 | **6** |
| 14 | `F14_WalletTopupService.HandlePayOSWebhookAsync` | **7** | 0 | 0 | 1 | 5 | 1 | **7** |
| 15 | `F15_PaymentWebhookController.ReceiveWebhook` | **4** | 0 | 0 | 2 | 2 | 0 | **4** |
| 16 | `F16_StudentWalletController.GetHistory` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 17 | `F17_StudentWalletController.GetDetail` | **5** | 0 | 0 | 2 | 3 | 0 | **5** |
| 18 | `F18_StudentWalletController.Cancel` | **5** | 0 | 0 | 2 | 3 | 0 | **5** |
| 19 | `F19_StudentWalletController.GetAiTransactions` | **7** | 0 | 0 | 3 | 2 | 2 | **7** |
| 20 | `F20_DocumentService.UploadDocumentAsync` | **8** | 0 | 0 | 3 | 4 | 1 | **8** |
| 21 | `F21_DocumentService.GetExtractionDraftAsync` | **4** | 0 | 0 | 2 | 2 | 0 | **4** |
| 22 | `F22_DocumentService.ListByUserAsync` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 23 | `F23_DocumentController.Download` | **4** | 0 | 0 | 2 | 2 | 0 | **4** |
| 24 | `F24_DocumentController.GetExtractionDraft` | **4** | 0 | 0 | 2 | 2 | 0 | **4** |
| 25 | `F25_FileExtractionService.ExtractTextAsync` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 26 | `F26_CourseService.CreateCourseAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 27 | `F27_CourseService.UpdateCourseAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 28 | `F28_CourseService.DeleteCourseAsync` | **5** | 0 | 0 | 1 | 4 | 0 | **5** |
| 29 | `F29_CourseService.GetPagedAsync` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 30 | `F30_CourseController.GetList` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 31 | `F31_CourseController.Create` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 32 | `F32_CourseController.Update` | **4** | 0 | 0 | 2 | 2 | 0 | **4** |
| 33 | `F33_CourseController.Delete` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 34 | `F34_StudentCourseController.List` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 35 | `F35_StudentDocumentController.List` | **4** | 0 | 0 | 2 | 2 | 0 | **4** |
| 36 | `F36_StudentDocumentController.GetExtractionDraft` | **4** | 0 | 0 | 1 | 3 | 0 | **4** |
| 37 | `F37_PracticeService.StartSessionAsync & GetSessionAsync` | **8** | 0 | 0 | 3 | 4 | 1 | **8** |
| 38 | `F38_PracticeController.Start & GetSession` | **8** | 0 | 0 | 4 | 2 | 2 | **8** |
| 39 | `F39_FESubmissionService.CreateAndEvaluateAsync` | **8** | 0 | 0 | 3 | 3 | 2 | **8** |
| 40 | `F40_SubmissionController.SubmitFE & GetSessionSubmissions` | **11** | 0 | 0 | 6 | 3 | 2 | **11** |
| 41 | `F41_ExamService.CreatePracticeExamAsync` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 42 | `F42_ExamService.GetExamDetailAsync` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 43 | `F43_ExamController.CreatePracticeExam` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 44 | `F44_ExamController.GetDetail` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 45 | `F45_PESubmissionService.SubmitCodingAsync` | **9** | 0 | 0 | 5 | 2 | 2 | **9** |
| 46 | `F46_PECodeRunService.ExecuteDryRunAsync` | **7** | 0 | 0 | 2 | 4 | 1 | **7** |
| 47 | `F47_SubmissionController.SubmitPE` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 48 | `F48_SubmissionController.RunCode` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 49 | `F49_ExecutionFeedbackDiagnosticParser.Parse` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 50 | `F50_CodeMentorService.GetHintAsync` | **7** | 0 | 0 | 3 | 3 | 1 | **7** |
| 51 | `F51_SubmissionController.GetMentorHint` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 52 | `F52_GamificationService.RecordPracticeActivityAsync` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 53 | `F53_GamificationService.GetStudentStatsAsync` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 54 | `F54_StudentAnalyticsGamificationController.GetStats` | **4** | 0 | 0 | 2 | 2 | 0 | **4** |
| 55 | `F55_StudentAnalyticsGamificationController.GetLeaderboard` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 56 | `F56_QuestionService.ListQuestionsAsync` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 57 | `F57_QuestionController.ListQuestions` | **6** | 0 | 0 | 4 | 1 | 1 | **6** |
| 58 | `F58_QuestionController.CreateQuestion` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 59 | `F59_QuestionGenerationReviewService.GenerateQuestionsAsync` | **7** | 0 | 0 | 3 | 3 | 1 | **7** |
| 60 | `F60_AIQuestionGenerationController.GenerateDraft` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 61 | `F61_AIRetrievalController.SearchDocuments` | **4** | 0 | 0 | 1 | 2 | 1 | **4** |
| 62 | `F62_AIRetrievalController.GetSystemDocuments` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 63 | `F63_RetrievalPlannerService.SearchAsync` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 64 | `F64_RetrievalPlannerService.GetSystemDocumentsAsync` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 65 | `F65_RetrievalPlannerService.GetCachedContextAsync` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 66 | `F66_StudentDocumentController.BrowsePublicDocuments` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 67 | `F67_StudentDocumentController.Search` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 68 | `F68_AIVndBillingService.GetRatesAsync` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 69 | `F69_AIVndBillingService.EnsureMinimumBalanceAsync` | **4** | 0 | 0 | 1 | 2 | 1 | **4** |
| 70 | `F70_AIVndBillingService.ChargeTokensAsync` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 71 | `F71_AIFeatureRoutingAndPromptService.ResolveRouteAsync` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 72 | `F72_AIGatekeeperService.CheckAccessAndThrottleAsync` | **5** | 0 | 0 | 1 | 3 | 1 | **5** |
| 73 | `F73_AIProviderModelCatalogService.GetActiveModelsAsync` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 74 | `F74_AdminAIController.GetCatalog` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 75 | `F75_AdminAIController.UpdateModelConfig` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 76 | `F76_AdminAIController.GetPricing` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 77 | `F77_AdminAIController.UpdatePricing` | **6** | 0 | 0 | 1 | 4 | 1 | **6** |
| 78 | `F78_AdminAIController.GetUsageStats` | **8** | 0 | 0 | 4 | 2 | 2 | **8** |
| 79 | `F79_AdminAIController.GetRoutingRules` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 80 | `F80_AdminAIController.UpdateRoutingRules` | **6** | 0 | 0 | 1 | 4 | 1 | **6** |
| 81 | `F81_Judge0ClientAdapter.SubmitAsync` | **8** | 0 | 0 | 4 | 3 | 1 | **8** |
| 82 | `F82_Judge0ClientAdapter.GetSubmissionResultAsync` | **8** | 0 | 0 | 5 | 2 | 1 | **8** |
| 83 | `F83_Judge0ClientAdapter.MapLanguageId` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 84 | `F84_Judge0Options.Validate` | **6** | 0 | 0 | 1 | 4 | 1 | **6** |
| 85 | `F85_SubmissionGradingReliability.ExecuteWithRetryAsync` | **7** | 0 | 0 | 3 | 3 | 1 | **7** |
| 86 | `F86_SubmissionGradingReliability.RecoverZombieSubmissionsAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 87 | `F87_SubmissionGradingReliability.HandleTimeoutAsync` | **6** | 0 | 0 | 1 | 4 | 1 | **6** |
| 88 | `F88_PESubmissionAggregate.CalculateTotalScore` | **7** | 0 | 0 | 4 | 1 | 2 | **7** |
| 89 | `F89_PESubmissionAggregate.MaskHiddenTestCaseResults` | **6** | 0 | 0 | 4 | 1 | 1 | **6** |
| 90 | `F90_PESubmissionAggregate.EvaluatePassedStatus` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 91 | `F91_SubmissionGradingLeaseFlow.AcquireLeaseAsync` | **7** | 0 | 0 | 2 | 4 | 1 | **7** |
| 92 | `F92_SubmissionGradingLeaseFlow.RenewLeaseHeartbeatAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 93 | `F93_SubmissionGradingLeaseFlow.ReleaseLeaseAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 94 | `F94_PESubmissionRepository.CompareAndSwapStatusAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 95 | `F95_PESubmissionRepository.AllocateAttemptIndexAsync` | **7** | 0 | 0 | 3 | 2 | 2 | **7** |
| 96 | `F96_GoogleAuthService.ValidateIdTokenAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 97 | `F97_JwtTokenGenerator.GenerateAccessToken` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 98 | `F98_JwtTokenGenerator.GenerateRefreshToken` | **6** | 0 | 0 | 3 | 1 | 2 | **6** |
| 99 | `F99_SecurityLogger.LogAuthenticationEvent` | **5** | 0 | 0 | 3 | 1 | 1 | **5** |
| 100 | `F100_DevDatabaseBootstrap.SeedInitialDataAsync` | **8** | 0 | 0 | 4 | 2 | 2 | **8** |
| 101 | `F101_AuthController.GoogleLogin` | **7** | 0 | 0 | 2 | 4 | 1 | **7** |
| 102 | `F102_AuthController.RefreshToken` | **6** | 0 | 0 | 1 | 4 | 1 | **6** |
| 103 | `F103_AuthController.Logout` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 104 | `F104_AuthController.Me` | **6** | 0 | 0 | 3 | 2 | 1 | **6** |
| 105 | `F105_AuthService.LogoutAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 106 | `F106_AuthService.ValidateOrCreateGoogleUserAsync` | **7** | 0 | 0 | 3 | 3 | 1 | **7** |
| 107 | `F107_AuthService.RotateRefreshTokenAsync` | **8** | 0 | 0 | 1 | 6 | 1 | **8** |
| 108 | `F108_UserController.GetProfile` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 109 | `F109_UserService.GetUserByIdAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 110 | `F110_UserService.GetUserByEmailAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 111 | `F111_StudentWalletController.GetConstraints` | **4** | 0 | 0 | 2 | 1 | 1 | **4** |
| 112 | `F112_StudentWalletController.CreateTopup` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 113 | `F113_WalletTopupService.ValidateTopupAmountAsync` | **6** | 0 | 0 | 1 | 4 | 1 | **6** |
| 114 | `F114_WalletTopupService.ProcessSuccessfulPaymentAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 115 | `F115_WalletTopupService.CancelPendingOrderAsync` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 116 | `F116_WalletTopupService.GetByOrderCodeAsync` | **5** | 0 | 0 | 1 | 3 | 1 | **5** |
| 117 | `F117_WalletTopupService.GetPagedTransactionsAsync` | **6** | 0 | 0 | 4 | 1 | 1 | **6** |
| 118 | `F118_PaymentWebhookController.ValidateSignature` | **5** | 0 | 0 | 2 | 2 | 1 | **5** |
| 119 | `F119_CodeExecutionEngine.ValidateContract` | **7** | 0 | 0 | 3 | 2 | 2 | **7** |
| 120 | `F120_CodeMentorService.EvaluatePromptQualityAsync` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 121 | `F121_AIQuestionGenerationController.ReviewDraft` | **6** | 0 | 0 | 1 | 4 | 1 | **6** |
| 122 | `F122_AIQuestionGenerationController.ApproveDraft` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 123 | `F123_QuestionGenerationReviewService.ValidateBloomDistribution` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 124 | `F124_SubmissionController.GetCodingDetail` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| 125 | `F125_SubmissionController.GetMcqDetail` | **6** | 0 | 0 | 2 | 3 | 1 | **6** |
| | **Sub total** | **741** | **0** | **0** | **295** | **321** | **125** | **741** |

---

## Overall Coverage Metrics

| Metric | Percentage | Ratio |
|---|:---:|:---:|
| **Test coverage** | **100.00 %** | 741 / 741 |
| **Test successful coverage** | **100.00 %** | 741 / 741 |
| **Normal case (N)** | **39.81 %** | 295 / 741 |
| **Abnormal case (A)** | **43.32 %** | 321 / 741 |
| **Boundary case (B)** | **16.87 %** | 125 / 741 |

---

## Excel Table Content TSV (Col A - I: 125 Rows)
```tsv
1	F01_AuthService.GoogleLoginAsync	9	0	0	3	5	1	9
2	F02_AuthService.RefreshTokenAsync	10	0	0	1	8	1	10
3	F03_AuthService.RevokeTokenAsync	7	0	0	2	4	1	7
4	F04_UserService.GetProfileAsync	8	0	0	3	3	2	8
5	F05_UserController.GetUsers	7	0	0	3	2	2	7
6	F06_UserController.GetUserById	6	0	0	2	3	1	6
7	F07_UserController.UpdateUserStatus	7	0	0	2	4	1	7
8	F08_WalletTopupService.GetConstraints	5	0	0	3	1	1	5
9	F09_WalletTopupService.GetPackagesAsync	6	0	0	3	2	1	6
10	F10_WalletTopupService.CreateTopupAsync	8	0	0	2	5	1	8
11	F11_WalletTopupService.GetHistoryAsync	6	0	0	3	2	1	6
12	F12_WalletTopupService.GetByIdAsync	5	0	0	2	3	0	5
13	F13_WalletTopupService.CancelTopupAsync	6	0	0	2	4	0	6
14	F14_WalletTopupService.HandlePayOSWebhookAsync	7	0	0	1	5	1	7
15	F15_PaymentWebhookController.ReceiveWebhook	4	0	0	2	2	0	4
16	F16_StudentWalletController.GetHistory	5	0	0	2	2	1	5
17	F17_StudentWalletController.GetDetail	5	0	0	2	3	0	5
18	F18_StudentWalletController.Cancel	5	0	0	2	3	0	5
19	F19_StudentWalletController.GetAiTransactions	7	0	0	3	2	2	7
20	F20_DocumentService.UploadDocumentAsync	8	0	0	3	4	1	8
21	F21_DocumentService.GetExtractionDraftAsync	4	0	0	2	2	0	4
22	F22_DocumentService.ListByUserAsync	5	0	0	3	1	1	5
23	F23_DocumentController.Download	4	0	0	2	2	0	4
24	F24_DocumentController.GetExtractionDraft	4	0	0	2	2	0	4
25	F25_FileExtractionService.ExtractTextAsync	5	0	0	3	1	1	5
26	F26_CourseService.CreateCourseAsync	6	0	0	2	3	1	6
27	F27_CourseService.UpdateCourseAsync	6	0	0	2	3	1	6
28	F28_CourseService.DeleteCourseAsync	5	0	0	1	4	0	5
29	F29_CourseService.GetPagedAsync	5	0	0	3	1	1	5
30	F30_CourseController.GetList	5	0	0	2	2	1	5
31	F31_CourseController.Create	4	0	0	2	1	1	4
32	F32_CourseController.Update	4	0	0	2	2	0	4
33	F33_CourseController.Delete	4	0	0	2	1	1	4
34	F34_StudentCourseController.List	4	0	0	2	1	1	4
35	F35_StudentDocumentController.List	4	0	0	2	2	0	4
36	F36_StudentDocumentController.GetExtractionDraft	4	0	0	1	3	0	4
37	F37_PracticeService.StartSessionAsync & GetSessionAsync	8	0	0	3	4	1	8
38	F38_PracticeController.Start & GetSession	8	0	0	4	2	2	8
39	F39_FESubmissionService.CreateAndEvaluateAsync	8	0	0	3	3	2	8
40	F40_SubmissionController.SubmitFE & GetSessionSubmissions	11	0	0	6	3	2	11
41	F41_ExamService.CreatePracticeExamAsync	6	0	0	3	2	1	6
42	F42_ExamService.GetExamDetailAsync	6	0	0	3	2	1	6
43	F43_ExamController.CreatePracticeExam	6	0	0	2	3	1	6
44	F44_ExamController.GetDetail	5	0	0	2	2	1	5
45	F45_PESubmissionService.SubmitCodingAsync	9	0	0	5	2	2	9
46	F46_PECodeRunService.ExecuteDryRunAsync	7	0	0	2	4	1	7
47	F47_SubmissionController.SubmitPE	6	0	0	2	3	1	6
48	F48_SubmissionController.RunCode	5	0	0	2	2	1	5
49	F49_ExecutionFeedbackDiagnosticParser.Parse	6	0	0	3	2	1	6
50	F50_CodeMentorService.GetHintAsync	7	0	0	3	3	1	7
51	F51_SubmissionController.GetMentorHint	6	0	0	2	3	1	6
52	F52_GamificationService.RecordPracticeActivityAsync	6	0	0	3	2	1	6
53	F53_GamificationService.GetStudentStatsAsync	5	0	0	3	1	1	5
54	F54_StudentAnalyticsGamificationController.GetStats	4	0	0	2	2	0	4
55	F55_StudentAnalyticsGamificationController.GetLeaderboard	4	0	0	2	1	1	4
56	F56_QuestionService.ListQuestionsAsync	6	0	0	3	2	1	6
57	F57_QuestionController.ListQuestions	6	0	0	4	1	1	6
58	F58_QuestionController.CreateQuestion	6	0	0	2	3	1	6
59	F59_QuestionGenerationReviewService.GenerateQuestionsAsync	7	0	0	3	3	1	7
60	F60_AIQuestionGenerationController.GenerateDraft	6	0	0	2	3	1	6
61	F61_AIRetrievalController.SearchDocuments	4	0	0	1	2	1	4
62	F62_AIRetrievalController.GetSystemDocuments	4	0	0	2	1	1	4
63	F63_RetrievalPlannerService.SearchAsync	6	0	0	3	2	1	6
64	F64_RetrievalPlannerService.GetSystemDocumentsAsync	4	0	0	2	1	1	4
65	F65_RetrievalPlannerService.GetCachedContextAsync	5	0	0	3	1	1	5
66	F66_StudentDocumentController.BrowsePublicDocuments	5	0	0	2	2	1	5
67	F67_StudentDocumentController.Search	5	0	0	2	2	1	5
68	F68_AIVndBillingService.GetRatesAsync	4	0	0	2	1	1	4
69	F69_AIVndBillingService.EnsureMinimumBalanceAsync	4	0	0	1	2	1	4
70	F70_AIVndBillingService.ChargeTokensAsync	5	0	0	3	1	1	5
71	F71_AIFeatureRoutingAndPromptService.ResolveRouteAsync	5	0	0	3	1	1	5
72	F72_AIGatekeeperService.CheckAccessAndThrottleAsync	5	0	0	1	3	1	5
73	F73_AIProviderModelCatalogService.GetActiveModelsAsync	5	0	0	2	2	1	5
74	F74_AdminAIController.GetCatalog	6	0	0	2	3	1	6
75	F75_AdminAIController.UpdateModelConfig	6	0	0	2	3	1	6
76	F76_AdminAIController.GetPricing	6	0	0	2	3	1	6
77	F77_AdminAIController.UpdatePricing	6	0	0	1	4	1	6
78	F78_AdminAIController.GetUsageStats	8	0	0	4	2	2	8
79	F79_AdminAIController.GetRoutingRules	6	0	0	3	2	1	6
80	F80_AdminAIController.UpdateRoutingRules	6	0	0	1	4	1	6
81	F81_Judge0ClientAdapter.SubmitAsync	8	0	0	4	3	1	8
82	F82_Judge0ClientAdapter.GetSubmissionResultAsync	8	0	0	5	2	1	8
83	F83_Judge0ClientAdapter.MapLanguageId	6	0	0	3	2	1	6
84	F84_Judge0Options.Validate	6	0	0	1	4	1	6
85	F85_SubmissionGradingReliability.ExecuteWithRetryAsync	7	0	0	3	3	1	7
86	F86_SubmissionGradingReliability.RecoverZombieSubmissionsAsync	6	0	0	2	3	1	6
87	F87_SubmissionGradingReliability.HandleTimeoutAsync	6	0	0	1	4	1	6
88	F88_PESubmissionAggregate.CalculateTotalScore	7	0	0	4	1	2	7
89	F89_PESubmissionAggregate.MaskHiddenTestCaseResults	6	0	0	4	1	1	6
90	F90_PESubmissionAggregate.EvaluatePassedStatus	6	0	0	2	3	1	6
91	F91_SubmissionGradingLeaseFlow.AcquireLeaseAsync	7	0	0	2	4	1	7
92	F92_SubmissionGradingLeaseFlow.RenewLeaseHeartbeatAsync	6	0	0	2	3	1	6
93	F93_SubmissionGradingLeaseFlow.ReleaseLeaseAsync	6	0	0	2	3	1	6
94	F94_PESubmissionRepository.CompareAndSwapStatusAsync	6	0	0	2	3	1	6
95	F95_PESubmissionRepository.AllocateAttemptIndexAsync	7	0	0	3	2	2	7
96	F96_GoogleAuthService.ValidateIdTokenAsync	6	0	0	2	3	1	6
97	F97_JwtTokenGenerator.GenerateAccessToken	6	0	0	3	2	1	6
98	F98_JwtTokenGenerator.GenerateRefreshToken	6	0	0	3	1	2	6
99	F99_SecurityLogger.LogAuthenticationEvent	5	0	0	3	1	1	5
100	F100_DevDatabaseBootstrap.SeedInitialDataAsync	8	0	0	4	2	2	8
101	F101_AuthController.GoogleLogin	7	0	0	2	4	1	7
102	F102_AuthController.RefreshToken	6	0	0	1	4	1	6
103	F103_AuthController.Logout	5	0	0	2	2	1	5
104	F104_AuthController.Me	6	0	0	3	2	1	6
105	F105_AuthService.LogoutAsync	6	0	0	2	3	1	6
106	F106_AuthService.ValidateOrCreateGoogleUserAsync	7	0	0	3	3	1	7
107	F107_AuthService.RotateRefreshTokenAsync	8	0	0	1	6	1	8
108	F108_UserController.GetProfile	6	0	0	2	3	1	6
109	F109_UserService.GetUserByIdAsync	6	0	0	2	3	1	6
110	F110_UserService.GetUserByEmailAsync	6	0	0	2	3	1	6
111	F111_StudentWalletController.GetConstraints	4	0	0	2	1	1	4
112	F112_StudentWalletController.CreateTopup	6	0	0	2	3	1	6
113	F113_WalletTopupService.ValidateTopupAmountAsync	6	0	0	1	4	1	6
114	F114_WalletTopupService.ProcessSuccessfulPaymentAsync	6	0	0	2	3	1	6
115	F115_WalletTopupService.CancelPendingOrderAsync	5	0	0	2	2	1	5
116	F116_WalletTopupService.GetByOrderCodeAsync	5	0	0	1	3	1	5
117	F117_WalletTopupService.GetPagedTransactionsAsync	6	0	0	4	1	1	6
118	F118_PaymentWebhookController.ValidateSignature	5	0	0	2	2	1	5
119	F119_CodeExecutionEngine.ValidateContract	7	0	0	3	2	2	7
120	F120_CodeMentorService.EvaluatePromptQualityAsync	6	0	0	2	3	1	6
121	F121_AIQuestionGenerationController.ReviewDraft	6	0	0	1	4	1	6
122	F122_AIQuestionGenerationController.ApproveDraft	6	0	0	2	3	1	6
123	F123_QuestionGenerationReviewService.ValidateBloomDistribution	6	0	0	2	3	1	6
124	F124_SubmissionController.GetCodingDetail	6	0	0	2	3	1	6
125	F125_SubmissionController.GetMcqDetail	6	0	0	2	3	1	6
```
