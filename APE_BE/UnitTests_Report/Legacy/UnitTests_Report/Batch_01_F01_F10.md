# UNIT TEST REPORT - BATCH 01 (F01 - F10)
> **Module**: Authentication & Account, AI Wallet & Payment  
> **Standard**: FPT University Capstone / MTCA Unit Test Report  
> **Status**: Passed (73 / 73 Test Cases - 100%)

---

## Table of Contents
1. [Sheet F01 - AuthService.GoogleLoginAsync](#sheet-f01--authservicegoogleloginasync)
2. [Sheet F02 - AuthService.RefreshTokenAsync](#sheet-f02--authservicerefreshtokenasync)
3. [Sheet F03 - AuthService.RevokeTokenAsync](#sheet-f03--authservicerevoketokenasync)
4. [Sheet F04 - UserService.GetProfileAsync](#sheet-f04--userservicegetprofileasync)
5. [Sheet F05 - UserController.GetUsers](#sheet-f05--usercontrollergetusers)
6. [Sheet F06 - UserController.GetUserById](#sheet-f06--usercontrollergetuserbyid)
7. [Sheet F07 - UserController.UpdateUserStatus](#sheet-f07--usercontrollerupdateuserstatus)
8. [Sheet F08 - WalletTopupService.GetConstraints](#sheet-f08--wallettopupservicegetconstraints)
9. [Sheet F09 - WalletTopupService.GetPackagesAsync](#sheet-f09--wallettopupservicegetpackagesasync)
10. [Sheet F10 - WalletTopupService.CreateTopupAsync](#sheet-f10--wallettopupservicecreatetopupasync)

---

## Sheet `F01` – `AuthService.GoogleLoginAsync`

### 1. Header Information & Summary
| Code Module | Authentication & Account | Method | AuthService.GoogleLoginAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify Google OAuth token verification, account retrieval/creation, status validation (Active/Disabled/Banned), token generation, and expired token cleanup. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D26:L26, "P")`<br>**(9)** | `=COUNTIF(D26:L26, "F")`<br>**(0)** | `=COUNTIF(D26:L26, "")`<br>**(0)** | `=COUNTIF(D25:L25, "N")`<br>**(3)** | `=COUNTIF(D25:L25, "A")`<br>**(5)** | `=COUNTIF(D25:L25, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(9)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 | UTCID09 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | Google ID Token Signature Valid | O | O | O | O | O | O | | O | O |
| | | Google ID Token Signature Invalid | | | | | | | O | | |
| | | User Exists with GoogleId | O | | | O | O | O | | | O |
| | | User Exists with Email only | | O | | | | | | | |
| | | User Does Not Exist in DB | | | O | | | | | O | |
| | | Account Status = "Active" | O | O | | | | | | | O |
| | | Account Status = "Disabled" | | | | O | | | | | |
| | | Account Status = "Inactive" | | | | | O | | | | |
| | | Account Status = "Banned" | | | | | | O | | | |
| | | Stored Tokens Include Expired | | | | | | | | | O |
| | **idToken** | Valid Token String | O | O | O | O | O | O | | O | O |
| | | Malformed / Expired Token | | | | | | | O | | |
| | **ipAddress** | "10.0.0.1" | O | O | O | O | O | O | O | O | O |
| **Confirm** | **Return** | AuthResponseDto (Tokens + User) | O | O | O | | | | | | O |
| | | null | | | | O | O | O | O | O | |
| | **User Entity** | Existing Record Maintained | O | O | | O | O | O | | | O |
| | | New Student User Inserted | | | O | | | | | | |
| | | GoogleId Linked to User | | O | | | | | | | |
| | **Tokens** | Refresh Token Saved to DB | O | O | O | | | | | | O |
| | | Expired Tokens Cleaned Up | O | O | O | | | | | | O |
| | **Exception** | None | O | O | O | | | | | | O |
| | | ForbiddenException | | | | O | O | O | | | |
| | | UnauthorizedException | | | | | | | O | | |
| | | InvalidOperationException | | | | | | | | O | |
| | **Log message** | None | O | O | O | | | | | | O |
| | | "Your account has been disabled." | | | | O | O | O | | | |
| | | "Invalid Google ID Token." | | | | | | | O | | |
| | | "User persistence failed." | | | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AuthServiceGoogleLoginTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 114 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F02` – `AuthService.RefreshTokenAsync`

### 1. Header Information & Summary
| Code Module | Authentication & Account | Method | AuthService.RefreshTokenAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify token rotation mechanism, revoking old refresh tokens, validating token expiration/revocation, and enforcing user account status. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D25:M25, "P")`<br>**(10)** | `=COUNTIF(D25:M25, "F")`<br>**(0)** | `=COUNTIF(D25:M25, "")`<br>**(0)** | `=COUNTIF(D24:M24, "N")`<br>**(1)** | `=COUNTIF(D24:M24, "A")`<br>**(8)** | `=COUNTIF(D24:M24, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(10)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 | UTCID09 | UTCID10 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | User Exists & Status = "Active" | O | | | O | O | | | | | O |
| | | User Exists & Status = "Disabled" | | | | | | O | | | | |
| | | User Exists & Status = "Inactive" | | | | | | | O | | | |
| | | User Exists & Status = "Banned" | | | | | | | | O | | |
| | | User Record Not in DB | | | O | | | | | | O | |
| | **refreshToken** | Valid Active Token in DB | O | | | | | O | O | O | | |
| | | Null or Empty String | | O | | | | | | | | |
| | | Token Not Found in DB | | | O | | | | | | O | |
| | | Expired Token (ExpiresAt < Now) | | | | O | | | | | | |
| | | Revoked Token (IsRevoked = true) | | | | | O | | | | | |
| | | Exact Boundary (ExpiresAt == Now)| | | | | | | | | | O |
| | **ipAddress** | "10.0.0.1" | O | O | O | O | O | O | O | O | O | O |
| **Confirm** | **Return** | AuthResponseDto (Rotated Pair) | O | | | | | | | | | |
| | | null | | O | O | O | O | O | O | O | O | O |
| | **Token DB** | Old Token Marked Revoked | O | | | | | | | | | |
| | | New Refresh Token Appended | O | | | | | | | | | |
| | **Exception** | None | O | | | | | | | | | |
| | | UnauthorizedException | | O | O | O | O | | | | O | O |
| | | ForbiddenException | | | | | | O | O | O | | |
| | **Log message** | None | O | | | | | | | | | |
| | | "Invalid or expired refresh token." | | O | O | O | O | | | | O | O |
| | | "Your account has been disabled." | | | | | | O | O | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **A** | **A** | **A** | **A** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AuthServiceRefreshTokenTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10, Duration: 124 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F03` – `AuthService.RevokeTokenAsync`

### 1. Header Information & Summary
| Code Module | Authentication & Account | Method | AuthService.RevokeTokenAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify user logout token invalidation, ensuring target token is marked revoked and multi-token isolation is preserved. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:J22, "P")`<br>**(7)** | `=COUNTIF(D22:J22, "F")`<br>**(0)** | `=COUNTIF(D22:J22, "")`<br>**(0)** | `=COUNTIF(D21:J21, "N")`<br>**(2)** | `=COUNTIF(D21:J21, "A")`<br>**(4)** | `=COUNTIF(D21:J21, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | User Exists with 1 Active Token | O | | | | | | |
| | | User Exists with 3 Active Tokens | | O | | | | | |
| | | User Exists with Already Revoked Token | | | | | O | | |
| | | User Record Does Not Exist | | | | | | O | |
| | **refreshToken** | Valid Existing Active Token | O | O (1 target) | | | | | |
| | | Null or Empty String | | | O | | | | |
| | | Token Not Found in DB | | | | O | | | |
| | | Already Revoked Token | | | | | O | | |
| | | Token Belonging to Non-existent User | | | | | | O | |
| | | Valid Token with Leading/Trailing Spaces| | | | | | | | O |
| **Confirm** | **Return** | `true` | O | O | | | O | | O |
| | | `false` | | | O | O | | O | |
| | **Token DB** | Target Token IsRevoked = true | O | O | | | Unchanged | | O |
| | | Other User Tokens Unchanged | | O | | | | | |
| | **Exception** | None | O | O | O | O | O | O | O |
| | **Log message** | None | O | O | | | | | O |
| | | "Token is null or empty" | | | O | | | | |
| | | "Token not found" | | | | O | | | |
| | | "User not found" | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~AuthServiceLogoutTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 78 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F04` – `UserService.GetProfileAsync`

### 1. Header Information & Summary
| Code Module | Authentication & Account | Method | UserService.GetProfileAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify fetching user profile information by MongoDB ID, checking role permissions, status mapping, and handling missing IDs. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:K22, "P")`<br>**(8)** | `=COUNTIF(D22:K22, "F")`<br>**(0)** | `=COUNTIF(D22:K22, "")`<br>**(0)** | `=COUNTIF(D21:K21, "N")`<br>**(3)** | `=COUNTIF(D21:K21, "A")`<br>**(3)** | `=COUNTIF(D21:K21, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Precondition** | User Exists in DB (Role = Student) | O | | | | | | O | O |
| | | User Exists in DB (Role = Admin) | | O | | | | | | |
| | | User Exists in DB (Role = Teacher) | | | O | | | | | |
| | | User Does Not Exist in DB | | | | O | | O | | |
| | **userId** | Valid 24-char Hex ID ("507f1f77b...") | O | O | O | | | | O | |
| | | Non-existent ID String | | | | O | | | | |
| | | Null or Empty String | | | | | O | | | |
| | | Invalid ID Format ("invalid-hex") | | | | | | O | | |
| | | Valid ID with Null Bio & Avatar | | | | | | | O | |
| | | Valid ID with Whitespaces (" 507f... ") | | | | | | | | O |
| **Confirm** | **Return** | UserProfileDto Instance | O | O | O | | | | O | O |
| | | null | | | | O | O | O | | |
| | **Dto Mapping** | Role & Profile Fields Correct | O | O | O | | | | O (Defaults) | O (Trimmed) |
| | **Exception** | None | O | O | O | | | | O | O |
| | | NotFoundException | | | | O | | | | |
| | | BadRequestException | | | | | O | O | | |
| | **Log message** | None | O | O | O | | | | O | O |
| | | "User not found." | | | | O | | | | |
| | | "User ID is required." | | | | | O | | | |
| | | "Invalid user ID format." | | | | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **A** | **A** | **B** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~UserServiceGetProfileTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 89 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F05` – `UserController.GetUsers`

### 1. Header Information & Summary
| Code Module | Authentication & Account | Method | UserController.GetUsers |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative user listing endpoint, keyword search filtering, role/status filtering, and pagination boundary normalization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:J22, "P")`<br>**(7)** | `=COUNTIF(D22:J22, "F")`<br>**(0)** | `=COUNTIF(D22:J22, "")`<br>**(0)** | `=COUNTIF(D21:J21, "N")`<br>**(3)** | `=COUNTIF(D21:J21, "A")`<br>**(2)** | `=COUNTIF(D21:J21, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller Role** | Admin (Authorized) | O | O | O | O | O | O | O |
| | **search** | "student" | O | | | | | | |
| | | "nonexistent-user" | | | | | O | | |
| | | null | | O | O | O | | O | O |
| | **role** | null | O | | | O | O | O | O |
| | | "Student" | | O | | | | | |
| | | "Admin" | | | O | | | | |
| | **status** | "Active" | | O | | | | | |
| | | "Disabled" | | | O | | | | |
| | | null | O | | | O | O | O | O |
| | **page / limit** | Default (1 / 20) | O | | | | O | | O |
| | | Custom Valid (2 / 10) | | O | O | | | | |
| | | Invalid Lower Bound (0 / 0) | | | | O | | | |
| | | Upper Bound (1 / 500) | | | | | | O | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | O | O | O | O | |
| | | 500 InternalServerError | | | | | | | O |
| | **Items Count** | > 0 items | O | O | O | O | | O | |
| | | 0 items | | | | | O | | |
| | **Paging Meta** | Page=1, Limit=20 (Default) | O | | | O | O | | |
| | | Page=2, Limit=10 | | O | O | | | | |
| | | Limit clamped to 100 (Max) | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **B** | **A** | **B** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~UserControllerGetProfileTests&Name~GetUsers"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 82 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F06` – `UserController.GetUserById`

### 1. Header Information & Summary
| Code Module | Authentication & Account | Method | UserController.GetUserById |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify administrative single user detail endpoint, role response payload structure, 404 for missing IDs, and 400 for malformed parameters. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(2)** | `=COUNTIF(D19:I19, "A")`<br>**(3)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller Role** | Admin (Authorized) | O | O | O | O | O | O |
| | **userId** | Valid Existing Student ID | O | | | | | |
| | | Valid Existing Admin ID | | O | | | | |
| | | Non-existent User ID | | | O | | | |
| | | Null / Empty String | | | | O | | |
| | | Malformed Format ("abc-123") | | | | | O | |
| | | Valid ID (DB Failure Trigger) | | | | | | O |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | | |
| | | 404 NotFound | | | O | | | |
| | | 400 BadRequest | | | | O | O | |
| | | 500 InternalServerError | | | | | | O |
| | **Body Payload** | UserDto (Role = Student) | O | | | | | |
| | | UserDto (Role = Admin) | | O | | | | |
| | | ApiResponse.Fail | | | O | O | O | O |
| | **Message** | None | O | O | | | | |
| | | "User not found" | | | O | | | |
| | | "User ID is required" | | | | O | | |
| | | "Invalid ID format" | | | | | O | |
| | | "Database query failed" | | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~UserControllerGetProfileTests&Name~GetUserById"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 74 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F07` – `UserController.UpdateUserStatus`

### 1. Header Information & Summary
| Code Module | Authentication & Account | Method | UserController.UpdateUserStatus |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify moderating user account status (Active/Disabled), revoking all refresh tokens upon banning, preventing self-ban, and input casing normalization. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D22:J22, "P")`<br>**(7)** | `=COUNTIF(D22:J22, "F")`<br>**(0)** | `=COUNTIF(D22:J22, "")`<br>**(0)** | `=COUNTIF(D21:J21, "N")`<br>**(2)** | `=COUNTIF(D21:J21, "A")`<br>**(4)** | `=COUNTIF(D21:J21, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(7)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **Caller** | Admin ("admin-1") | O | O | O | O | O | O | O |
| | **Target User** | Existing Active Student | O | | | O | | O | O |
| | | Existing Disabled Student | | O | | | | | |
| | | Non-existent User ID | | | O | | | | |
| | | Caller Admin ("admin-1") | | | | | O | | |
| | **New Status** | "Disabled" | O | | O | | O | | O |
| | | "Active" | | O | | | | | |
| | | "UnknownStatus" (Invalid) | | | | O | | | |
| | | "disabled" (lowercase) | | | | | | O | |
| **Confirm** | **HTTP Status** | 200 OK | O | O | | | | O | |
| | | 404 NotFound | | | O | | | | |
| | | 400 BadRequest | | | | O | O | | |
| | | 500 InternalServerError | | | | | | | O |
| | **DB Status** | Updated to "Disabled" | O | | | | | O | |
| | | Updated to "Active" | | O | | | | | |
| | | Unchanged | | | | O | O | | O |
| | **Tokens** | All Refresh Tokens Cleared | O | | | | | O | |
| | | Refresh Tokens Retained | | O | | | | | |
| | **Message** | None | O | O | | | | O | |
| | | "User not found" | | | O | | | | |
| | | "Invalid status value" | | | | O | | | |
| | | "Cannot ban your own account" | | | | | O | | |
| | | "Database write failed" | | | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **B** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~UserControllerGetProfileTests&Name~UpdateUserStatus"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 76 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F08` – `WalletTopupService.GetConstraints`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | WalletTopupService.GetConstraints |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify retrieval of top-up constraint parameters (MinAmount, MaxAmount, StepAmount, Currency, supported payment providers) and fallback resilience. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:H20, "P")`<br>**(5)** | `=COUNTIF(D20:H20, "F")`<br>**(0)** | `=COUNTIF(D20:H20, "")`<br>**(0)** | `=COUNTIF(D19:H19, "N")`<br>**(3)** | `=COUNTIF(D19:H19, "A")`<br>**(1)** | `=COUNTIF(D19:H19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(5)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **App Config State** | Standard Defaults (10k - 5M) | O | O | | | |
| | | Multi-Provider Active Config | | | O | | |
| | | Custom Bounds Config (50k - 10M) | | | | O | |
| | | Missing / Corrupted Keys | | | | | O |
| **Confirm** | **Return** | TopupConstraintsDto Instance | O | O | O | O | O |
| | **MinAmount** | 10000 VND | O | O | O | | O (Fallback) |
| | | 50000 VND | | | | O | |
| | **MaxAmount** | 5000000 VND | O | O | O | | O (Fallback) |
| | | 10000000 VND | | | | O | |
| | **StepAmount** | 1000 VND | O | O | O | | O (Fallback) |
| | | 10000 VND | | | | O | |
| | **Currency** | "VND" | O | O | O | O | O |
| | **Providers** | ["PayOS"] | O | O | | O | O |
| | | ["PayOS", "VNPay"] | | | O | | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **B** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~WalletTopupServiceGetConstraintsTests"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 62 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F09` – `WalletTopupService.GetPackagesAsync`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | WalletTopupService.GetPackagesAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify active top-up package listing, ascending sort order by amount, bonus credit calculation, inactive tier filtering, and empty catalog handling. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D20:I20, "P")`<br>**(6)** | `=COUNTIF(D20:I20, "F")`<br>**(0)** | `=COUNTIF(D20:I20, "")`<br>**(0)** | `=COUNTIF(D19:I19, "N")`<br>**(3)** | `=COUNTIF(D19:I19, "A")`<br>**(2)** | `=COUNTIF(D19:I19, "B")`<br>**(1)** | `=SUM(A5:C5)`<br>**(6)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **DB Packages State** | 3 Active Tiers (50k, 100k, 200k) | O | | | | | |
| | | 5 Active Tiers with Bonus Credits | | O | | | | |
| | | 2 Active Tiers, 2 Inactive Tiers | | | O | | | |
| | | Empty Package Table (0 rows) | | | | O | | |
| | | Single Active Base Tier (No Bonus)| | | | | O | |
| | | DB Connection / Query Failure | | | | | | O |
| **Confirm** | **Return** | List<TopupPackageDto> | O | O | O | O | O | |
| | | null | | | | | | O |
| | **Items Count** | 3 | O | | | | | |
| | | 5 | | O | | | | |
| | | 2 (Active only) | | | O | | | |
| | | 0 (Empty List) | | | | O | | |
| | | 1 | | | | | O | |
| | **Ordering** | Ascending by Amount | O | O | O | | | |
| | **Bonus Credits** | Correctly Calculated | O | O | O | | O (0 Bonus) | |
| | **Exception** | None | O | O | O | O | O | |
| | | InvalidOperationException | | | | | | O |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **N** | **A** | **B** | **A** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~WalletTopupServiceCreateTopupTests&Name~GetPackages"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 71 ms - BE_UnitTests.dll (net8.0)
```

---

## Sheet `F10` – `WalletTopupService.CreateTopupAsync`

### 1. Header Information & Summary
| Code Module | AI Wallet & Payment | Method | WalletTopupService.CreateTopupAsync |
|---|---|---|---|
| **Created By** | Developer | **Executed By** | Tester |
| **Test requirement** | Verify student top-up order creation, custom amount vs package ID handling, constraint boundary validation (Min/Max/Step), and PayOS gateway URL generation. |

| Passed | Failed | Untested | N | A | B | Total Test Cases |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `=COUNTIF(D25:K25, "P")`<br>**(8)** | `=COUNTIF(D25:K25, "F")`<br>**(0)** | `=COUNTIF(D25:K25, "")`<br>**(0)** | `=COUNTIF(D24:K24, "N")`<br>**(2)** | `=COUNTIF(D24:K24, "A")`<br>**(4)** | `=COUNTIF(D24:K24, "B")`<br>**(2)** | `=SUM(A5:C5)`<br>**(8)** |

---

### 2. Decision Matrix Table
| Category | Parameter / Item | Value / Condition | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Condition** | **User Account** | Active Student User | O | O | O | O | O | | O | O |
| | | Disabled Student User | | | | | | O | | |
| | **Input Mode** | Custom Amount | O | | O | O | O | O | O | O |
| | | Package ID ("pkg-100k") | | O | | | | | | |
| | **Amount** | 50,000 VND | O | | | | | O | O | |
| | | Resolved from Package (100k) | | O | | | | | | |
| | | 5,000 VND (< Min 10,000 VND) | | | O | | | | | |
| | | 6,000,000 VND (> Max 5,000,000) | | | | O | | | | |
| | | 15,500 VND (Not multiple of step)| | | | | O | | | |
| | | 10,000 VND (Exact Min Boundary) | | | | | | | | O |
| | **PayOS API** | Gateway Returns Checkout URL | O | O | | | | | | O |
| | | Gateway Throws Exception | | | | | | | O | |
| **Confirm** | **Return** | CreateTopupResponseDto | O | O | | | | | | O |
| | | null | | | O | O | O | O | O | |
| | **Payment DB** | Record Created with Status PENDING| O | O | | | | | | O |
| | | Record Created with Status FAILED | | | | | | | O | |
| | **Exception** | None | O | O | | | | | | O |
| | | BadRequestException | | | O | O | O | | | |
| | | ForbiddenException | | | | | | O | | |
| | | PaymentGatewayException | | | | | | | O | |
| | **Log message** | None | O | O | | | | | | O |
| | | "Amount must be at least 10,000 VND." | | | O | | | | | |
| | | "Amount cannot exceed 5,000,000 VND." | | | | O | | | | |
| | | "Amount must be a multiple of 1,000 VND." | | | | | O | | | |
| | | "User account is disabled." | | | | | | O | | |
| | | "Failed to create PayOS payment link." | | | | | | | O | |
| **Result** | **Type (N/A/B)** | | **N** | **N** | **A** | **A** | **A** | **A** | **A** | **B** |
| | **Passed/Failed** | | **P** | **P** | **P** | **P** | **P** | **P** | **P** | **P** |
| | **Executed Date** | | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 | 2026-08-21 |
| | **Defect ID** | | | | | | | | |

---

### 3. Test Execution Command & Evidence Output

```powershell
dotnet test ".\BE_UnitTests\BE_UnitTests.csproj" --filter "FullyQualifiedName~WalletTopupServiceCreateTopupTests&Name~CreateTopup"
```

```text
Test run for D:\1_Tailieuhoctap REAL Reborn\Ky9 - Final\SEP - 3\APE_BE\BE_UnitTests\bin\Debug\net8.0\BE_UnitTests.dll (.NETCoreApp,Version=v8.0)
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 87 ms - BE_UnitTests.dll (net8.0)
```
