# UNIT TEST REPORT - BATCH 07 (F61 - F70)
> **Module**: AI RAG Retrieval, Document Semantic Search & AI Billing (VND)  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (46 / 46 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F61 - AIRetrievalController.SearchDocuments](#sheet-f61--airetrievalcontrollersearchdocuments)
2. [Sheet F62 - AIRetrievalController.GetSystemDocuments](#sheet-f62--airetrievalcontrollergetsystemdocuments)
3. [Sheet F63 - RetrievalPlannerService.SearchAsync](#sheet-f63--retrievalplannerservicesearchasync)
4. [Sheet F64 - RetrievalPlannerService.GetSystemDocumentsAsync](#sheet-f64--retrievalplannerservicegetsystemdocumentsasync)
5. [Sheet F65 - RetrievalPlannerService.GetCachedContextAsync](#sheet-f65--retrievalplannerservicegetcachedcontextasync)
6. [Sheet F66 - StudentDocumentController.BrowsePublicDocuments](#sheet-f66--studentdocumentcontrollerbrowsepublicdocuments)
7. [Sheet F67 - StudentDocumentController.Search](#sheet-f67--studentdocumentcontrollersearch)
8. [Sheet F68 - AIVndBillingService.GetRatesAsync](#sheet-f68--aivndbillingservicegetratesasync)
9. [Sheet F69 - AIVndBillingService.EnsureMinimumBalanceAsync](#sheet-f69--aivndbillingserviceensureminimumbalanceasync)
10. [Sheet F70 - AIVndBillingService.ChargeTokensAsync](#sheet-f70--aivndbillingservicechargetokensasync)

---

## Sheet `F61` – `AIRetrievalController.SearchDocuments`

### 1. Header Information & Summary
| Code Module | AI RAG Retrieval | Method | AIRetrievalController.SearchDocuments |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify AI RAG semantic search endpoint, query forwarding, topK result limits, threshold filtering, and Teacher/Admin authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Teacher | O | O | | O |
| | | Unauthenticated Caller | | | O | |
| | **Query search** | Valid Query ("Binary Search Tree") | O | | O | |
| | | Empty Search Query ("") | | O | | |
| | **topK / threshold**| Standard Valid (5 / 0.7) | O | O | O | |
| | | Boundary Max topK (50 items) | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | | | O |
| | | 400 BadRequest | | O | | |
| | | 401 Unauthorized | | | O | |
| | **Body Payload** | ApiResponse.Ok (List<RetrievedChunkDto>)| O | | | O |
| | | ApiResponse.Fail | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIRetrievalControllerBrowsingTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 62 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F62` – `AIRetrievalController.GetSystemDocuments`

### 1. Header Information & Summary
| Code Module | AI RAG Retrieval | Method | AIRetrievalController.GetSystemDocuments |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify retrieval of curated system documents and global knowledge base chunks used for default RAG context. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated User | O | O | | O |
| | | Unauthenticated Caller | | | O | |
| | **courseId** | null (Global system docs) | O | | | |
| | | "CSD201" (Course-scoped system docs) | | O | | |
| | | Boundary Empty CourseId ("") | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | O |
| | | 401 Unauthorized | | | O | |
| | **Body Payload** | ApiResponse.Ok (List<SystemDocDto>) | O | O | | O |
| | | ApiResponse.Fail | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIRetrievalControllerSystemDocumentsTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 58 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F63` – `RetrievalPlannerService.SearchAsync`

### 1. Header Information & Summary
| Code Module | AI RAG Retrieval | Method | RetrievalPlannerService.SearchAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify semantic embedding search, cosine similarity ranking, threshold filtering (>= 0.7), and chunk assembling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Vector DB** | Chunks with High Similarity (> 0.85) | O | | | | | |
| | | Chunks with Moderate Similarity (0.75)| | O | | | | |
| | | Chunks below Threshold (< 0.7) | | | O | | | |
| | | Vector DB Connection Timeout | | | | O | | |
| | | Empty Search Query String | | | | | O | |
| | | Exact Threshold Boundary (0.7000) | | | | | | O |
| **Confirm** | **Return** | List<RetrievedChunkDto> | O | O | O (0 items)| | | O (1 item) |
| | | null | | | | O | O | |
| | **Ordering** | Sorted by Similarity Descending | O | O | None | None | None | O |
| | **Exception** | None | O | O | O | | | O |
| | | VectorDbException | | | | O | | |
| | | ValidationException | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~RetrievalPlannerServiceBrowsingTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 76 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F64` – `RetrievalPlannerService.GetSystemDocumentsAsync`

### 1. Header Information & Summary
| Code Module | AI RAG Retrieval | Method | RetrievalPlannerService.GetSystemDocumentsAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify system documents query by courseId, active status filtering, and fallback to global documents if course has none. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **courseId** | Course with Active System Docs | O | | | |
| | | Course with 0 System Docs (Global fallback)| | O | | |
| | | DB Connection Failure | | | O | |
| | | null (Global Docs Query) | | | | O |
| **Confirm** | **Return** | List<SystemDocDto> | O | O (Global) | | O |
| | | null | | | O | |
| | **Exception** | None | O | O | | O |
| | | DatabaseException | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~RetrievalPlannerServiceSystemDocumentsTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 60 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F65` – `RetrievalPlannerService.GetCachedContextAsync`

### 1. Header Information & Summary
| Code Module | AI RAG Retrieval | Method | RetrievalPlannerService.GetCachedContextAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify in-memory/Redis caching of RAG context vectors, cache hit retrieval, cache miss calculation, and TTL expiration. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Cache State** | Key Exists (Cache Hit) | O | | | | |
| | | Key Missing (Cache Miss) | | O | | | |
| | | Cache Key Expired (TTL reached) | | | O | | |
| | | Redis Server Offline | | | | O | |
| | | Boundary Large Context (250KB) | | | | | O |
| **Confirm** | **Return** | CachedContextDto | O | O (Calculated)| O (Recalculated)| O (Fallback)| O |
| | **Execution** | Vector DB Queried | No | Yes | Yes | Yes | Yes |
| | **Exception** | None | O | O | O | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~RetrievalPlannerServiceCachingTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 64 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F66` – `StudentDocumentController.BrowsePublicDocuments`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | StudentDocumentController.BrowsePublicDocuments |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student public syllabus document browsing endpoint, course filtering, pagination, and claim validation. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(2)** | `=COUNTIF(D19:H19, "A")`<br>**(2)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Student | O | O | O | | O |
| | | Unauthenticated Caller | | | | O | |
| | **courseId** | null (All courses) | O | | | | |
| | | "CSD201" (DSA Course) | | O | | | |
| | | "NonExistentCourse" | | | O | | |
| | **page / limit** | Default (1 / 20) | O | O | O | O | |
| | | Invalid Lower Bounds (0 / 0) | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | | O |
| | | 401 Unauthorized | | | | O | |
| | **Body Payload** | ApiResponse.Ok (PagedPublicDocs) | O | O | O (0 items)| | O |
| | | ApiResponse.Fail | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentDocumentControllerBrowsingTests&Name~Browse"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 69 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F67` – `StudentDocumentController.Search`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | StudentDocumentController.Search |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student keyword/title document search endpoint, course scoping, and 401 unauthorized handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(2)** | `=COUNTIF(D19:H19, "A")`<br>**(2)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Authenticated Student | O | O | O | | O |
| | | Unauthenticated Caller | | | | O | |
| | **keyword** | "Syllabus" (Matching title) | O | | | | |
| | | "Exam Guide" (Matching course doc)| | O | | | |
| | | "UnknownDoc" (0 matches) | | | O | | |
| | | Boundary Whitespace (" syllabus ") | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | | O |
| | | 401 Unauthorized | | | | O | |
| | **Body Payload** | ApiResponse.Ok (List<DocumentDto>) | O | O | O (0 items)| | O |
| | | ApiResponse.Fail | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentDocumentControllerBrowsingTests&Name~Search"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 67 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F68` – `AIVndBillingService.GetRatesAsync`

### 1. Header Information & Summary
| Code Module | AI Billing (VND) | Method | AIVndBillingService.GetRatesAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify conversion rates retrieval for AI prompt and completion tokens to VND per model, and fallback default pricing. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **modelName** | "gpt-4o-mini" (Configured model) | O | | | |
| | | "claude-3-5-sonnet" (Custom pricing) | | O | | |
| | | "unconfigured-model" (Fallback trigger) | | | O | |
| | | Boundary Whitespace (" gpt-4o-mini ") | | | | O |
| **Confirm** | **Return** | TokenPricingRateDto | O | O | O (Default)| O |
| | **PromptRate** | Rate per 1k input tokens (e.g. 3.5 VND)| O | O | Default | O |
| | **CompletionRate**| Rate per 1k output tokens (e.g. 14 VND)| O | O | Default | O |
| | **Exception** | None | O | O | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIVndBillingServiceConfigurationTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 58 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F69` – `AIVndBillingService.EnsureMinimumBalanceAsync`

### 1. Header Information & Summary
| Code Module | AI Billing (VND) | Method | AIVndBillingService.EnsureMinimumBalanceAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student wallet pre-flight check before initiating AI generation or mentor hints, requiring minimum balance threshold (e.g. 500 VND). |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(1)** | `=COUNTIF(D18:G18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Wallet Balance** | Balance >= 500 VND (e.g. 10,000 VND) | O | | | |
| | | Balance < 500 VND (e.g. 200 VND) | | O | | |
| | | Exact Minimum Boundary (500 VND) | | | | O |
| | **User Record** | User Record Missing in DB | | | O | |
| **Confirm** | **Return** | `true` (Allowed to proceed) | O | | | O |
| | | `false` | | O | O | |
| | **Exception** | None | O | | | O |
| | | InsufficientBalanceException | | O | | |
| | | NotFoundException | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIVndBillingServiceEnsureMinimumBalanceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 59 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F70` – `AIVndBillingService.ChargeTokensAsync`

### 1. Header Information & Summary
| Code Module | AI Billing (VND) | Method | AIVndBillingService.ChargeTokensAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify token-to-VND conversion calculation, wallet balance deduction, transaction logging with feature tagging (QGen/CodeMentor), and atomicity. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:H21, "P")`<br>**(5)** | `=COUNTIF(D21:H21, "F")`<br>**(0)** | `=COUNTIF(D21:H21, "")`<br>**(0)** | `=COUNTIF(D20:H20, "N")`<br>**(3)** | `=COUNTIF(D20:H20, "A")`<br>**(1)** | `=COUNTIF(D20:H20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Feature Tag** | Feature: CodeMentor | O | | | | |
| | | Feature: QuestionGeneration | | O | | | |
| | **Token Count** | Standard (500 prompt, 200 comp) | O | O | | | |
| | | High Volume (10,000 prompt tokens) | | | O | | |
| | | Balance Less Than Charge Amount | | | | O | |
| | | Zero Tokens (0 prompt, 0 comp) | | | | | O |
| **Confirm** | **Return** | BillingReceiptDto (AmountVnd) | O | O | O | | O (0 VND) |
| | **Wallet DB** | Balance Decremented Exactly | O | O | O | Unchanged | Unchanged |
| | **Audit Log** | AIBillingTransaction Record Inserted | O | O | O | None | O |
| | **Exception** | None | O | O | O | | O |
| | | InsufficientBalanceException | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **B** | **A** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIVndBillingServiceChargeTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 68 ms - BE_UnitTests.dll (net8.0)
```
