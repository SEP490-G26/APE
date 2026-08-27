# UNIT TEST REPORT - BATCH 09 (F81 - F90)
> **Module**: Sandbox Code Execution Client (Judge0), Submission Reliability & Aggregate Scoring  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (66 / 66 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F81 - Judge0ClientAdapter.SubmitAsync](#sheet-f81--judge0clientadaptersubmitasync)
2. [Sheet F82 - Judge0ClientAdapter.GetSubmissionResultAsync](#sheet-f82--judge0clientadaptergetsubmissionresultasync)
3. [Sheet F83 - Judge0ClientAdapter.MapLanguageId](#sheet-f83--judge0clientadaptermaplanguageid)
4. [Sheet F84 - Judge0Options.Validate](#sheet-f84--judge0optionsvalidate)
5. [Sheet F85 - SubmissionGradingReliability.ExecuteWithRetryAsync](#sheet-f85--submissiongradingreliabilityexecutewithretryasync)
6. [Sheet F86 - SubmissionGradingReliability.RecoverZombieSubmissionsAsync](#sheet-f86--submissiongradingreliabilityrecoverzombiesubmissionsasync)
7. [Sheet F87 - SubmissionGradingReliability.HandleTimeoutAsync](#sheet-f87--submissiongradingreliabilityhandletimeoutasync)
8. [Sheet F88 - PESubmissionAggregate.CalculateTotalScore](#sheet-f88--pesubmissionaggregatecalculatetotalscore)
9. [Sheet F89 - PESubmissionAggregate.MaskHiddenTestCaseResults](#sheet-f89--pesubmissionaggregatemaskhiddentestcaseresults)
10. [Sheet F90 - PESubmissionAggregate.EvaluatePassedStatus](#sheet-f90--pesubmissionaggregateevaluatepassedstatus)

---

## Sheet `F81` – `Judge0ClientAdapter.SubmitAsync`

### 1. Header Information & Summary
| Code Module | Sandbox Code Execution | Method | Judge0ClientAdapter.SubmitAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify HTTP POST code execution submission to Judge0 CE/Extra CE sandbox API, base64 encoding/decoding, token extraction, and network retry. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:K21, "P")`<br>**(8)** | `=COUNTIF(D21:K21, "F")`<br>**(0)** | `=COUNTIF(D21:K21, "")`<br>**(0)** | `=COUNTIF(D20:K20, "N")`<br>**(4)** | `=COUNTIF(D20:K20, "A")`<br>**(3)** | `=COUNTIF(D20:K20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Judge0 Status** | 201 Created (Token returned) | O | O | O | O | | | | O |
| | | 503 Service Unavailable | | | | | O | | | |
| | | 422 Unprocessable Entity | | | | | | O | | |
| | | Connection Timeout (No response) | | | | | | | O | |
| | **Language** | Java (Language ID: 62) | O | | | | O | | O | |
| | | C++ (Language ID: 54) | | O | | | | O | | |
| | | C# (Language ID: 51) | | | O | | | | | |
| | | Python (Language ID: 71) | | | | O | | | | |
| | **Source Code** | Boundary Empty Code ("") | | | | | | | | O |
| **Confirm** | **Return** | Judge0SubmissionTokenDto | O | O | O | O | | | | O |
| | | null | | | | | O | O | O | |
| | **Exception** | None | O | O | O | O | | | | O |
| | | Judge0ServerException | | | | | O | | O | |
| | | ValidationException | | | | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~Judge0ClientAdapterTests&Name~Submit"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 84 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F82` – `Judge0ClientAdapter.GetSubmissionResultAsync`

### 1. Header Information & Summary
| Code Module | Sandbox Code Execution | Method | Judge0ClientAdapter.GetSubmissionResultAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify polling of Judge0 submission status (1: In Queue, 2: Processing, 3: Accepted, 4: Wrong Answer, 5: TLE, 6: Compilation Error), and stdout/stderr decoding. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:K21, "P")`<br>**(8)** | `=COUNTIF(D21:K21, "F")`<br>**(0)** | `=COUNTIF(D21:K21, "")`<br>**(0)** | `=COUNTIF(D20:K20, "N")`<br>**(4)** | `=COUNTIF(D20:K20, "A")`<br>**(3)** | `=COUNTIF(D20:K20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Sandbox Status**| StatusId: 3 (Accepted) | O | | | | | | | |
| | | StatusId: 4 (Wrong Answer) | | O | | | | | | |
| | | StatusId: 5 (Time Limit Exceeded) | | | O | | | | | |
| | | StatusId: 6 (Compilation Error) | | | | O | | | | |
| | | StatusId: 1/2 (In Queue / Processing)| | | | | O | | | |
| | | Token Not Found (404) | | | | | | O | | |
| | | Network Error during poll | | | | | | | O | |
| | | Boundary Large Stderr (50KB) | | | | | | | | O |
| **Confirm** | **Return** | Judge0ExecutionResultDto | O | O | O | O | O (In Prog)| | | O |
| | | null | | | | | | O | O | |
| | **IsCompleted** | true | O | O | O | O | | | | O |
| | | false | | | | | O | | | |
| | **Exception** | None | O | O | O | O | O | | | O |
| | | NotFoundException | | | | | | O | | |
| | | Judge0ServerException | | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~Judge0ClientAdapterTests&Name~GetSubmissionResult"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 80 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F83` – `Judge0ClientAdapter.MapLanguageId`

### 1. Header Information & Summary
| Code Module | Sandbox Code Execution | Method | Judge0ClientAdapter.MapLanguageId |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify language name/alias mapping to standard Judge0 Language IDs (Java, C, C++, C#, Python), casing tolerance, and unsupported language rejection. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **languageString**| "Java" / "java" / "JAVA" | O | | | | | |
| | | "C++" / "cpp" / "CPP" | | O | | | | |
| | | "C#" / "csharp" / "cs" | | | O | | | |
| | | "Ruby" / "UnsupportedLang" | | | | O | | |
| | | null or Empty String | | | | | O | |
| | | Boundary Whitespace (" java ") | | | | | | O |
| **Confirm** | **Return (Int)** | 62 (Java ID) | O | | | | | O |
| | | 54 (C++ ID) | | O | | | | |
| | | 51 (C# ID) | | | O | | | |
| | **Exception** | None | O | O | O | | | O |
| | | NotSupportedException | | | | O | | |
| | | ArgumentException | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~Judge0ClientAdapterTests&Name~MapLanguage"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 71 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F84` – `Judge0Options.Validate`

### 1. Header Information & Summary
| Code Module | Sandbox Code Execution | Method | Judge0Options.Validate |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify Judge0 options validation (BaseUrl, ApiKey, CpuTimeLimitSec, MemoryLimitKb, MaxConcurrentGraders), and boundary enforcement. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:I21, "P")`<br>**(6)** | `=COUNTIF(D21:I21, "F")`<br>**(0)** | `=COUNTIF(D21:I21, "")`<br>**(0)** | `=COUNTIF(D20:I20, "N")`<br>**(2)** | `=COUNTIF(D20:I20, "A")`<br>**(3)** | `=COUNTIF(D20:I20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **BaseUrl** | "http://localhost:2358" (Valid) | O | O | | O | O | O |
| | | null or Invalid URL format | | | O | | | |
| | **CpuTimeLimitSec**| 5 seconds (Standard) | O | | | | | |
| | | 0 or Negative Value (-1) | | O | | | | |
| | **MemoryLimitKb** | 128,000 KB (Standard) | O | O | O | | | |
| | | 0 or Negative Value | | | | O | | |
| | **MaxConcurrent** | 0 (Must be >= 1) | | | | | O | |
| | | Boundary Max (100 concurrency) | | | | | | O |
| **Confirm** | **Validation** | Succeeded (IsValid = true) | O | | | | | O |
| | | Failed (IsValid = false) | | O | O | O | O | |
| | **Exception** | None | O | | | | | O |
| | | OptionsValidationException | | O | O | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~Judge0OptionsValidationTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 68 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F85` – `SubmissionGradingReliability.ExecuteWithRetryAsync`

### 1. Header Information & Summary
| Code Module | Submission Reliability | Method | SubmissionGradingReliability.ExecuteWithRetryAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify resilient retry policy on transient network failures, exponential backoff with jitter, non-retryable error handling, and max retry ceiling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:J21, "P")`<br>**(7)** | `=COUNTIF(D21:J21, "F")`<br>**(0)** | `=COUNTIF(D21:J21, "")`<br>**(0)** | `=COUNTIF(D20:J20, "N")`<br>**(3)** | `=COUNTIF(D20:J20, "A")`<br>**(3)** | `=COUNTIF(D20:J20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Execution State**| Success on 1st Attempt | O | | | | | | |
| | | Fails 1st, Succeeds on 2nd Attempt | | O | | | | | |
| | | Fails 1st & 2nd, Succeeds on 3rd | | | O | | | | |
| | | Exceeds Max Retries (3/3 Fails) | | | | O | | | |
| | | Non-retryable Error (Invalid Token) | | | | | O | | |
| | | CancellationToken Cancelled | | | | | | O | |
| | | Boundary Max Retry Setting (5 Retries)| | | | | | | O |
| **Confirm** | **Return** | Execution Result DTO | O | O | O | | | | O |
| | | null | | | | O | O | O | |
| | **Retry Count** | 0 Retries | O | | | | | | |
| | | 1 Retry | | O | | | | | |
| | | 2 Retries | | | O | | | | |
| | **Exception** | None | O | O | O | | | | O |
| | | MaxRetriesExceededException | | | | O | | | |
| | | NonRetryableException | | | | | O | | |
| | | OperationCanceledException | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~SubmissionGradingReliabilityTests&Name~ExecuteWithRetry"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 82 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F86` – `SubmissionGradingReliability.RecoverZombieSubmissionsAsync`

### 1. Header Information & Summary
| Code Module | Submission Reliability | Method | SubmissionGradingReliability.RecoverZombieSubmissionsAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify background worker detection and recovery of stuck/orphaned grading jobs whose heartbeat lease has expired (> 5 minutes). |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Stuck Submissions**| 3 Submissions with Expired Lease | O | | | | | |
| | | 0 Stuck Submissions (All healthy) | | O | | | | |
| | | Submissions Exceeding Max Recovery Tries| | | O | | | |
| | | DB Connection Error during query | | | | O | | |
| | | Concurrent Worker Race on Recovery | | | | | O | |
| | | Exact Lease Expiry Boundary (300s) | | | | | | O |
| **Confirm** | **Re-queued** | 3 Submissions Reset to Queued Status | O | None | | None | 1 Winner | O |
| | **Marked Failed**| Max Recoveries Exceeded Marked FAILED | | | O | | | |
| | **Exception** | None | O | O | O | | O | O |
| | | DatabaseException | | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~SubmissionGradingReliabilityTests&Name~RecoverZombie"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 74 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F87` – `SubmissionGradingReliability.HandleTimeoutAsync`

### 1. Header Information & Summary
| Code Module | Submission Reliability | Method | SubmissionGradingReliability.HandleTimeoutAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify graceful timeout termination for hung grading processes, updating submission status to TIMED_OUT, and releasing execution locks. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Submission State**| InProgress Submission Exceeds Timeout | O | | | | | O |
| | | Already Completed Submission (Late trigger)| | O | | | | |
| | | Non-existent Submission ID | | | O | | | |
| | | DB Failure During Timeout Update | | | | O | | |
| | | Cancelled by User Before Timeout | | | | | O | |
| **Confirm** | **DB Status** | Updated to Status: TIMED_OUT | O | Unchanged | None | Unchanged | Unchanged | O |
| | **Lease** | Lease Lock Cleared (null) | O | None | None | None | None | O |
| | **Exception** | None | O | O | | | O | O |
| | | NotFoundException | | | O | | | |
| | | DatabaseException | | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~SubmissionGradingReliabilityTests&Name~HandleTimeout"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 71 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F88` – `PESubmissionAggregate.CalculateTotalScore`

### 1. Header Information & Summary
| Code Module | Aggregate Scoring | Method | PESubmissionAggregate.CalculateTotalScore |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify calculation of total weighted submission score across all test cases (visible and hidden), point rounding, and zero-point handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:J20, "P")`<br>**(7)** | `=COUNTIF(D20:J20, "F")`<br>**(0)** | `=COUNTIF(D20:J20, "")`<br>**(0)** | `=COUNTIF(D19:J19, "N")`<br>**(3)** | `=COUNTIF(D19:J19, "A")`<br>**(2)** | `=COUNTIF(D19:J19, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Test Cases** | 5/5 Passed (100% Score) | O | | | | | | |
| | | 3/5 Passed (60% Score) | | O | | | | | |
| | | 0/5 Passed (0% Score) | | | O | | | | |
| | | Weighted Points (e.g. 2pts, 3pts, 5pts)| | | | O | | | |
| | | Empty Test Case List (0 items) | | | | | O | | |
| | | Max Score Boundary (100.00 / 100.00) | | | | | | O | |
| | | Floating Point Precision (33.3333%) | | | | | | | O |
| **Confirm** | **Calculated Score**| Exact Total Score (e.g. 10.0 / 10.0)| O | 6.0 | 0.0 | Calculated| 0.0 | 100.0 | 33.33 |
| | **Exception** | None | O | O | O | O | O | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **N** | **A** | **B** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PESubmissionAggregateTests&Name~CalculateTotalScore"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 76 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F89` – `PESubmissionAggregate.MaskHiddenTestCaseResults`

### 1. Header Information & Summary
| Code Module | Aggregate Scoring | Method | PESubmissionAggregate.MaskHiddenTestCaseResults |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student response sanitization, masking expected/actual outputs for hidden test cases while preserving pass/fail boolean status. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller Role** | Student (Masking required) | O | O | | O | O | O |
| | | Teacher / Admin (Full unmasked view)| | | O | | | |
| | **Test Case Type**| Mix of Visible & Hidden Test Cases | O | | O | | | |
| | | All Visible Test Cases | | O | | | | |
| | | All Hidden Test Cases | | | | O | | |
| | | Empty Test Case List | | | | | O | |
| | | Boundary Hidden Flag (IsHidden = true)| | | | | | O |
| **Confirm** | **Visible Cases** | Expected/Actual Output Shown | O | O | O | None | None | None |
| | **Hidden Cases** | Expected/Actual Output = null/"[Hidden]"| O | None | Shown | Masked | None | Masked |
| | **Status Flag** | IsPassed Preserved Accurately | O | O | O | O | None | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PESubmissionAggregateTests&Name~MaskHidden"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 73 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F90` – `PESubmissionAggregate.EvaluatePassedStatus`

### 1. Header Information & Summary
| Code Module | Aggregate Scoring | Method | PESubmissionAggregate.EvaluatePassedStatus |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify final submission status determination (Accepted, WrongAnswer, CompilationError, RuntimeError, TimeLimitExceeded). |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Execution Outcome**| All Test Cases Passed | O | | | | | |
| | | >= 1 Test Case Failed Output | | O | | | | |
| | | Compilation Error Occurred | | | O | | | |
| | | Runtime Exception Occurred | | | | O | | |
| | | Time Limit Exceeded | | | | | O | |
| | | Boundary Partial Pass (Score > 0) | | | | | | O |
| **Confirm** | **Evaluation Status**| SubmissionStatus.Accepted | O | | | | | |
| | | SubmissionStatus.WrongAnswer | | O | | | | |
| | | SubmissionStatus.CompilationError | | | O | | | |
| | | SubmissionStatus.RuntimeError | | | | O | | |
| | | SubmissionStatus.TimeLimitExceeded | | | | | O | |
| | | SubmissionStatus.Partial | | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PESubmissionAggregateTests&Name~EvaluatePassedStatus"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 71 ms - BE_UnitTests.dll (net8.0)
```
