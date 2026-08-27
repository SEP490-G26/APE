# UNIT TEST REPORT - BATCH 04 (F31 - F40)
> **Module**: Course Administration, Student Course Catalog, Student Document, MCQ & Practice Session  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (55 / 55 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F31 - CourseController.Create](#sheet-f31--coursecontrollercreate)
2. [Sheet F32 - CourseController.Update](#sheet-f32--coursecontrollerupdate)
3. [Sheet F33 - CourseController.Delete](#sheet-f33--coursecontrollerdelete)
4. [Sheet F34 - StudentCourseController.List](#sheet-f34--studentcoursecontrollerlist)
5. [Sheet F35 - StudentDocumentController.List](#sheet-f35--studentdocumentcontrollerlist)
6. [Sheet F36 - StudentDocumentController.GetExtractionDraft](#sheet-f36--studentdocumentcontrollergetextractiondraft)
7. [Sheet F37 - PracticeService.StartSessionAsync & GetSessionAsync](#sheet-f37--practiceservicestartsessionasync--getsessionasync)
8. [Sheet F38 - PracticeController.Start & GetSession](#sheet-f38--practicecontrollerstart--getsession)
9. [Sheet F39 - FESubmissionService.CreateAndEvaluateAsync](#sheet-f39--fesubmissionservicecreateandevaluateasync)
10. [Sheet F40 - SubmissionController.SubmitFE & GetSessionSubmissions](#sheet-f40--submissioncontrollersubmitfe--getsessionsubmissions)

---

## Sheet `F31` – `CourseController.Create`

### 1. Header Information & Summary
| Code Module | Course Administration | Method | CourseController.Create |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative course creation endpoint, DTO forwarding, actor ID extraction, conflict error mapping, and Admin role authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller Role** | Admin with NameIdentifier ("507f...") | O | O | | O |
| | | Admin without NameIdentifier (Null actor) | | | O | |
| | **CreateCourseDto** | Valid DTO (Code: "swp391", Name: "Project") | O | | O | O |
| | | Duplicate Course Code ("SWP391") | | O | | |
| **Confirm** | **HTTP Status** | 200 OK | O | | O | |
| | | 400 BadRequest | | O | | |
| | **Body Payload** | ApiResponse.Ok (CourseDto) | O | | O | |
| | | ApiResponse.Fail | | O | | |
|  | **Service Invocation** | CreateCourseAsync Called with Actor ID | O | O | O |   |
| | **Routing & Auth** | Route = "api/admin/courses", Role = Admin | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **B** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseControllerMutationTests&Name~Create"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 64 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F32` – `CourseController.Update`

### 1. Header Information & Summary
| Code Module | Course Administration | Method | CourseController.Update |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative course update endpoint, ID & DTO forwarding, 404 for non-existent courses, 400 for business rule failures, and Admin role enforcement. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(2)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller Role** | Admin (Authorized) | O | O | O | O |
| | **courseId** | Valid Existing Course ID ("c-1") | O | O | | |
| | | Non-existent Course ID ("c-missing") | | | O | |
| | **UpdateCourseDto** | Valid DTO (Name: "Programming Foundations") | O | | O | O |
| | | Conflict / Invalid Operation DTO | | O | | |
| **Confirm** | **HTTP Status** | 200 OK | O | | | |
| | | 400 BadRequest | | O | | |
| | | 404 NotFound | | | O | |
| | **Body Payload** | ApiResponse.Ok (Updated CourseDto) | O | | | |
| | | ApiResponse.Fail | | O | O | |
| | **Routing & Auth** | Route = "{id}", Role = Admin | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseControllerMutationTests&Name~Update"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 62 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F33` – `CourseController.Delete`

### 1. Header Information & Summary
| Code Module | Course Administration | Method | CourseController.Delete |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative course deletion endpoint, 204 NoContent upon success, 404 for missing courses, null actor handling, and Admin role enforcement. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller Role** | Admin with NameIdentifier ("507f...") | O | O | | O |
| | | Admin without NameIdentifier (Null actor) | | | O | |
| | **courseId** | Valid Existing Course ID ("course-1") | O | | O | |
| | | Non-existent Course ID ("missing-course") | | O | | |
| **Confirm** | **HTTP Status** | 204 NoContent | O | | O | |
| | | 404 NotFound | | O | | |
| | **Body Payload** | Empty Body | O | | O | |
| | | ApiResponse.Fail ("Course not found...") | | O | | |
| | **Routing & Auth** | Route = "{id}", Produces 204, Role = Admin | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **B** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseControllerDeleteTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 59 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F34` – `StudentCourseController.List`

### 1. Header Information & Summary
| Code Module | Student Course Catalog | Method | StudentCourseController.List |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student-facing course catalog listing endpoint, filtering only Active status courses, pagination normalization (0,0 -> 1,20), and safe DTO field mapping. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **DB Courses State** | Mix of Active and Inactive Courses | O | | | |
| | | Active Course with Audit Fields | | | O | |
| | | Repository Failure / Exception | | | | O |
| | **page / limit** | Valid Page (1 / 20) | O | | O | O |
| | | Invalid Boundary (0 / 0) | | O | | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | |
| | | 400 BadRequest | | | | O |
| | **Status Filter** | Automatically Enforced Status = "Active" | O | O | O | |
|  | **Items Count** | Only Active Courses (Inactive excluded) | O | O | O |   |
| | **Paging Meta** | Normalized to Page=1, Limit=20 | | O | | |
| | **DTO Privacy** | CreatedBy & LastModifiedBy Stripped (null)| | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **B** | **N** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentCourseControllerListTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 61 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F35` – `StudentDocumentController.List`

### 1. Header Information & Summary
| Code Module | Student Document | Method | StudentDocumentController.List |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student uploaded documents listing endpoint, claim extraction, 401 for unauthenticated calls, query pagination forwarding, and error handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(2)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Student ("507f...") | O | | O | O |
| | | Unauthenticated / Missing Claims | | O | | |
| | **Service State** | Normal Repository Response | O | | | O |
| | | Repository Throws Exception | | | O | |
| | **page / limit** | Default (1 / 20) | O | O | O | |
| | | Custom Query Values (3 / 7) | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | | | O |
| | | 401 Unauthorized | | O | | |
| | | 400 BadRequest | | | O | |
| | **Body Payload** | PaginatedResult<DocumentListItemDto> | O | | | O |
| | | ApiResponse.Fail | | O | O | |
| | **Forwarded Args**| Page=3, Limit=7 Passed to Repo | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentDocumentControllerListTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 58 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F36` – `StudentDocumentController.GetExtractionDraft`

### 1. Header Information & Summary
| Code Module | Student Document | Method | StudentDocumentController.GetExtractionDraft |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student extraction draft detail endpoint, claim extraction, ownership security check (401 for non-owners), and 404 for missing drafts. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(1)** | `=COUNTIF(D18:G18, "A")`<br>**(3)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Document Owner ("owner-1") | | | O | O |
| | | Authenticated Non-Owner ("other-user") | | O | | |
| | | Unauthenticated / No Token | O | | | |
| | **documentId** | Valid Document with Active Draft | O | O | | O |
| | | Valid Document without Draft (Draft = null) | | | O | |
| **Confirm** | **HTTP Status** | 200 OK | | | | O |
| | | 401 Unauthorized | O | O | | |
| | | 404 NotFound | | | O | |
| | **Body Payload** | ExtractionDraftDto (DraftId matches) | | | | O |
| | | ApiResponse.Fail ("Unauthorized") | O | | | |
| | | ApiResponse.Fail ("No permission...") | | O | | |
| | | ApiResponse.Fail ("Draft not found") | | | O | |
| **Result** | **Type (N/A/B)** | | **A** | **A** | **A** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentDocumentControllerGetExtractionDraftTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 60 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F37` – `PracticeService.StartSessionAsync & GetSessionAsync`

### 1. Header Information & Summary
| Code Module | MCQ & Practice Session | Method | PracticeService.StartSessionAsync & GetSessionAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student practice session lifecycle, resuming active in-progress sessions, creating new MCQ/PE coding sessions, ownership authorization, and draft code restoration. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D23:K23, "P")`<br>**(8)** | `=COUNTIF(D23:K23, "F")`<br>**(0)** | `=COUNTIF(D23:K23, "")`<br>**(0)** | `=COUNTIF(D22:K22, "N")`<br>**(4)** | `=COUNTIF(D22:K22, "A")`<br>**(3)** | `=COUNTIF(D22:K22, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Session State** | Existing InProgress Session | O | | | | O | | | O |
| | | No Active Session (Create New) | | O | O | O | | | | |
| | | Non-existent Session ID | | | | | | | O | |
| | **User Account** | Existing Student ("student-1") | O | | O | O | O | | O | O |
| | | Non-existent User | | O | | | | | | |
| | | Foreign Student ("student-2") | | | | | | O | | |
| | **Exam Type** | MCQ Practice Exam (FE Questions) | O | O | O | O | O | O | O | |
| | | Coding Practice Exam (PE Questions)| | | | | | | | O |
| **Confirm** | **Service Result**| Success = true | O | | | O | O | | | O |
| | | Success = false | | O | O | | | O | O | |
| | **Session Payload**| Resumed SessionId & StartTime | O | | | | | | | |
| | | Newly Created InProgress Session | | | | O | | | | O |
|  |  | Mapped FE & PE Question Counts |   |   |   |   | O |   |   | O |
| | **Error Message** | None | O | | | O | O | | | O |
| | | "User not found" | | O | | | | | | |
| | | "Exam not found" | | | O | | | | | |
| | | "You do not have permission..." | | | | | | O | | |
| | | "Session not found" | | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PracticeServiceMcqPracticeTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 86 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F38` – `PracticeController.Start & GetSession`

### 1. Header Information & Summary
| Code Module | MCQ & Practice Session | Method | PracticeController.Start & GetSession |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student practice session HTTP endpoints, start session forwarding, session detail retrieval, 404 for foreign sessions, and routing attributes. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:K21, "P")`<br>**(8)** | `=COUNTIF(D21:K21, "F")`<br>**(0)** | `=COUNTIF(D21:K21, "")`<br>**(0)** | `=COUNTIF(D20:K20, "N")`<br>**(4)** | `=COUNTIF(D20:K20, "A")`<br>**(2)** | `=COUNTIF(D20:K20, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Endpoint** | POST api/student/practice/start | O | O | O | | | | O | |
| | | GET api/student/practice/sessions/{id}| | | | O | O | O | | O |
| | **Caller** | Authenticated Session Owner | O | O | O | O | | O | O | O |
| | | Foreign Student Caller | | | | | O | | | |
| | **Exam / Session**| Valid MCQ Exam / Active Session | O | | | O | | | | |
| | | Missing Exam (Service Failure) | | O | | | | | | |
| | | Valid PE Coding Exam / Session | | | | | | | O | O |
| **Confirm** | **HTTP Status** | 200 OK | O | | | O | | | O | O |
| | | 400 BadRequest | | O | | | | | | |
| | | 404 NotFound | | | | | O | | | |
| | **Body Payload** | StartSessionResultDto (SessionId) | O | | | | | | O | |
| | | PracticeSessionDto (Questions/Drafts)| | | | O | | | | O |
| | | ApiResponse.Fail | | O | | | O | | | |
| | **Routing** | Route & Authorize Attributes Valid| | | O | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **N** | **N** | **A** | **N** | **B** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PracticeControllerMcqPracticeTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 82 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F39` – `FESubmissionService.CreateAndEvaluateAsync`

### 1. Header Information & Summary
| Code Module | MCQ & Practice Session | Method | FESubmissionService.CreateAndEvaluateAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify multiple-choice answer evaluation engine, case-insensitive and whitespace trimming, multi-select answer comparison, ownership check, and submitted session rejection. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D23:K23, "P")`<br>**(8)** | `=COUNTIF(D23:K23, "F")`<br>**(0)** | `=COUNTIF(D23:K23, "")`<br>**(0)** | `=COUNTIF(D22:K22, "N")`<br>**(3)** | `=COUNTIF(D22:K22, "A")`<br>**(3)** | `=COUNTIF(D22:K22, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Session Ownership**| Owned Active Session ("student-1") | O | | O | O | O | O | O | O |
| | | Foreign Session ("student-2") | | O | | | | | | |
| | **Session Status**| InProgress | O | O | O | | O | O | O | O |
| | | Submitted (Completed Session) | | | | O | | | | |
| | **Question Exam** | Question Belongs to Active Exam | O | O | | O | O | O | O | O |
| | | Question Outside Exam | | | O | | | | | |
| | **User Answer** | Exact Match with Trim/Case (" c ", "a")| O | | | | | | | |
| | | Incorrect Choice ("B") | | | | | O | | | |
| | | Partial Choice on Multi-Select ("A") | | | | | | O | | |
| | | Empty Answer Array ([]) | | | | | | | O | |
| | | Duplicate Options in Array (["A", "A"])| | | | | | | | O |
| **Confirm** | **Service Result**| Success = true | O | | | | O | O | O | O |
| | | Success = false | | O | O | O | | | | |
| | **Evaluation** | IsCorrect = true | O | | | | | | | |
| | | IsCorrect = false | | | | | O | O | O | O |
|  | **DB Submission** | Record Saved with Evaluation Result | O |   |   |   | O | O | O | O |
| | **Error Message** | None | O | | | | O | O | O | O |
| | | "You do not have permission..." | | O | | | | | | |
| | | "Question does not belong to exam" | | | O | | | | | |
| | | "Session already submitted" | | | | O | | | | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **N** | **B** | **B** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~FESubmissionServiceMcqPracticeTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 88 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F40` – `SubmissionController.SubmitFE & GetSessionSubmissions`

### 1. Header Information & Summary
| Code Module | MCQ & Practice Session | Method | SubmissionController.SubmitFE & GetSessionSubmissions |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student answer submission HTTP POST and session submission review HTTP GET endpoints, claim extraction, 401 for unauthenticated calls, combined FE & PE submission aggregations, and routing. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D23:N23, "P")`<br>**(11)** | `=COUNTIF(D23:N23, "F")`<br>**(0)** | `=COUNTIF(D23:N23, "")`<br>**(0)** | `=COUNTIF(D22:N22, "N")`<br>**(5)** | `=COUNTIF(D22:N22, "A")`<br>**(4)** | `=COUNTIF(D22:N22, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(11)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 | UTCID09 | UTCID10 | UTCID11 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Endpoint** | POST api/student/submissions/fe | O | O | O | | | | O | O | | | |
| | | GET api/student/submissions/session/{id}| | | | O | O | O | | | O | O | O |
| | **Caller** | Authenticated Student ("student-1") | O | | O | O | O | O | O | O | | O | O |
| | | Unauthenticated / No Token | | O | | | | | | | O | | |
| | **Session State** | Active Owned Session | O | O | | O | | | O | O | | O | O |
| | | Foreign / Unauthorized Session | | | | | O | | | | | | |
| | | Empty Submissions (0 items) | | | | | | | | | | O | |
| | | Combined FE + PE Submissions | | | | O | | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | | | O | | | O | O | | O | O |
| | | 401 Unauthorized | | O | | | | | | | O | | |
| | | 404 NotFound | | | | | O | | | | | | |
| | **Body Payload** | FESubmissionResultDto (IsCorrect) | O | | | | | | O | O | | | |
| | | SubmissionSessionResultDto | | | | O | | | | | | O | O |
| | | ApiResponse.Fail | | O | | | O | | | | O | | |
| | **Counts** | FE Count = 1, PE Count = 1 (Total = 2) | | | | O | | | | | | | O |
| | | Total Count = 0 | | | | | | | | | | O | |
| | **Routing** | Route & Authorize Attributes Valid | | | O | | | O | | | | | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **N** | **N** | **A** | **N** | **B** | **N** | **A** | **B** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~SubmissionControllerMcqPracticeTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 94 ms - BE_UnitTests.dll (net8.0)
```
