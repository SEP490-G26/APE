# UNIT TEST REPORT - BATCH 03 (F21 - F30)
> **Module**: Knowledge Base & Document, Course & Syllabus Management  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (49 / 49 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F21 - DocumentService.GetExtractionDraftAsync](#sheet-f21--documentservicegetextractiondraftasync)
2. [Sheet F22 - DocumentService.ListByUserAsync](#sheet-f22--documentservicelistbyuserasync)
3. [Sheet F23 - DocumentController.Download](#sheet-f23--documentcontrollerdownload)
4. [Sheet F24 - DocumentController.GetExtractionDraft](#sheet-f24--documentcontrollergetextractiondraft)
5. [Sheet F25 - FileExtractionService.ExtractTextAsync](#sheet-f25--fileextractionserviceextracttextasync)
6. [Sheet F26 - CourseService.CreateCourseAsync](#sheet-f26--courseservicecreatecourseasync)
7. [Sheet F27 - CourseService.UpdateCourseAsync](#sheet-f27--courseserviceupdatecourseasync)
8. [Sheet F28 - CourseService.DeleteCourseAsync](#sheet-f28--courseservicedeletecourseasync)
9. [Sheet F29 - CourseService.GetPagedAsync](#sheet-f29--courseservicegetpagedasync)
10. [Sheet F30 - CourseController.GetList](#sheet-f30--coursecontrollergetlist)

---

## Sheet `F21` – `DocumentService.GetExtractionDraftAsync`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | DocumentService.GetExtractionDraftAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify retrieval of document text extraction draft, checking processing status (PENDING/COMPLETED/FAILED), and ownership validation. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(2)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **userId** | Document Owner ("student-1") | O | O | | O |
| | | Unauthorized User ("student-2") | | | O | |
| | **documentId** | Valid COMPLETED Document ID | O | | | |
| | | Valid PENDING Document ID | | O | | |
| | | Valid Document ID (Not Owner) | | | O | |
| | | Non-existent Document ID | | | | O |
| **Confirm** | **Return** | ExtractionDraftDto (Text Content) | O | | | |
| | | ExtractionDraftDto (Status: PENDING) | | O | | |
| | | null | | | O | O |
| | **Exception** | None | O | O | | |
| | | ForbiddenException | | | O | |
| | | NotFoundException | | | | O |
| | **Log message** | None | O | O | | |
| | | "You are not authorized to view this document." | | | O | |
| | | "Document not found." | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~DocumentServiceGetExtractionDraftTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 155 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F22` – `DocumentService.ListByUserAsync`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | DocumentService.ListByUserAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student/teacher uploaded documents listing, courseId filtering, pagination, and descending sort order by creation date. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **userId** | Valid User ID ("user-1") | O | O | O | | O |
| | | Null / Empty User ID | | | | O | |
| | **courseId** | null (All courses) | O | | | | |
| | | "course-dsa" (Filtered) | | O | | | |
| | | "course-empty" (No docs) | | | O | | |
| | **page / limit** | Default (1 / 20) | O | O | O | | |
| | | Custom Boundary (0 / 0) | | | | | O |
| **Confirm** | **Return** | PagedResult<DocumentSummaryDto> | O | O | O | | O |
| | | null | | | | O | |
| | **Items Count** | Full list matching user | O | | | | |
| | | Filtered count (DSA only) | | O | | | |
| | | 0 items | | | O | | |
| | | Normalized Page=1, Limit=20 | | | | | O |
| | **Exception** | None | O | O | O | | O |
| | | BadRequestException | | | | O | |
| | **Log message** | None | O | O | O | | O |
| | | "User ID is required." | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~DocumentServiceListByUserTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 74 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F23` – `DocumentController.Download`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | DocumentController.Download |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify document binary stream download endpoint, content type determination, file existence, and ownership check. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(2)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Owner ("student-1") | O | O | | O |
| | | Different Student ("student-2") | | | O | |
| | **File Storage** | File Exists in Local/S3 Storage (PDF) | O | | O | |
| | | File Exists in Local/S3 Storage (DOCX)| | O | | |
| | | File Missing on Disk / Storage | | | | O |
| **Confirm** | **HTTP Status** | 200 OK (FileStreamResult) | O | O | | |
| | | 403 Forbidden | | | O | |
| | | 404 NotFound | | | | O |
| | **ContentType**| "application/pdf" | O | | | |
| | | "application/vnd.openxmlformats..." | | O | | |
| | **Body Payload**| Binary Stream | O | O | | |
| | | ApiResponse.Fail | | | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~DocumentControllerDownloadTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 67 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F24` – `DocumentController.GetExtractionDraft`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | DocumentController.GetExtractionDraft |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify HTTP GET endpoint for extracted document draft text, claim validation, 404 for missing docs, and 403 for unauthorized users. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(2)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Owner ("student-1") | O | O | | O |
| | | Unauthenticated / Unauthorized User | | | O | |
| | **documentId** | Valid Completed Extraction Doc | O | | | |
| | | Valid In-Progress Extraction Doc | | O | | |
| | | Valid Doc ID (Different User) | | | O | |
| | | Non-existent Document ID | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | |
| | | 403 Forbidden | | | O | |
| | | 404 NotFound | | | | O |
| | **Payload** | ExtractionDraftDto (Completed) | O | | | |
| | | ExtractionDraftDto (PENDING) | | O | | |
| | | ApiResponse.Fail | | | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~DocumentControllerGetExtractionDraftTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 65 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F25` – `FileExtractionService.ExtractTextAsync`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | FileExtractionService.ExtractTextAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify text extraction parsing engine for PDF, DOCX, and TXT files, chunking, character cleanup, and corrupted file handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **File Stream** | Valid Text-based PDF Stream | O | | | | |
| | | Valid OpenXML DOCX Stream | | O | | | |
| | | Valid UTF-8 TXT Stream | | | O | | |
| | | Corrupted / Invalid Binary Stream | | | | O | |
| | | Empty 0-byte Stream | | | | | O |
| **Confirm** | **Return** | ExtractedTextResult (Length > 0) | O | O | O | | |
| | | ExtractedTextResult (Length = 0) | | | | | O |
| | | null | | | | O | |
| | **Exception** | None | O | O | O | | O |
| | | DocumentParsingException | | | | O | |
| | **Log message** | None | O | O | O | | O |
| | | "Failed to parse document stream." | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~FileExtractionServiceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 95 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F26` – `CourseService.CreateCourseAsync`

### 1. Header Information & Summary
| Code Module | Course & Syllabus Management | Method | CourseService.CreateCourseAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify course creation, course code uniqueness validation, required field checking (Code, Name, Subject), and trimming. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:I22, "P")`<br>**(6)** | `=COUNTIF(D22:I22, "F")`<br>**(0)** | `=COUNTIF(D22:I22, "")`<br>**(0)** | `=COUNTIF(D21:I21, "N")`<br>**(2)** | `=COUNTIF(D21:I21, "A")`<br>**(3)** | `=COUNTIF(D21:I21, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | Course Code Does Not Exist in DB | O | O | | O | O | O |
| | | Course Code Already Exists in DB | | | O | | | |
|  | **Code** | "CSD201" (Valid) | O |   | O |   |   |   |
| | | "CSD201" |   |   |   |   | O |   |
| | | " csd201 " |   |   |   |   |   | O |
| | | Null / Empty Code | | | | O | | |
|  | **Name** | "Data Structures & Algorithms" | O |   | O | O |   | O |
| | | "Java Programming" |   | O |   |   |   |   |
|  | **Subject** | "Computer Science" | O |   | O | O | O | O |
| | | "Software" |   | O |   |   |   |   |
| **Confirm** | **Return** | CourseDto (Id != null) | O | O | | | | O |
| | | null | | | O | O | O | |
|  | **DB Course** | Inserted with Uppercase Code | O | O |   |   |   | O |
| | **Exception** | None | O | O | | | | O |
| | | ConflictException | | | O | | | |
| | | ValidationException / BadRequest | | | | O | O | |
| | **Log message** | None | O | O | | | | O |
| | | "Course code already exists." | | | O | | | |
| | | "Course code is required." | | | | O | | |
| | | "Course name is required." | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseServiceMutationTests&Name~CreateCourse"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 80 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F27` – `CourseService.UpdateCourseAsync`

### 1. Header Information & Summary
| Code Module | Course & Syllabus Management | Method | CourseService.UpdateCourseAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify course update flow, modifying name/description/subject, preventing duplicate code conflict with other courses, and missing course check. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:I22, "P")`<br>**(6)** | `=COUNTIF(D22:I22, "F")`<br>**(0)** | `=COUNTIF(D22:I22, "")`<br>**(0)** | `=COUNTIF(D21:I21, "N")`<br>**(2)** | `=COUNTIF(D21:I21, "A")`<br>**(3)** | `=COUNTIF(D21:I21, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | Target Course Exists in DB | O | O | O | | O | O |
| | | Target Course Not in DB | | | | O | | |
| | | New Code Conflict with Other Course | | | O | | | |
| | **courseId** | Valid Course ID ("course-1") | O | O | O | | O | O |
| | | Non-existent Course ID | | | | O | | |
|  | **Name** | "Updated DSA Course Name" | O |   |   |   |   |   |
| | | "New Name" |   | O |   |   |   |   |
| | | "Name" |   |   | O | O |   |   |
| | | "" (Empty) |   |   |   |   | O |   |
| | | " Trimmed Name " |   |   |   |   |   | O |
|  | **Code** | "CSD201" (Same Code) | O |   |   |   |   |   |
| | | "CSD202" (New Available) |   | O |   |   |   |   |
| | | "PRO192" (Taken) |   |   | O |   |   |   |
| | | "CSD201" |   |   |   | O | O | O |
| **Confirm** | **Return** | CourseDto (Updated fields) | O | O | | | | O |
| | | null | | | O | O | O | |
|  | **DB Course** | Record Fields Modified in DB | O | O |   |   |   | O |
| | **Exception** | None | O | O | | | | O |
| | | ConflictException | | | O | | | |
| | | NotFoundException | | | | O | | |
| | | ValidationException | | | | | O | |
| | **Log message** | None | O | O | | | | O |
| | | "Course code is already used by another course." | | | O | | | |
| | | "Course not found." | | | | O | | |
| | | "Course name cannot be empty." | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseServiceMutationTests&Name~UpdateCourse"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 79 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F28` – `CourseService.DeleteCourseAsync`

### 1. Header Information & Summary
| Code Module | Course & Syllabus Management | Method | CourseService.DeleteCourseAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify course deletion, preventing deletion if questions/exams are attached to the course, and handling non-existent course ID. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:H21, "P")`<br>**(5)** | `=COUNTIF(D21:H21, "F")`<br>**(0)** | `=COUNTIF(D21:H21, "")`<br>**(0)** | `=COUNTIF(D20:H20, "N")`<br>**(1)** | `=COUNTIF(D20:H20, "A")`<br>**(4)** | `=COUNTIF(D20:H20, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | Course Exists with 0 Questions/Exams | O | | | | |
| | | Course Exists with Active Questions | | O | | | |
| | | Course Exists with Active Exams | | | O | | |
| | | Course Record Not in DB | | | | O | |
| | **courseId** | Valid Course ID ("course-1") | O | O | O | | |
| | | Non-existent Course ID ("non-existent") | | | | O | |
| | | Null or Empty String | | | | | O |
| **Confirm** | **Return** | `true` (Deleted successfully) | O | | | | |
| | | `false` | | O | O | O | O |
|  | **DB Course** | Record Removed from DB | O |   |   |   |   |
| | **Exception** | None | O | | | | |
| | | ConflictException | | O | O | | |
| | | NotFoundException | | | | O | |
| | | BadRequestException | | | | | O |
| | **Log message** | None | O | | | | |
| | | "Cannot delete course with associated questions." | | O | | | |
| | | "Cannot delete course with associated exams." | | | O | | |
| | | "Course not found." | | | | O | |
| | | "Course ID is required." | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseServiceDeleteTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 71 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F29` – `CourseService.GetPagedAsync`

### 1. Header Information & Summary
| Code Module | Course & Syllabus Management | Method | CourseService.GetPagedAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify course catalog pagination, search filter by code or name, semester filter, and boundary page index handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **search** | null (All courses) | O | | | | |
| | | "CSD" (Matches Code) | | O | | | |
| | | "Java" (Matches Name) | | | O | | |
| | | "NonExistentTerm" | | | | O | |
| | **page / limit** | Default (1 / 20) | O | O | O | O | |
| | | Custom Boundary (0 / 0) | | | | | O |
| **Confirm** | **Return** | PagedResult<CourseDto> | O | O | O | O | O |
| | **Items Count** | Total courses in DB | O | | | | |
| | | Filtered count (CSD only) | | O | | | |
| | | Filtered count (Java only) | | | O | | |
| | | 0 items | | | | O | |
| | | Normalized Page=1, Limit=20 | | | | | O |
| | **Exception** | None | O | O | O | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseServiceGetPagedTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 73 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F30` – `CourseController.GetList`

### 1. Header Information & Summary
| Code Module | Course & Syllabus Management | Method | CourseController.GetList |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify HTTP GET course listing endpoint, query parameter binding, 200 OK response format, and error handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(2)** | `=COUNTIF(D19:H19, "A")`<br>**(2)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated User | O | O | O | O | |
| | | Unauthenticated User | | | | | O |
| | **Query search** | null | O | | | | |
| | | "CSD201" (Keyword) | | O | | | |
| | | "UnknownCourse" | | | O | | |
| | **Query page/limit** | 1 / 20 | O | O | O | | |
| | | 0 / 0 (Invalid boundary) | | | | O | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | O | |
| | | 401 Unauthorized | | | | | O |
| | **Body Payload** | ApiResponse.Ok (Paged Courses) | O | O | O | O | |
| | | ApiResponse.Fail | | | | | O |
|  | **Paging Info** | Normalized Page=1, Limit=20 | O | O | O | O |   |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **B** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~CourseControllerGetListTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 69 ms - BE_UnitTests.dll (net8.0)
```
