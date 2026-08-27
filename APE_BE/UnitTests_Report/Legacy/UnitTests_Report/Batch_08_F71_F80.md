# UNIT TEST REPORT - BATCH 08 (F71 - F80)
> **Module**: AI Gateway, Routing, Rate Limiting & Admin AI Governance  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (59 / 59 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F71 - AIFeatureRoutingAndPromptService.ResolveRouteAsync](#sheet-f71--aifeatureroutingandpromptserviceresolverouteasync)
2. [Sheet F72 - AIGatekeeperService.CheckAccessAndThrottleAsync](#sheet-f72--aigatekeeperservicecheckaccessandthrottleasync)
3. [Sheet F73 - AIProviderModelCatalogService.GetActiveModelsAsync](#sheet-f73--aiprovidermodelcatalogservicegetactivemodelsasync)
4. [Sheet F74 - AdminAIController.GetCatalog](#sheet-f74--adminaicontrollergetcatalog)
5. [Sheet F75 - AdminAIController.UpdateModelConfig](#sheet-f75--adminaicontrollerupdatemodelconfig)
6. [Sheet F76 - AdminAIController.GetPricing](#sheet-f76--adminaicontrollergetpricing)
7. [Sheet F77 - AdminAIController.UpdatePricing](#sheet-f77--adminaicontrollerupdatepricing)
8. [Sheet F78 - AdminAIController.GetUsageStats](#sheet-f78--adminaicontrollergetusagestats)
9. [Sheet F79 - AdminAIController.GetRoutingRules](#sheet-f79--adminaicontrollergetroutingrules)
10. [Sheet F80 - AdminAIController.UpdateRoutingRules](#sheet-f80--adminaicontrollerupdateroutingrules)

---

## Sheet `F71` – `AIFeatureRoutingAndPromptService.ResolveRouteAsync`

### 1. Header Information & Summary
| Code Module | AI Gateway & Routing | Method | AIFeatureRoutingAndPromptService.ResolveRouteAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify AI model resolution by feature (QGen vs CodeMentor), primary model selection, fallback model fallback when primary is unavailable, and temperature/token configurations. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Feature Key** | "QuestionGeneration" | O | | | | |
| | | "CodeMentor" | | O | | | |
| | **Primary Model** | Healthy / Active ("gpt-4o") | O | O | | | O |
| | | Inactive / Degraded Provider | | | O | | |
| | | Unknown Feature Key | | | | O | |
| | **Fallback Model**| Healthy / Active ("claude-3-5-sonnet") | | | O | | |
| **Confirm** | **Return** | ResolvedAIRouteDto | O | O | O (Fallback)| | O |
| | | null | | | | O | |
| | **Selected Model**| "gpt-4o" | O | O | | | "gpt-4o" |
| | | "claude-3-5-sonnet" | | | O | | |
| | **Exception** | None | O | O | O | | O |
| | | ArgumentException | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **B** | **A** | **N** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIFeatureRoutingAndPromptServiceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 64 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F72` – `AIGatekeeperService.CheckAccessAndThrottleAsync`

### 1. Header Information & Summary
| Code Module | AI Gateway & Rate Limiting | Method | AIGatekeeperService.CheckAccessAndThrottleAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify rate limiting per student (RPM/TPM limits), sliding window enforcement, circuit breaker trip on error rate spikes, and quota replenishment. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:H21, "P")`<br>**(5)** | `=COUNTIF(D21:H21, "F")`<br>**(0)** | `=COUNTIF(D21:H21, "")`<br>**(0)** | `=COUNTIF(D20:H20, "N")`<br>**(2)** | `=COUNTIF(D20:H20, "A")`<br>**(2)** | `=COUNTIF(D20:H20, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Request Count** | Within Limit (< 10 req/min) | O | | | | |
| | | Exceeded Limit (> 10 req/min) | | O | | | |
| | | Exact Boundary Limit (10 req/min) | | | O | | |
| | **Circuit Breaker**| Normal / Closed State | O | O | O | | O |
| | | Tripped / Open State (High Error Rate)| | | | O | |
| | **User Account** | Banned / Disabled User | | | | | O |
| **Confirm** | **Return** | GatekeeperResult (Allowed = true) | O | | O | | |
| | | GatekeeperResult (Allowed = false)| | O | | O | O |
| | **Exception** | None | O | | O | | |
| | | RateLimitExceededException | | O | | | |
| | | ServiceUnavailableException | | | | O | |
| | | ForbiddenException | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **B** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIGatekeeperServiceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 62 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F73` – `AIProviderModelCatalogService.GetActiveModelsAsync`

### 1. Header Information & Summary
| Code Module | AI Provider Catalog | Method | AIProviderModelCatalogService.GetActiveModelsAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify multi-provider model catalog query (OpenAI, Anthropic, DeepSeek, Local LLM), health state checking, and enabled model filtering. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Catalog State** | 3 Enabled Providers (OpenAI, Anthropic) | O | | | | |
| | | 1 Provider Disabled / Degraded | | O | | | |
| | | Empty Catalog (0 providers configured) | | | O | | |
| | | Single Provider Active | | | | | O |
| | **DB Connection** | Database Unreachable | | | | O | |
| **Confirm** | **Return** | List<AIModelCatalogDto> | O | O | O (0 items)| | O |
| | | null | | | | O | |
| | **Items Count** | 3 | O | | | | |
| | | 2 (Active only) | | O | | | |
| | **Exception** | None | O | O | O | | O |
| | | DatabaseException | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AIProviderModelCatalogServiceTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 60 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F74` – `AdminAIController.GetCatalog`

### 1. Header Information & Summary
| Code Module | Admin AI Governance | Method | AdminAIController.GetCatalog |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative AI model catalog management endpoint, listing providers, model versions, and Admin authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller Role** | Admin (Authorized) | O | O | O | O | | O |
| | | Non-Admin / Student Caller | | | | | O | |
| | **Catalog State** | Multiple Active Providers | O | | | | | |
| | | Filter by Provider ("openai") | | O | | | | |
| | | Empty Filter Match | | | O | | | |
| | | Service Failure / Exception | | | | O | | |
| | | Boundary Large Provider List | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | | | O |
| | | 403 Forbidden / 401 Unauth | | | | | O | |
| | | 500 InternalServerError | | | | O | | |
| | **Body Payload** | ApiResponse.Ok (CatalogList) | O | O | O | | | O |
| | | ApiResponse.Fail | | | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AdminAIControllerTests&Name~GetCatalog"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 74 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F75` – `AdminAIController.UpdateModelConfig`

### 1. Header Information & Summary
| Code Module | Admin AI Governance | Method | AdminAIController.UpdateModelConfig |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative model configuration updates (Enable/Disable, MaxTokens, Temperature, ApiKey reference), payload validation, and Admin authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Admin (Authorized) | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **modelId** | Valid Existing Model ("gpt-4o") | O | O | | | | O |
| | | Non-existent Model ID | | | O | | | |
| | **Config DTO** | Valid Params (Temp: 0.7, MaxTok: 4096)| O | | | | | |
| | | Enable/Disable Toggle (IsEnabled = false)| | O | | | | |
| | | Invalid Bounds (Temp: 2.5, MaxTok: -1)| | | | O | | |
| | | Boundary Max Tokens (128,000) | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | | O |
| | | 404 NotFound | | | O | | | |
| | | 400 BadRequest | | | | O | | |
| | | 401 Unauthorized | | | | | O | |
| | **Body Payload** | ApiResponse.Ok (UpdatedModelDto) | O | O | | | | O |
| | | ApiResponse.Fail | | | O | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AdminAIControllerTests&Name~UpdateModelConfig"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 76 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F76` – `AdminAIController.GetPricing`

### 1. Header Information & Summary
| Code Module | Admin AI Governance | Method | AdminAIController.GetPricing |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative token pricing rates endpoint, model pricing table query, and Admin role authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:I19, "P")`<br>**(6)** | `=COUNTIF(D19:I19, "F")`<br>**(0)** | `=COUNTIF(D19:I19, "")`<br>**(0)** | `=COUNTIF(D18:I18, "N")`<br>**(3)** | `=COUNTIF(D18:I18, "A")`<br>**(2)** | `=COUNTIF(D18:I18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Admin (Authorized) | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **Model Filter** | null (All model pricing) | O | | | | | |
| | | "gpt-4o-mini" (Single model pricing)| | O | | | | |
| | | "unconfigured-model" | | | O | | | |
| | | DB Query Error | | | | O | | |
| | | Boundary Whitespace (" gpt-4o ") | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | | | O |
| | | 401 Unauthorized | | | | | O | |
| | | 500 InternalServerError | | | | O | | |
| | **Body Payload** | ApiResponse.Ok (PricingTableDto) | O | O | O | | | O |
| | | ApiResponse.Fail | | | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AdminAIControllerTests&Name~GetPricing"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 73 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F77` – `AdminAIController.UpdatePricing`

### 1. Header Information & Summary
| Code Module | Admin AI Governance | Method | AdminAIController.UpdatePricing |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative token price adjustment HTTP PUT endpoint, rates validation (positive values), and Admin role authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Admin (Authorized) | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **modelName** | "gpt-4o" (Valid model) | O | O | | | | O |
| | | Non-existent Model Name | | | O | | | |
| | **Pricing Rates**| Valid Rates (Prompt: 5 VND, Comp: 20)| O | | | | | |
| | | Negative Rates (Prompt: -1 VND) | | O | | | | |
| | | Decimal Micro Rates (Prompt: 0.005) | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | | | | | O |
| | | 400 BadRequest | | O | | | | |
| | | 404 NotFound | | | O | | | |
| | | 401 Unauthorized | | | | | O | |
| | **Body Payload** | ApiResponse.Ok (UpdatedPricingDto)| O | | | | | O |
| | | ApiResponse.Fail | | O | O | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AdminAIControllerTests&Name~UpdatePricing"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 71 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F78` – `AdminAIController.GetUsageStats`

### 1. Header Information & Summary
| Code Module | Admin AI Governance | Method | AdminAIController.GetUsageStats |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative AI consumption statistics endpoint, date range filtering, breakdown by feature/model/student, and total VND cost calculations. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:K21, "P")`<br>**(8)** | `=COUNTIF(D21:K21, "F")`<br>**(0)** | `=COUNTIF(D21:K21, "")`<br>**(0)** | `=COUNTIF(D20:K20, "N")`<br>**(3)** | `=COUNTIF(D20:K20, "A")`<br>**(3)** | `=COUNTIF(D20:K20, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Admin (Authorized) | O | O | O | O | O | | O | O |
| | | Unauthenticated Caller | | | | | | O | | |
| | **Date Range** | null (All time usage) | O | | | | | | | |
| | | Valid Range (Last 7 Days) | | O | | | | | | |
| | | Invalid Range (From > To) | | | O | | | | | |
| | **Filter Feature**| "CodeMentor" | | | | O | | | | |
| | | "QuestionGeneration" | | | | | O | | | |
| | | Boundary Single Day (From == To) | | | | | | | O | |
| | | High Volume Aggregation (1M Tokens)| | | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | O | O | | O | O |
| | | 400 BadRequest | | | O | | | | | |
| | | 401 Unauthorized | | | | | | O | | |
| | **Body Payload** | ApiResponse.Ok (AIUsageStatsDto) | O | O | | O | O | | O | O |
| | | ApiResponse.Fail | | | O | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **N** | **N** | **A** | **B** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AdminAIControllerTests&Name~GetUsageStats"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 82 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F79` – `AdminAIController.GetRoutingRules`

### 1. Header Information & Summary
| Code Module | Admin AI Governance | Method | AdminAIController.GetRoutingRules |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative AI routing rules query endpoint, mapping feature keys to primary & fallback models. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:I19, "P")`<br>**(6)** | `=COUNTIF(D19:I19, "F")`<br>**(0)** | `=COUNTIF(D19:I19, "")`<br>**(0)** | `=COUNTIF(D18:I18, "N")`<br>**(3)** | `=COUNTIF(D18:I18, "A")`<br>**(2)** | `=COUNTIF(D18:I18, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Admin (Authorized) | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **Rules State** | Configured Rules for All Features | O | | | | | |
| | | Partial Rules Configured | | O | | | | |
| | | 0 Rules Configured (Default state) | | | O | | | |
| | | Database Read Error | | | | O | | |
| | | Boundary Large Rule Matrix | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | | | O |
| | | 401 Unauthorized | | | | | O | |
| | | 500 InternalServerError | | | | O | | |
| | **Body Payload** | ApiResponse.Ok (List<RoutingRuleDto>)| O | O | O | | | O |
| | | ApiResponse.Fail | | | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AdminAIControllerTests&Name~GetRoutingRules"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 74 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F80` – `AdminAIController.UpdateRoutingRules`

### 1. Header Information & Summary
| Code Module | Admin AI Governance | Method | AdminAIController.UpdateRoutingRules |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative AI routing rules modification HTTP PUT endpoint, primary/fallback model validation, and Admin authorization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Admin (Authorized) | O | O | O | O | | O |
| | | Unauthenticated Caller | | | | | O | |
| | **featureKey** | "CodeMentor" (Valid feature) | O | O | | | | O |
| | | "InvalidFeatureKey" | | | O | | | |
| | **Rule DTO** | Valid Primary & Fallback Models | O | | | | | |
| | | Non-existent Primary Model ("fake-model")| | O | | | | |
| | | Missing Fallback Model (null) | | | | O | | |
| | | Boundary Same Model for Primary & Fallback| | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | | | | | O |
| | | 400 BadRequest | | O | O | O | | |
| | | 401 Unauthorized | | | | | O | |
| | **Body Payload** | ApiResponse.Ok (UpdatedRuleDto) | O | | | | | O |
| | | ApiResponse.Fail | | O | O | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AdminAIControllerTests&Name~UpdateRoutingRules"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 77 ms - BE_UnitTests.dll (net8.0)
```
