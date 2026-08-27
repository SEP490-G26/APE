# UNIT TEST REPORT - BATCH 05 (F41 - F50)
> **Module**: Exam Setup, Coding Practice, Code Execution & Submissions  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (63 / 63 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F41 - ExamService.CreatePracticeExamAsync](#sheet-f41--examservicecreatepracticeexamasync)
2. [Sheet F42 - ExamService.GetExamDetailAsync](#sheet-f42--examservicegetexamdetailasync)
3. [Sheet F43 - ExamController.CreatePracticeExam](#sheet-f43--examcontrollercreatepracticeexam)
4. [Sheet F44 - ExamController.GetDetail](#sheet-f44--examcontrollergetdetail)
5. [Sheet F45 - PESubmissionService.SubmitCodingAsync](#sheet-f45--pesubmissionservicesubmitcodingasync)
6. [Sheet F46 - PECodeRunService.ExecuteDryRunAsync](#sheet-f46--pecoderunserviceexecutedyrunasync)
7. [Sheet F47 - SubmissionController.SubmitPE](#sheet-f47--submissioncontrollersubmitpe)
8. [Sheet F48 - SubmissionController.RunCode](#sheet-f48--submissioncontrollerruncode)
9. [Sheet F49 - ExecutionFeedbackDiagnosticParser.Parse](#sheet-f49--executionfeedbackdiagnosticparserparse)
10. [Sheet F50 - CodeMentorService.GetHintAsync](#sheet-f50--codementorservicegethintasync)

---

## Sheet `F41` – `ExamService.CreatePracticeExamAsync`

### 1. Header Information & Summary
| Code Module | Exam Setup | Method | ExamService.CreatePracticeExamAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify practice exam creation, validating question list presence, exam mode assignment (Practice), and course association. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:I21, "P")`<br>**(6)** | `=COUNTIF(D21:I21, "F")`<br>**(0)** | `=COUNTIF(D21:I21, "")`<br>**(0)** | `=COUNTIF(D20:I20, "N")`<br>**(3)** | `=COUNTIF(D20:I20, "A")`<br>**(2)** | `=COUNTIF(D20:I20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | Course Exists in DB ("CSD201") | O | O | O | | O | O |
| | | Course Not Found in DB | | | | O | | |
| | **Exam Questions** | Valid FE Question IDs (10 items) | O | | | | | |
| | | Valid PE Question IDs (3 items) | | O | | | | |
| | | Empty Question Lists (0 items) | | | O | | | |
| | | Boundary Question Count (1 item) | | | | | O | |
| | | Whitespace in Title (" Exam 1 ") | | | | | | O |
| **Confirm** | **Return** | ExamDto (Id != null) | O | O | | | O | O |
| | | null | | | O | O | | |
| | **DB Exam** | Mode = ExamMode.Practice | O | O | None | None | O | O |
| | **Exception** | None | O | O | | | O | O |
| | | ValidationException | | | O | | | |
| | | NotFoundException | | | | O | | |
| | **Log message** | None | O | O | | | O | O |
| | | "Exam must contain at least 1 question."| | | O | | | |
| | | "Course not found." | | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **B** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~ExamServicePracticeSetupTests&Name~CreatePracticeExam"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 74 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F42` – `ExamService.GetExamDetailAsync`

### 1. Header Information & Summary
| Code Module | Exam Setup | Method | ExamService.GetExamDetailAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify exam detail retrieval, question hydration, point total calculations, and 404 handling for non-existent exams. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **examId** | Valid Existing MCQ Exam ID | O | | | | | |
| | | Valid Existing Coding Exam ID | | O | | | | |
| | | Valid Mixed Exam ID (FE + PE) | | | O | | | |
| | | Non-existent Exam ID | | | | O | | |
| | | Null or Empty String | | | | | O | |
| | | Valid ID with Leading/Trailing Spaces| | | | | | O |
| **Confirm** | **Return** | ExamDetailDto | O | O | O | | | O |
| | | null | | | | O | O | |
| | **Hydration** | Full Question Metadata Populated | O | O | O | None | None | O (Trimmed)|
| | **Exception** | None | O | O | O | | | O |
| | | NotFoundException | | | | O | | |
| | | BadRequestException | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~ExamServicePracticeSetupTests&Name~GetExamDetail"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 71 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F43` – `ExamController.CreatePracticeExam`

### 1. Header Information & Summary
| Code Module | Exam Setup | Method | ExamController.CreatePracticeExam |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify teacher/student exam generation HTTP POST endpoint, payload validation, and role authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Teacher / Student | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **Request Body** | Valid CreatePracticeExamDto (MCQ) | O | | | | O | |
| | | Valid CreatePracticeExamDto (PE) | | O | | | | |
| | | Invalid / Empty DTO | | | O | | | |
| | | Non-existent Course ID in DTO | | | | O | | |
| | | Boundary Max Questions Count | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | | O |
| | | 400 BadRequest | | | O | O | | |
| | | 401 Unauthorized | | | | | O | |
| | **Body Payload** | ApiResponse.Ok (ExamDto) | O | O | | | | O |
| | | ApiResponse.Fail | | | O | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~ExamControllerPracticeSetupTests&Name~CreatePracticeExam"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 77 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F44` – `ExamController.GetDetail`

### 1. Header Information & Summary
| Code Module | Exam Setup | Method | ExamController.GetDetail |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify HTTP GET exam detail endpoint, response mapping, 404 for non-existent exams, and 401 for unauthorized calls. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:H19, "P")`<br>**(5)** | `=COUNTIF(D19:H19, "F")`<br>**(0)** | `=COUNTIF(D19:H19, "")`<br>**(0)** | `=COUNTIF(D18:H18, "N")`<br>**(2)** | `=COUNTIF(D18:H18, "A")`<br>**(2)** | `=COUNTIF(D18:H18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated User | O | O | O | | O |
| | | Unauthenticated Caller | | | | O | |
| | **examId** | Valid Existing Exam ID | O | O | | | |
| | | Non-existent Exam ID | | | O | | |
| | | Boundary Whitespace ID (" exam-1 ") | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | O |
| | | 404 NotFound | | | O | | |
| | | 401 Unauthorized | | | | O | |
| | **Body Payload** | ApiResponse.Ok (ExamDetailDto) | O | O | | | O |
| | | ApiResponse.Fail | | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~ExamControllerPracticeSetupTests&Name~GetDetail"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 68 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F45` – `PESubmissionService.SubmitCodingAsync`

### 1. Header Information & Summary
| Code Module | Coding Practice & Submissions | Method | PESubmissionService.SubmitCodingAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student PE coding solution submission, sandbox compilation & test execution, scoring calculation, hidden test evaluation, and attempt incrementation. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D24:L24, "P")`<br>**(9)** | `=COUNTIF(D24:L24, "F")`<br>**(0)** | `=COUNTIF(D24:L24, "")`<br>**(0)** | `=COUNTIF(D23:L23, "N")`<br>**(4)** | `=COUNTIF(D23:L23, "A")`<br>**(3)** | `=COUNTIF(D23:L23, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(9)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 | UTCID09 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Session State** | Owned Active InProgress Session | O | O | O | O | | O | O | O | O |
| | | Foreign / Unauthorized Session | | | | | O | | | | |
| | **Code Quality** | All Visible + Hidden Tests Pass | O | | | | | | | | |
| | | Visible Tests Pass, Hidden Fail | | O | | | | | | | |
| | | Compiler Error / Syntax Bug | | | O | | | | | | |
| | | Runtime Error (Zero Division) | | | | O | | | | | |
| | | Time Limit Exceeded (TLE) | | | | | | O | | | |
| | | Empty Code File (0 bytes) | | | | | | | O | | |
| | | Exact Max Score Boundary | | | | | | | | O | |
| | | Multi-File Solution Package | | | | | | | | | O |
| **Confirm** | **Return** | PESubmissionResultDto | O | O | O | O | | O | O | O | O |
| | | null | | | | | O | | | | |
| | **Status** | Passed (Full score) | O | | | | | | | O | O |
| | | Partial (Reduced score) | | O | | | | | | | |
| | | Failed (0 score) | | | O | O | | O | O | | |
| | **Attempt Count**| Incremented (+1) | O | O | O | O | None | O | O | O | O |
| | **Exception** | None | O | O | O | O | | O | O | O | O |
| | | ForbiddenException | | | | | O | | | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **N** | **A** | **A** | **B** | **B** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PESubmissionServiceCodingPracticeTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 98 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F46` – `PECodeRunService.ExecuteDryRunAsync`

### 1. Header Information & Summary
| Code Module | Code Execution | Method | PECodeRunService.ExecuteDryRunAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student dry run execution with custom test cases or sample test cases without persisting permanent submission records or deducting quota. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:J22, "P")`<br>**(7)** | `=COUNTIF(D22:J22, "F")`<br>**(0)** | `=COUNTIF(D22:J22, "")`<br>**(0)** | `=COUNTIF(D21:J21, "N")`<br>**(3)** | `=COUNTIF(D21:J21, "A")`<br>**(3)** | `=COUNTIF(D21:J21, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Test Input** | Custom Stdin Input ("5 10") | O | | | | | | |
| | | Sample Test Cases from Question | | O | | | | | |
| | | Compilation Error in Dry Run | | | O | | | | |
| | | Infinite Loop / Timeout | | | | O | | | |
| | | Missing Code Payload | | | | | O | | |
| | | Unauthenticated Caller | | | | | | O | |
| | | Boundary Large Output (100KB) | | | | | | | O |
| **Confirm** | **Return** | CodeRunResultDto | O | O | O | O | | | O |
| | | null | | | | | O | O | |
| | **Output** | Expected Stdout Stream | O | O | Error | Timeout | None | None | Truncated|
| | **DB Side-effects**| 0 Submissions Created (Isolated)| O | O | O | O | None | None | O |
| | **Exception** | None | O | O | O | O | | | O |
| | | ValidationException | | | | | O | | |
| | | UnauthorizedException | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PECodeRunServiceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 84 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F47` – `SubmissionController.SubmitPE`

### 1. Header Information & Summary
| Code Module | Coding Practice & Submissions | Method | SubmissionController.SubmitPE |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student PE submission HTTP POST endpoint, claim extraction, 401 for unauthenticated calls, payload validation, and service delegating. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Student ("student-1") | O | O | O | | O | O |
| | | Unauthenticated / Missing Claim | | | | O | | |
| | **Request Body** | Valid PESubmissionInputDto | O | | | | | O (Boundary) |
| | | Non-existent Session ID | | O | | | | |
| | | Empty Code Files Array | | | O | | | |
| **Confirm** | **HTTP Status** | 200 OK | O | | | | | O |
| | | 404 NotFound | | O | | | | |
| | | 400 BadRequest | | | O | | | |
| | | 401 Unauthorized | | | | O | | |
| | **Body Payload** | ApiResponse.Ok (PESubmissionResultDto)| O | | | | | O |
| | | ApiResponse.Fail | | O | O | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **N** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~SubmissionControllerCodingPracticeTests&Name~SubmitPE"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 73 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F48` – `SubmissionController.RunCode`

### 1. Header Information & Summary
| Code Module | Code Execution | Method | SubmissionController.RunCode |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student dry run execution HTTP POST endpoint, validation of code snippets, execution result return, and error mapping. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(2)** | `=COUNTIF(D19:H19, "A")`<br>**(2)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Student | O | O | O | | O |
| | | Unauthenticated Caller | | | | O | |
| | **Request Body** | Valid RunCodeDto with Custom Stdin | O | | | | |
| | | Valid RunCodeDto with Question TestCases| | O | | | |
| | | Empty Code String | | | O | | |
| | | Boundary Large Code Payload | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | O |
| | | 400 BadRequest | | | O | | |
| | | 401 Unauthorized | | | | O | |
| | **Body Payload** | ApiResponse.Ok (CodeRunResultDto) | O | O | | | O |
| | | ApiResponse.Fail | | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~SubmissionControllerCodingPracticeTests&Name~RunCode"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 69 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F49` – `ExecutionFeedbackDiagnosticParser.Parse`

### 1. Header Information & Summary
| Code Module | Code Execution & Diagnostics | Method | ExecutionFeedbackDiagnosticParser.Parse |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify diagnostic parsing of Java/C++ compiler and runtime errors, extracting error line numbers, error types (Syntax, NPE, OutOfBounds), and cleaning raw compiler logs. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:I21, "P")`<br>**(6)** | `=COUNTIF(D21:I21, "F")`<br>**(0)** | `=COUNTIF(D21:I21, "")`<br>**(0)** | `=COUNTIF(D20:I20, "N")`<br>**(3)** | `=COUNTIF(D20:I20, "A")`<br>**(2)** | `=COUNTIF(D20:I20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Raw Log Format**| Java javac Compilation Error | O | | | | | |
| | | Java NullPointerException StackTrace| | O | | | | |
| | | C++ g++ Compilation Error | | | O | | | |
| | | Null or Empty Error String | | | | O | | |
| | | Malformed Unrecognized Error Log | | | | | O | |
| | | Extremely Long StackTrace (50KB) | | | | | | O |
| **Confirm** | **Return** | DiagnosticReport (Line, Message, Type)| O | O | O | O | O | O |
| | **Extracted Line**| Positive Line Number (e.g. 15) | O | O | O | null | null | O |
| | **Error Category**| "CompilationError" | O | | O | "Unknown"| "Unknown"| "RuntimeError"|
| | | "RuntimeError" | | O | | | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~ExecutionFeedbackDiagnosticParserTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 65 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F50` – `CodeMentorService.GetHintAsync`

### 1. Header Information & Summary
| Code Module | AI Code Mentor | Method | CodeMentorService.GetHintAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify AI Code Mentor hint generation, Socratic hint level progression (Level 1: General, Level 2: Specific, Level 3: Code direction), balance deduction check, and rate limiting. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D23:J23, "P")`<br>**(7)** | `=COUNTIF(D23:J23, "F")`<br>**(0)** | `=COUNTIF(D23:J23, "")`<br>**(0)** | `=COUNTIF(D22:J22, "N")`<br>**(3)** | `=COUNTIF(D22:J22, "A")`<br>**(3)** | `=COUNTIF(D22:J22, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **User Balance** | Sufficient AI Balance (> 500 VND) | O | O | O | | O | O | O |
| | | Insufficient AI Balance (< 500 VND) | | | | O | | | |
| | **Hint Level** | Level 1 (Conceptual Hint) | O | | | | | | |
| | | Level 2 (Algorithm Direction) | | O | | | | | |
| | | Level 3 (Specific Fix Hint) | | | O | | | | |
| | | Invalid Level (Level 5) | | | | | O | | |
| | | Rate Limit Exceeded (5 req/min) | | | | | | O | |
| | | Boundary Balance (Exact 500 VND) | | | | | | | O |
| **Confirm** | **Return** | CodeMentorHintDto | O | O | O | | | | O |
| | | null | | | | O | O | O | |
| | **Wallet** | Fee Deducted from AI Wallet | O | O | O | No | No | No | O |
| | **Exception** | None | O | O | O | | | | O |
| | | InsufficientBalanceException | | | | O | | | |
| | | ValidationException | | | | | O | | |
| | | RateLimitExceededException | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CodeMentorServiceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 91 ms - BE_UnitTests.dll (net8.0)
```
