# UNIT TEST REPORT - BATCH 02 (F11 - F20)
> **Module**: AI Wallet & Payment, Knowledge Base & Document  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (58 / 58 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F11 - WalletTopupService.GetHistoryAsync](#sheet-f11--wallettopupservicegethistoryasync)
2. [Sheet F12 - WalletTopupService.GetByIdAsync](#sheet-f12--wallettopupservicegetbyidasync)
3. [Sheet F13 - WalletTopupService.CancelTopupAsync](#sheet-f13--wallettopupservicecanceltopupasync)
4. [Sheet F14 - WalletTopupService.HandlePayOSWebhookAsync](#sheet-f14--wallettopupservicehandlepayoswebhookasync)
5. [Sheet F15 - PaymentWebhookController.ReceiveWebhook](#sheet-f15--paymentwebhookcontrollerreceivewebhook)
6. [Sheet F16 - StudentWalletController.GetHistory](#sheet-f16--studentwalletcontrollergethistory)
7. [Sheet F17 - StudentWalletController.GetDetail](#sheet-f17--studentwalletcontrollergetdetail)
8. [Sheet F18 - StudentWalletController.Cancel](#sheet-f18--studentwalletcontrollercancel)
9. [Sheet F19 - StudentWalletController.GetAiTransactions](#sheet-f19--studentwalletcontrollergetaitransactions)
10. [Sheet F20 - DocumentService.UploadDocumentAsync](#sheet-f20--documentserviceuploaddocumentasync)

---

## Sheet `F11` – `WalletTopupService.GetHistoryAsync`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | WalletTopupService.GetHistoryAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student top-up payment history retrieval, status filtering (PENDING/PAID/CANCELLED/FAILED), pagination, and descending order by creation date. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:I22, "P")`<br>**(6)** | `=COUNTIF(D22:I22, "F")`<br>**(0)** | `=COUNTIF(D22:I22, "")`<br>**(0)** | `=COUNTIF(D21:I21, "N")`<br>**(3)** | `=COUNTIF(D21:I21, "A")`<br>**(2)** | `=COUNTIF(D21:I21, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **userId** | Valid Student ID ("student-1") | O | O | O | O | | O |
| | | Null or Empty String | | | | | O | |
| | **statusFilter** | null (All statuses) | O | | | | | |
| | | "PAID" | | O | | | | |
| | | "CANCELLED" | | | O | | | |
| | | "INVALID_STATUS" | | | | O | | |
| | **page / limit** | Default (1 / 20) | O | O | O | | | |
| | | Custom (0 / 0 - Invalid bounds) | | | | | | O |
| **Confirm** | **Return** | PagedResult<PaymentDto> | O | O | O | | | O |
| | | null | | | | O | O | |
| | **Items Count** | Full list matching user | O | | | | | |
| | | Filtered count (PAID only) | | O | | | | |
| | | Filtered count (CANCELLED only) | | | O | | | |
| | | Normalized Page=1, Limit=20 | | | | | | O |
| | **Exception** | None | O | O | O | | | O |
| | | BadRequestException | | | | O | O | |
| | **Log message** | None | O | O | O | | | O |
| | | "Invalid status filter." | | | | O | | |
| | | "User ID is required." | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~WalletTopupServiceGetHistoryTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 72 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F12` – `WalletTopupService.GetByIdAsync`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | WalletTopupService.GetByIdAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student top-up order detail query by orderCode / paymentId, enforcing ownership check and handling non-existent orders. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(2)** | `=COUNTIF(D19:H19, "A")`<br>**(3)** | `=COUNTIF(D19:H19, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **userId** | Student Owner ("student-1") | O | O | O | | O |
| | | Different Student ("student-2") | | | | O | |
| | **orderCode** | Valid Existing OrderCode (123456) | O | | | O | |
| | | Valid Existing PaymentId ("pay-1") | | O | | | |
| | | Non-existent OrderCode (999999) | | | O | | |
| | | Negative OrderCode (-1) | | | | | O |
| **Confirm** | **Return** | PaymentDetailDto | O | O | | | |
| | | null | | | O | O | O |
| | **Exception** | None | O | O | | | |
| | | NotFoundException | | | O | O | |
| | | BadRequestException | | | | | O |
| | **Log message** | None | O | O | | | |
| | | "Payment order not found." | | | O | | |
| | | "You are not authorized to view this payment." | | | | O | |
| | | "Invalid order code." | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~WalletTopupServiceGetByIdTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 68 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F13` – `WalletTopupService.CancelTopupAsync`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | WalletTopupService.CancelTopupAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student payment cancellation flow, ensuring only PENDING orders can be cancelled, updating status to CANCELLED, and notifying PayOS. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:I21, "P")`<br>**(6)** | `=COUNTIF(D21:I21, "F")`<br>**(0)** | `=COUNTIF(D21:I21, "")`<br>**(0)** | `=COUNTIF(D20:I20, "N")`<br>**(2)** | `=COUNTIF(D20:I20, "A")`<br>**(4)** | `=COUNTIF(D20:I20, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **userId** | Student Owner ("student-1") | O | O | O | O | O | |
| | | Different Student ("student-2") | | | | | | O |
| | **Current Status** | PENDING (Without reason) | O | | | | | |
| | | PENDING (With custom reason) | | O | | | | |
| | | PAID (Already Completed) | | | O | | | |
| | | CANCELLED (Already Cancelled) | | | | O | | |
| | | Non-existent Order Code | | | | | O | |
| **Confirm** | **Return** | `true` (Cancelled successfully) | O | O | | | | |
| | | `false` | | | O | O | O | O |
| | **DB Status** | Status Updated to CANCELLED | O | O | Unchanged | Unchanged | None | Unchanged |
| | **CancellationReason** | "User cancelled" (Default) | O | | None | None | None | None |
| | | "User selected wrong package" | | O | None | None | None | None |
| | **Exception** | None | O | O | | | | |
| | | BadRequestException | | | O | O | | |
| | | NotFoundException | | | | | O | O |
| | **Log message** | None | O | O | | | | |
| | | "Only pending payments can be cancelled."| | | O | O | | |
| | | "Payment order not found." | | | | | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~WalletTopupServiceCancelTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 65 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F14` – `WalletTopupService.HandlePayOSWebhookAsync`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | WalletTopupService.HandlePayOSWebhookAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify processing of PayOS IPN webhook, webhook signature verification, wallet balance increment upon payment success, and idempotency. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D23:J23, "P")`<br>**(7)** | `=COUNTIF(D23:J23, "F")`<br>**(0)** | `=COUNTIF(D23:J23, "")`<br>**(0)** | `=COUNTIF(D22:J22, "N")`<br>**(2)** | `=COUNTIF(D22:J22, "A")`<br>**(4)** | `=COUNTIF(D22:J22, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Signature** | Valid PayOS Webhook Signature | O | O | | O | O | O | O |
| | | Invalid Signature / Tampered Payload | | | O | | | | |
| | **Payment In DB** | Exists with Status PENDING | O | | | | O | O | O |
| | | Exists with Status PAID (Duplicate) | | O | | | | | |
| | | Non-existent Order Code | | | | O | | | |
| | **Webhook Code** | "00" (Success) | O | O | O | O | | | O |
| | | "01" (Payment Failed) | | | | | O | | |
| | | "02" (Cancelled by Gateway) | | | | | | O | |
| | **User Wallet** | Active User in DB | O | O | O | O | O | O | |
| | | User Not Found in DB | | | | | | | O |
| **Confirm** | **Return** | WebhookResultDto (Success = true)| O | O | | | O | O | |
| | | WebhookResultDto (Success = false)| | | O | O | | | O |
| | **DB Payment** | Status Updated to PAID | O | Unchanged | Unchanged | None | | | |
| | | Status Updated to FAILED | | | | | O | | |
| | | Status Updated to CANCELLED | | | | | | O | |
| | **Wallet Credit**| Balance Incremented (+Amount) | O | No (+0) | No (+0) | No (+0) | No (+0) | No (+0) | No (+0) |
| | **Exception** | None | O | O | None | None | O | O | None |
| | **Log message** | None | O | | | | | | |
| | | "Payment already processed." | | O | | | | | |
| | | "Invalid webhook signature." | | | O | | | | |
| | | "Payment order not found." | | | | O | | | |
| | | "User not found for wallet credit."| | | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **B** | **A** | **A** | **A** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~WalletTopupServiceHandleWebhookTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 84 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F15` – `PaymentWebhookController.ReceiveWebhook`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | PaymentWebhookController.ReceiveWebhook |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify webhook endpoint receiving PayOS HTTP POST callbacks, returning 200 OK for valid IPN and 400 Bad Request for malformed payloads. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:G19, "P")`<br>**(4)** | `=COUNTIF(D19:G19, "F")`<br>**(0)** | `=COUNTIF(D19:G19, "")`<br>**(0)** | `=COUNTIF(D18:G18, "N")`<br>**(2)** | `=COUNTIF(D18:G18, "A")`<br>**(2)** | `=COUNTIF(D18:G18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(4)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 |
|---|---|---|:---:|:---:|:---:|:---:|
| **Condition** | **Request Body** | Valid PayOS Webhook Body | O | | | |
| | | Valid PayOS Test Webhook Body | | O | | |
| | | Null / Empty Request Body | | | O | |
| | | Invalid Signature / Data Format | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | |
| | | 400 BadRequest | | | O | O |
| | **Service Invocation**| HandlePayOSWebhookAsync Called | O | O | | O |
| | **Response Body** | ApiResponse.Ok | O | O | | |
| | | ApiResponse.Fail | | | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~PaymentWebhookControllerReceiveWebhookTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 58 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F16` – `StudentWalletController.GetHistory`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | StudentWalletController.GetHistory |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student top-up transaction history HTTP endpoint, claim extraction, query validation, and pagination. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(2)** | `=COUNTIF(D19:H19, "A")`<br>**(2)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Claims Principal** | Authenticated Student ("student-1") | O | O | O | | O |
| | | Unauthenticated / Missing Claim | | | | O | |
| | **Query status** | null (All) | O | | | | |
| | | "PAID" | | O | | | |
| | | "INVALID" | | | O | | |
| | **Query page/limit** | 1 / 20 | O | O | O | O | |
| | | 0 / 0 (Invalid boundary) | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | O |
| | | 400 BadRequest | | | O | | |
| | | 401 Unauthorized | | | | O | |
| | **Body Payload** | PagedResult<PaymentDto> | O | O | | | O (Normalized)|
| | | ApiResponse.Fail | | | O | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentWalletControllerGetHistoryTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 64 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F17` – `StudentWalletController.GetDetail`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | StudentWalletController.GetDetail |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student top-up transaction detail HTTP endpoint by orderCode / paymentId, and authorization checks. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D19:H19, "P")`<br>**(5)** | `=COUNTIF(D19:H19, "F")`<br>**(0)** | `=COUNTIF(D19:H19, "")`<br>**(0)** | `=COUNTIF(D18:H18, "N")`<br>**(2)** | `=COUNTIF(D18:H18, "A")`<br>**(3)** | `=COUNTIF(D18:H18, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Claims Principal** | Authenticated Student ("student-1") | O | O | O | O | |
| | | Unauthenticated / No Token | | | | | O |
| | **orderCode** | Valid Existing OrderCode (123456) | O | | | | |
| | | Valid Existing PaymentId ("pay-1") | | O | | | |
| | | Non-existent OrderCode (999999) | | | O | | |
| | | Order Owned by Different Student | | | | O | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | |
| | | 404 NotFound | | | O | O | |
| | | 401 Unauthorized | | | | | O |
| | **Body Payload** | PaymentDetailDto | O | O | | | |
| | | ApiResponse.Fail | | | O | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentWalletControllerGetDetailTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 62 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F18` – `StudentWalletController.Cancel`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | StudentWalletController.Cancel |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student top-up order cancellation HTTP POST endpoint, validation of cancel reasons, and error mapping. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(2)** | `=COUNTIF(D19:H19, "A")`<br>**(3)** | `=COUNTIF(D19:H19, "B")`<br>**(0)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Claims Principal** | Authenticated Student ("student-1") | O | O | O | O | |
| | | Unauthenticated / No Token | | | | | O |
| | **orderCode** | Valid Existing PENDING Order | O | O | | | |
| | | Non-existent Order Code | | | O | | |
| | | Already PAID Order | | | | O | |
| | **Cancel Reason** | null (Default reason) | O | | | | |
| | | "User requested cancellation" | | O | | | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | |
| | | 404 NotFound | | | O | | |
| | | 400 BadRequest | | | | O | |
| | | 401 Unauthorized | | | | | O |
| | **Body Payload** | ApiResponse.Ok (Cancelled = true) | O | O | | | |
| | | ApiResponse.Fail | | | O | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentWalletControllerCancelTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 60 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F19` – `StudentWalletController.GetAiTransactions`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | StudentWalletController.GetAiTransactions |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student AI consumption billing transaction history endpoint, feature filter (CodeMentor/QGen), and date range filtering. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D21:J21, "P")`<br>**(7)** | `=COUNTIF(D21:J21, "F")`<br>**(0)** | `=COUNTIF(D21:J21, "")`<br>**(0)** | `=COUNTIF(D20:J20, "N")`<br>**(3)** | `=COUNTIF(D20:J20, "A")`<br>**(2)** | `=COUNTIF(D20:J20, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Claims Principal** | Authenticated Student ("student-1") | O | O | O | O | O | O | |
| | | Unauthenticated / No Token | | | | | | | O |
| | **Feature Filter** | null (All AI features) | O | | | | | | |
| | | "CodeMentor" | | O | | | | | |
| | | "QuestionGeneration" | | | O | | | | |
| | | "InvalidFeature" | | | | O | | | |
| | **Date Range** | null (All time) | O | O | O | | | | |
| | | Valid Range (From <= To) | | | | | O | | |
| | | Invalid Range (From > To) | | | | | | O | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | | O | | |
| | | 400 BadRequest | | | | O | | O | |
| | | 401 Unauthorized | | | | | | | O |
| | **Body Payload** | PagedResult<AIBillingTxnDto> | O | O | O | | O | | |
| | | ApiResponse.Fail | | | | O | | O | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **B** | **B** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~StudentWalletControllerGetAiTransactionsTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 81 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F20` – `DocumentService.UploadDocumentAsync`

### 1. Header Information & Summary
| Code Module | Knowledge Base & Document | Method | DocumentService.UploadDocumentAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student/teacher syllabus and document upload, file format validation (PDF, DOCX, TXT), size limit enforcement (max 20MB), and text extraction initiation. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D23:K23, "P")`<br>**(8)** | `=COUNTIF(D23:K23, "F")`<br>**(0)** | `=COUNTIF(D23:K23, "")`<br>**(0)** | `=COUNTIF(D22:K22, "N")`<br>**(3)** | `=COUNTIF(D22:K22, "A")`<br>**(3)** | `=COUNTIF(D22:K22, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **User Role** | Student / Teacher (Authorized) | O | O | O | O | O | O | O | |
| | | Unauthenticated User | | | | | | | | O |
| | **File Format** | PDF (".pdf") | O | | | | | | | |
| | | DOCX (".docx") | | O | | | | | | |
| | | TXT (".txt") | | | O | | | | | |
| | | EXE / Disallowed (".exe") | | | | O | | | | |
| | **File Size** | 2 MB (Normal valid) | O | O | O | O | | | | |
| | | 0 KB (Empty file) | | | | | O | | | |
| | | 25 MB (> 20 MB Limit) | | | | | | O | | |
| | | 20 MB (Exact Max Boundary) | | | | | | | O | |
| **Confirm** | **Return** | UploadDocumentResponseDto | O | O | O | | | | O | |
| | | null | | | | O | O | O | | O |
| | **DB Document** | Record Inserted with Status PENDING| O | O | O | | | | O | |
| | **Exception** | None | O | O | O | | | | O | |
| | | BadRequestException / Validation | | | | O | O | O | | |
| | | UnauthorizedException | | | | | | | | O |
| | **Log message** | None | O | O | O | | | | O | |
| | | "Unsupported file format." | | | | O | | | | |
| | | "Uploaded file cannot be empty." | | | | | O | | | |
| | | "File size exceeds 20MB limit." | | | | | | O | | |
| | | "Authentication required." | | | | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **A** | **B** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~DocumentServiceUploadTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 112 ms - BE_UnitTests.dll (net8.0)
```
