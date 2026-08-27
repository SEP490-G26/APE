# FE Handoff: PE Execution Feedback Contract

## Affected Endpoints

- `POST /api/student/submissions/pe/run`
- `POST /api/student/submissions/pe`
- `GET /api/student/submissions/pe/{id}`

## Request Rules

### Run Code

Use `POST /api/student/submissions/pe/run`.

- When the user wants sample/public testcase execution, call the endpoint **once without `stdin`**.
- When the user wants a genuine custom run, pass `stdin` and the backend will execute **exactly one custom case**.
- Do not calculate verdicts independently in React.

Request shape:

```json
{
  "questionId": "string",
  "stdin": "optional custom input",
  "files": [
    {
      "filename": "Main.java",
      "content": "class Main { ... }"
    }
  ]
}
```

### Submit Code

Use `POST /api/student/submissions/pe`.

- This still creates an asynchronous persisted submission.
- The worker still grades all configured testcases.

## Response Additions

The backend keeps existing fields for backward compatibility and adds new fields.

### New Enum Values

`executionStatus`

- `Passed`
- `Failed`
- `NotExecuted`
- `Completed`

`diagnostics[].category`

- `Compilation`
- `Runtime`
- `ResourceLimit`
- `System`

`diagnostics[].severity`

- `Error`

## Run Code Response Contract

Existing fields remain:

- `status`
- `stdout`
- `stderr`
- `compileOutput`
- `runtimeMs`
- `memoryKb`
- `passedSampleCases`
- `totalSampleCases`
- `sampleResults`
- `customRunResult`

New additive fields:

- `executionStatus`
- `isJudged`
- `diagnostics`
- `sampleResults[].number`
- `sampleResults[].executionStatus`
- `sampleResults[].isJudged`
- `sampleResults[].isHidden`
- `sampleResults[].diagnostics`
- `customRunResult.number`
- `customRunResult.executionStatus`
- `customRunResult.isJudged`
- `customRunResult.isHidden`
- `customRunResult.diagnostics`

## Persisted Submission Detail Contract

Existing fields remain:

- `id`
- `questionId`
- `sessionId`
- `courseId`
- `mode`
- `languageId`
- `maxScore`
- `questionScore`
- `status`
- `finalVerdict`
- `testCasesPassed`
- `totalTestCases`
- `runtimeMs`
- `memoryKb`
- `attemptCount`
- `submittedAt`
- `processingStartedAt`
- `completedAt`
- `processingError`
- `submittedCode`
- `testResults`

New additive fields:

- `diagnostics`
- `testResults[].number`
- `testResults[].label`
- `testResults[].executionStatus`
- `testResults[].isJudged`
- `testResults[].diagnostics`

## Visibility Rules

### Public testcase

May expose:

- input
- expected output
- actual output
- sanitized stderr
- sanitized compile output
- structured diagnostics

### Hidden testcase

May expose only:

- generic label
- `number`
- `isHidden`
- `verdict`
- `executionStatus`
- safe structured diagnostics

Must not expose:

- input
- expected output
- actual output
- stdout
- stderr
- per-case compile output
- raw diagnostic details

## Custom Run Behavior

Custom runs are not judged against expected output.

- `isJudged = false`
- successful custom execution uses `executionStatus = Completed`
- runtime/resource failures use `executionStatus = Failed`
- compilation failures use `executionStatus = NotExecuted`

Do not render a testcase pass/fail checkmark when `isJudged` is `false`.

## JSON Examples

### Accepted Public Case

```json
{
  "status": "1/1 sample cases passed",
  "executionStatus": "Passed",
  "isJudged": true,
  "sampleResults": [
    {
      "index": 1,
      "number": 1,
      "label": "Sample 1",
      "status": "Accepted",
      "passed": true,
      "executionStatus": "Passed",
      "isJudged": true,
      "isHidden": false,
      "input": "1",
      "expectedOutput": "2",
      "actualOutput": "2",
      "diagnostics": []
    }
  ]
}
```

### Wrong Answer Public Case

```json
{
  "status": "0/1 sample cases passed",
  "executionStatus": "Failed",
  "isJudged": true,
  "sampleResults": [
    {
      "status": "Wrong Answer",
      "executionStatus": "Failed",
      "isJudged": true
    }
  ]
}
```

### Compilation Error

```json
{
  "status": "Compilation Error",
  "executionStatus": "NotExecuted",
  "isJudged": true,
  "diagnostics": [
    {
      "category": "Compilation",
      "code": "MISSING_SEMICOLON",
      "severity": "Error",
      "title": "A semicolon is missing",
      "message": "A statement appears to be missing a semicolon.",
      "filename": "Main.java",
      "line": 3,
      "column": null
    }
  ],
  "sampleResults": [
    {
      "status": "Compilation Error",
      "executionStatus": "NotExecuted",
      "isJudged": true
    }
  ]
}
```

### Runtime Error

```json
{
  "status": "0/1 sample cases passed",
  "executionStatus": "Failed",
  "sampleResults": [
    {
      "status": "Runtime Error",
      "executionStatus": "Failed",
      "isJudged": true,
      "diagnostics": [
        {
          "category": "Runtime",
          "code": "NULL_REFERENCE",
          "severity": "Error",
          "title": "A null value was used like an object",
          "message": "The program tried to use an object reference that was null.",
          "filename": "Main.java",
          "line": 10
        }
      ]
    }
  ]
}
```

### Time Limit Exceeded

```json
{
  "sampleResults": [
    {
      "status": "Time Limit Exceeded",
      "executionStatus": "Failed",
      "diagnostics": [
        {
          "category": "ResourceLimit",
          "code": "TIME_LIMIT_EXCEEDED",
          "severity": "Error"
        }
      ]
    }
  ]
}
```

### System Error

```json
{
  "finalVerdict": "SystemError",
  "diagnostics": [
    {
      "category": "System",
      "code": "SYSTEM_ERROR",
      "severity": "Error",
      "title": "The execution platform reported a system error",
      "message": "The submission could not be fully processed because of a platform error."
    }
  ],
  "testResults": [
    {
      "label": "Hidden test case 2",
      "executionStatus": "NotExecuted",
      "isJudged": true,
      "isHidden": true
    }
  ]
}
```

## UI Rendering Recommendations

- Prefer `executionStatus` for card state rendering.
- Use `status` as a display string for backward-compatible text only.
- Use `isJudged` to decide whether a pass/fail badge should be shown.
- Render `diagnostics` as the primary beginner-friendly explanation block.
- For hidden tests, never attempt to reconstruct missing details from other fields.
- For compilation failure, render top-level diagnostics once and show testcase cards as `NotExecuted`.

## Backward Compatibility Notes

- Existing routes are unchanged.
- Existing fields are unchanged.
- New fields are additive.
- The old frontend can keep reading legacy fields during migration.
- The recommended frontend migration path is to render testcase cards from backend `sampleResults`, `customRunResult`, and persisted `testResults` instead of recalculating verdicts in React.
