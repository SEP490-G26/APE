# Unit Test Workbook Editing Playbook

## Purpose

This note captures the agreed working rules for editing the school workbook:

- `APE_BE/BE_UnitTests/UnitTest_Workbook/Report5.1_Unit Test (1).xlsx`

Use this when resuming workbook cleanup so the next pass does not need the full verbal briefing again.

## Current workbook strategy

- Treat `Report5.1_Unit Test (1).xlsx` as the active workbook.
- Preserve the school template style as the baseline.
- Do not rebuild the workbook from scratch.
- Make targeted edits sheet-by-sheet.
- Prefer fixing 10-20 function sheets per batch, then verify before moving on.

## Source of truth

When sheet content is unclear:

1. Read the real backend code first.
2. Map workbook function `Fxx` to the real service/controller method.
3. Update the workbook wording to match current code behavior.
4. Do not write generic test wording that is not supported by code.

Useful references:

- `Function` sheet inside the workbook
- corresponding backend files under `APE_BE/Application/Services`
- corresponding controllers under `APE_BE/API/Controllers`

## Required formatting rules

These are user-confirmed requirements and should be preserved:

- keep school workbook style/template
- content wording should be normal sentence case or lowercase-style prose, not SHOUTING
- avoid duplicate section labels
- avoid placeholder rows like `3`, `4`, `5`, `6`
- avoid generic filler like:
  - `all direct unit tests passed for the function under current module regression`
  - `coverage includes normal, abnormal, and boundary behavior where meaningful`
- executed date format should stay like `8/4/2026`
- executed by should be `MinhPQ` where relevant
- text must not be visually cut off
- blank spacing between content blocks should be small; if rows are empty and truly unnecessary, delete them

## Golden sheet structure

Each function sheet should follow the same logical shape as the cleaned early sheets:

- `Precondition`
- `Input`
- `Return`
- `Exception`
- `Result`

The descriptive content belongs in the left content area, not scattered into the case columns.

Case columns should primarily contain:

- case id
- `O` markers mapping each case to content rows
- type (`N`, `A`, `B`)
- passed/failed
- executed date
- defect id

## Blank row policy

Important: do not merely shrink useless blank rows.

Preferred behavior:

- delete truly empty rows in the content area
- keep only minimal separation between blocks
- preserve rows that contain real content, `O` markers, formulas, or summary metadata

Safe practice:

- after deleting rows, immediately verify and refresh summary formulas
- never mass-delete rows below the summary block blindly

## Summary block rules

Each sheet should end with a clean summary block:

- `Type(N : Normal, A : Abnormal, B : Boundary)`
- `Passed/Failed`
- `Executed Date`
- `Defect ID`

Requirements:

- no duplicate `Result` labels leaking into the summary block
- no overwritten summary labels
- no stale formulas
- no `#REF!`

After row deletions, refresh at least:

- `A7`
- `C7`
- `F7`
- `L7`
- `M7`
- `N7`
- `O7`

based on the current case-column range and the current summary rows.

## Content writing rules

When replacing placeholder content, write rows that reflect the real function behavior:

### Precondition

Describe what must already be true before the method/action runs.

Examples:

- authenticated student context is present
- repository/provider dependencies are available
- target entity id is supplied

### Input

Describe what the method/action receives.

Examples:

- user id plus top-up request are supplied
- page, limit, keyword, and status filter are supplied
- authenticated `NameIdentifier` and route parameter are supplied

### Return

Describe the direct success output contract.

Examples:

- returns `ApiResponse.Ok(...)`
- returns DTO with key fields
- completes without payload

### Exception

Describe real failure behavior from code.

Examples:

- throws `InvalidOperationException`
- throws `KeyNotFoundException`
- maps service `ArgumentException` to HTTP 400
- returns 401 or 404 in controller flow

### Result

Describe the stable behavioral guarantee or contract preserved by that function.

Examples:

- ownership is enforced
- wrapped response contract is preserved
- active-only visibility is preserved
- billing never deducts below zero

## What to avoid

- do not invent requirements not present in code
- do not write test prose copied from another unrelated function
- do not leave duplicated labels like two `Input`, two `Return`, or two `Exception`
- do not leave merged-area residue that visually looks like duplicate blocks
- do not overwrite merged cells blindly; check whether a cell is a merged anchor first

## Safe editing workflow

For each batch:

1. identify target sheets
2. inspect their current structure
3. map each `Fxx` to real code
4. clean duplicate labels
5. delete truly empty rows in content area
6. refresh row heights for long text
7. replace placeholder/generic content with real function wording
8. restore summary labels if they drifted
9. refresh formulas
10. verify:
   - no `#REF!`
   - no numeric placeholders
   - no generic filler
   - no duplicate labels
   - no clipped text

## Batch history already established

The following pattern has already been used successfully:

- `F1-F10`: cleaned to a stronger standard
- `F11-F30`: blank rows deleted, duplicate labels reduced, many placeholder rows replaced with code-based wording

When resuming, inspect the next batch first instead of assuming it matches the previous one exactly.

## Practical caution

Workbook editing can easily drift if rows are deleted near merged areas or summary blocks.

Before finalizing any batch, explicitly check:

- top case-id row still lines up
- `O` markers still align with intended rows
- summary block still starts at the intended row
- `Passed/Failed`, `Executed Date`, and `Defect ID` labels are still present

## Resume instruction

When continuing this work in a later session:

- read this note first
- inspect the active workbook state second
- continue in small batches
- prioritize correctness of structure over speed
- prefer code-backed wording over generic testing prose
