# UNIT TEST REPORT - BATCH 06 (F51 - F60)
> **Module**: AI Code Mentor, Gamification & Analytics, Question Bank & AI Question Generation  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (56 / 56 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F51 - SubmissionController.GetMentorHint](#sheet-f51--submissioncontrollergetmentorhint)
2. [Sheet F52 - GamificationService.RecordPracticeActivityAsync](#sheet-f52--gamificationservicerecordpracticeactivityasync)
3. [Sheet F53 - GamificationService.GetStudentStatsAsync](#sheet-f53--gamificationservicegetstudentstatsasync)
4. [Sheet F54 - StudentAnalyticsGamificationController.GetStats](#sheet-f54--studentanalyticsgamificationcontrollergetstats)
5. [Sheet F55 - StudentAnalyticsGamificationController.GetLeaderboard](#sheet-f55--studentanalyticsgamificationcontrollergetleaderboard)
6. [Sheet F56 - QuestionService.ListQuestionsAsync](#sheet-f56--questionservicelistquestionsasync)
7. [Sheet F57 - QuestionController.ListQuestions](#sheet-f57--questioncontrollerlistquestions)
8. [Sheet F58 - QuestionController.CreateQuestion](#sheet-f58--questioncontrollercreatequestion)
9. [Sheet F59 - QuestionGenerationReviewService.GenerateQuestionsAsync](#sheet-f59--questiongenerationreviewservicegeneratequestionsasync)
10. [Sheet F60 - AIQuestionGenerationController.GenerateDraft](#sheet-f60--aiquestiongenerationcontrollergeneratedraft)

---

## Sheet `F51` – `SubmissionController.GetMentorHint`

### 1. Header Information & Summary
| Code Module | AI Code Mentor | Method | SubmissionController.GetMentorHint |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student AI Code Mentor HTTP POST endpoint, claim extraction, 401 for unauthenticated calls, 402 for payment/balance failure, and hint response delivery. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Student ("student-1") | O | O | O | | O | O |
| | | Unauthenticated / No Token | | | | O | | |
| | **Request Body** | Valid GetMentorHintDto (Level 1) | O | | | | | |
| | | Valid GetMentorHintDto (Level 3) | | | | | O | |
| | | Insufficient Balance Trigger | | O | | | | |
| | | Non-existent Session ID | | | O | | | |
| | | Boundary Max Prompt Length | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | | | | O | O |
| | | 402 PaymentRequired / 400 BadReq | | O | | | | |
| | | 404 NotFound | | | O | | | |
| | | 401 Unauthorized | | | | O | | |
| | **Body Payload** | ApiResponse.Ok (CodeMentorHintDto) | O | | | | O | O |
| | | ApiResponse.Fail | | O | O | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **N** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~SubmissionControllerMentorTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 76 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F52` – `GamificationService.RecordPracticeActivityAsync`

### 1. Header Information & Summary
| Code Module | Gamification & Analytics | Method | GamificationService.RecordPracticeActivityAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student daily practice streak tracking, consecutive day incrementation, streak reset on skipped days, same-day idempotency, and EXP point rewards. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:I22, "P")`<br>**(6)** | `=COUNTIF(D22:I22, "F")`<br>**(0)** | `=COUNTIF(D22:I22, "")`<br>**(0)** | `=COUNTIF(D21:I21, "N")`<br>**(3)** | `=COUNTIF(D21:I21, "A")`<br>**(2)** | `=COUNTIF(D21:I21, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Last Activity** | Yesterday (Consecutive day) | O | | | | | |
| | | Today (Same calendar day) | | O | | | | |
| | | 3 Days Ago (Streak broken) | | | O | | | |
| | | First Time Ever (No prior history) | | | | O | | |
| | | Invalid User ID | | | | | O | |
| | | Midnight Boundary (23:59:59 -> 00:00:01)| | | | | | O |
| **Confirm** | **Return** | GamificationStatsDto | O | O | O | O | | O |
| | | null | | | | | O | |
| | **Streak Count**| Incremented (+1) | O | Unchanged | Reset to 1 | Initialized (1)| None | Incremented (+1)|
| | **EXP Points** | Awarded (+50 EXP) | O | No (+0) | Awarded (+50) | Awarded (+50)| None | Awarded (+50)|
| | **Exception** | None | O | O | O | O | | O |
| | | NotFoundException | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~GamificationServiceTests&Name~RecordPracticeActivity"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 82 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F53` – `GamificationService.GetStudentStatsAsync`

### 1. Header Information & Summary
| Code Module | Gamification & Analytics | Method | GamificationService.GetStudentStatsAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student analytics overview query, total submissions count, accuracy rate, current streak, badge achievements, and non-existent student fallback. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **User State** | Active Student with Submissions & Badges | O | | | | |
| | | New Student with 0 Submissions (0% rate) | | O | | | |
| | | Top Rank Student (Leaderboard #1) | | | O | | |
| | | Non-existent Student ID | | | | O | |
| | | Valid ID with Leading/Trailing Spaces | | | | | O |
| **Confirm** | **Return** | StudentStatsDto | O | O | O | | O |
| | | null | | | | O | |
| | **Stats Accuracy**| Calculated Correctly (e.g. 85.5%) | O | 0.0% | 100.0% | None | Calculated |
| | **Exception** | None | O | O | O | | O |
| | | NotFoundException | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~GamificationServiceTests&Name~GetStudentStats"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 70 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F54` – `StudentAnalyticsGamificationController.GetStats`

### 1. Header Information & Summary
| Code Module | Gamification & Analytics | Method | StudentAnalyticsGamificationController.GetStats |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student analytics & stats HTTP GET endpoint, claim extraction, 401 for unauthenticated calls, and DTO payload delivery. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(2)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Student ("student-1") | O | O | | O |
| | | Unauthenticated / No Token | | | O | |
| | **Service State** | Normal Stats Return | O | | | O |
| | | Student Profile Not Found | | O | | |
| **Confirm** | **HTTP Status** | 200 OK | O | | | O |
| | | 404 NotFound | | O | | |
| | | 401 Unauthorized | | | O | |
| | **Body Payload** | ApiResponse.Ok (StudentStatsDto) | O | | | O |
| | | ApiResponse.Fail | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentAnalyticsGamificationControllerTests&Name~GetStats"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 62 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F55` – `StudentAnalyticsGamificationController.GetLeaderboard`

### 1. Header Information & Summary
| Code Module | Gamification & Analytics | Method | StudentAnalyticsGamificationController.GetLeaderboard |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student leaderboard ranking HTTP GET endpoint, pagination parameters, sorting by points/streak descending, and 401 handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated User | O | O | | O |
| | | Unauthenticated Caller | | | O | |
| | **Query page/limit** | Default (1 / 10) | O | | | |
| | | Custom Valid (2 / 5) | | O | | |
| | | Invalid Lower Bound (0 / 0) | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | O |
| | | 401 Unauthorized | | | O | |
| | **Body Payload** | ApiResponse.Ok (LeaderboardDto) | O | O | | O |
| | | ApiResponse.Fail | | | O | |
| | **Ordering** | Sorted Descending by Score/Points | O | O | None | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentAnalyticsGamificationControllerTests&Name~GetLeaderboard"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 60 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F56` – `QuestionService.ListQuestionsAsync`

### 1. Header Information & Summary
| Code Module | Question Bank Management | Method | QuestionService.ListQuestionsAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify teacher/admin question bank query, filtering by CourseId, Type (MCQ/Coding), Difficulty, Tags, and Pagination. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:I21, "P")`<br>**(6)** | `=COUNTIF(D21:I21, "F")`<br>**(0)** | `=COUNTIF(D21:I21, "")`<br>**(0)** | `=COUNTIF(D20:I20, "N")`<br>**(3)** | `=COUNTIF(D20:I20, "A")`<br>**(2)** | `=COUNTIF(D20:I20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Filter courseId**| null (All courses) | O | | | | | |
| | | "CSD201" (DSA course) | | O | | | | |
| | **Filter type** | "MCQ" | | | O | | | |
| | | "Coding" | | | | O | | |
| | **Filter tag** | "tree" | | | | | O | |
| | **page / limit** | Default (1 / 20) | O | O | O | O | O | |
| | | Custom Bounds (0 / 0) | | | | | | O |
| **Confirm** | **Return** | PagedResult<QuestionDto> | O | O | O | O | O | O |
| | **Items Count** | Matches filter condition | O | O | O | O | O | O |
| | **Exception** | None | O | O | O | O | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~QuestionControllerQuestionBankManagementTests&Name~List"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 79 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F57` – `QuestionController.ListQuestions`

### 1. Header Information & Summary
| Code Module | Question Bank Management | Method | QuestionController.ListQuestions |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify teacher/admin question bank listing HTTP GET endpoint, query param binding, role authorization, and response formatting. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Teacher / Admin | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **Query courseId**| null | O | | | | | |
| | | "CSD201" | | O | | | | |
| | **Query type** | "PE" | | | O | | | |
| | **Query search** | "Binary Search" | | | | O | | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | O | | O |
| | | 401 Unauthorized | | | | | O | |
| | **Body Payload** | ApiResponse.Ok (PagedQuestions) | O | O | O | O | | O |
| | | ApiResponse.Fail | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~QuestionControllerQuestionBankManagementTests&Name~GetList"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 75 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F58` – `QuestionController.CreateQuestion`

### 1. Header Information & Summary
| Code Module | Question Bank Management | Method | QuestionController.CreateQuestion |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify teacher question creation HTTP POST endpoint, payload validation (title, options, correct answer, test cases), and Teacher/Admin authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:I21, "P")`<br>**(6)** | `=COUNTIF(D21:I21, "F")`<br>**(0)** | `=COUNTIF(D21:I21, "")`<br>**(0)** | `=COUNTIF(D20:I20, "N")`<br>**(2)** | `=COUNTIF(D20:I20, "A")`<br>**(3)** | `=COUNTIF(D20:I20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Teacher | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **Question Type**| Valid MCQ Question DTO | O | | | | | |
| | | Valid PE Coding Question DTO | | O | | | | |
| | | Missing Correct Answer in MCQ | | | O | | | |
| | | Missing Test Cases in PE | | | | O | | |
| | | Boundary 100 Options Count | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | | O |
| | | 400 BadRequest | | | O | O | | |
| | | 401 Unauthorized | | | | | O | |
| | **Body Payload** | ApiResponse.Ok (QuestionDto) | O | O | | | | O |
| | | ApiResponse.Fail | | | O | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~QuestionControllerQuestionBankManagementTests&Name~Create"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 77 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F59` – `QuestionGenerationReviewService.GenerateQuestionsAsync`

### 1. Header Information & Summary
| Code Module | AI Question Generation | Method | QuestionGenerationReviewService.GenerateQuestionsAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify AI syllabus-to-question generator, Bloom taxonomy distribution, difficulty balance, draft creation, and teacher review workflow states. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D23:J23, "P")`<br>**(7)** | `=COUNTIF(D23:J23, "F")`<br>**(0)** | `=COUNTIF(D23:J23, "")`<br>**(0)** | `=COUNTIF(D22:J22, "N")`<br>**(3)** | `=COUNTIF(D22:J22, "A")`<br>**(3)** | `=COUNTIF(D22:J22, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Source Document**| Valid Extracted Document ID | O | O | O | | O | O | O |
| | | Non-existent Document ID | | | | O | | | |
| | **Target Type** | MCQ Questions (5 items) | O | | | | | | |
| | | PE Coding Questions (2 items) | | O | | | | | |
| | | Mixed Questions (MCQ + PE) | | | O | | | | |
| | **AI Provider** | Provider Healthy (JSON valid) | O | O | O | | | | O |
| | | Provider Returns Malformed JSON | | | | | O | | |
| | | Teacher Balance Insufficient | | | | | | O | |
| | **Count Bounds** | Maximum Allowed Count (20 items) | | | | | | | O |
| **Confirm** | **Return** | QuestionGenerationDraftDto | O | O | O | | | | O |
| | | null | | | | O | O | O | |
| | **Draft Status**| "needs_revision" / "pending_review"| O | O | O | None | None | None | O |
| | **Exception** | None | O | O | O | | | | O |
| | | NotFoundException | | | | O | | | |
| | | AIProviderException | | | | | O | | |
| | | InsufficientBalanceException | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~QuestionGenerationReviewServiceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 96 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F60` – `AIQuestionGenerationController.GenerateDraft`

### 1. Header Information & Summary
| Code Module | AI Question Generation | Method | AIQuestionGenerationController.GenerateDraft |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify teacher AI question generation initiation HTTP POST endpoint, document ID validation, generation prompt forwarding, and Teacher authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Teacher | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **Request Body** | Valid GenerateQuestionsDto (MCQ) | O | | | | | |
| | | Valid GenerateQuestionsDto (PE) | | O | | | | |
| | | Missing Document ID | | | O | | | |
| | | Generation Count <= 0 (Invalid) | | | | O | | |
| | | Boundary Max Count (20 items) | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | | O |
| | | 400 BadRequest | | | O | O | | |
| | | 401 Unauthorized | | | | | O | |
| | **Body Payload** | ApiResponse.Ok (DraftDto) | O | O | | | | O |
| | | ApiResponse.Fail | | | O | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIQuestionGenerationControllerTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 84 ms - BE_UnitTests.dll (net8.0)
```
