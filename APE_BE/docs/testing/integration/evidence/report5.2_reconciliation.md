# Report 5.2 Reconciliation

- Total planned TestCaseIDs: 76
- CORE planned: 72
- CORE implemented: 72
- CORE executed: 72
- CORE passed: 72
- CORE failed: 0
- CORE skipped: 0
- LIVE_SMOKE planned: 4
- LIVE_SMOKE executed: 0
- Pending: 4
- Planned endpoint traceability: 144 / 144
- Endpoints exercised by PASSED CORE tests: 144
- Endpoints covered only by deferred LIVE_SMOKE: 0
- Endpoints with no executed or pending evidence: 0

## Module Totals

| Module | Total | Passed | Failed | Pending | N/A |
| --- | ---: | ---: | ---: | ---: | ---: |
| Authentication | 5 | 4 | 0 | 1 | 0 |
| Admin User Management | 2 | 2 | 0 | 0 | 0 |
| Course Management | 4 | 4 | 0 | 0 | 0 |
| Document & Retrieval | 12 | 12 | 0 | 0 | 0 |
| Question Bank & Review | 9 | 9 | 0 | 0 | 0 |
| AI Question Generation | 6 | 5 | 0 | 1 | 0 |
| Exam Setup & Delivery | 7 | 7 | 0 | 0 | 0 |
| Practice & Submission | 14 | 13 | 0 | 1 | 0 |
| Wallet & Payment | 7 | 6 | 0 | 1 | 0 |
| Profile & Gamification | 4 | 4 | 0 | 0 | 0 |
| Admin AI Management | 6 | 6 | 0 | 0 | 0 |

## Proven Defects

- API_PRACTICE_004: ownership defect on `POST /api/student/practice/sessions/{sessionId}/end`; resolved in `API/Controllers/PracticeController.cs` and `Application/Services/PracticeService.cs`; final regression passed.
- API_ADMIN_AI_002: AI usage summary endpoints failed when aggregate output returned ObjectId values; resolved in `Infrastructure/Persistence/AIUsageLogRepository.cs`; final regression passed.
