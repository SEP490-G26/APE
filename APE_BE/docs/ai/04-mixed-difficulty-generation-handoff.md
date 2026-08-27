# Mixed Difficulty Generation Handoff

Date: 2026-08-01

This document describes the backend extension for mixed-difficulty question generation.
It is intended for FE implementation and cross-team alignment.

## Update history

### 2026-08-01

Initial mixed-difficulty handoff:

- BE accepted `difficultyProfile` as the preferred request contract
- BE supported single-row and multi-row difficulty selection without creating a separate frontend mode
- mixed execution was defined as per-bucket generation/review with shared retrieval reuse

### 2026-08-03

This note was updated after backend stabilization work:

- request and resend payload guidance was aligned to the current BE contract
- runtime request limits were updated to match the active controller validation
- FE/PE limits were documented clearly to reduce FE/BE mismatch
- the note now also records the quality hardening that motivated these limits and payload changes

## 1. Goal

Allow one generation request to ask for a mix of difficulties such as:

- 2 Easy
- 3 Medium
- 1 Hard

without breaking the existing single-difficulty request contract.

The extension must remain:

- backward-compatible
- scope-grounded
- explainable when some buckets cannot be fulfilled

## 2. Backend contract direction

The external FE contract should be understood as `difficultyProfile` first.

### 2.1 Single bucket request

If FE sends one difficulty row only:

```json
{
  "difficultyProfile": [
    { "difficulty": "Medium", "count": 10 }
  ],
  "count": 10
}
```

Backend treats this as a normal single-difficulty generation run.

Important current interpretation:

- FE should still send `count`
- `count` is now the total requested question count for the whole request
- for single mode, `count` should equal the single bucket count
- `difficultyMode` may still be sent as `single`, but BE does not depend on it as the main source of truth
- the real source of truth for mixed-vs-single behavior is the normalized `difficultyProfile`

### 2.2 Multi-bucket request

If FE sends two or three difficulty rows:

```json
{
  "difficultyProfile": [
    { "difficulty": "Easy", "count": 8 },
    { "difficulty": "Medium", "count": 10 },
    { "difficulty": "Hard", "count": 6 }
  ],
  "count": 24
}
```

Backend treats this as mixed-difficulty generation internally.

Rules:

- `count` must equal the sum of `difficultyProfile[].count`
- duplicate difficulty entries are rejected
- more than 3 difficulty rows are rejected
- the existing request-level count validation still applies to the total count
- if `difficultyProfile` is omitted, backend still supports the legacy `difficulty + count` single request for backward compatibility

## 2.3 Current request payload shape

Current production routes:

- `POST /api/ai/questions/generate-review/system`
- `POST /api/ai/questions/generate-review/byos`

Current request shape should be understood as:

```json
{
  "documentId": "...",
  "courseId": "...",
  "subject": "JAVA_OOP",
  "chapterKeys": ["chapter-01", "chapter-02"],
  "difficultyMode": "mixed",
  "difficultyProfile": [
    { "difficulty": "Easy", "count": 10 },
    { "difficulty": "Medium", "count": 20 }
  ],
  "difficulty": "Medium",
  "questionType": "FE",
  "count": 30,
  "targetTopics": ["inheritance", "polymorphism"],
  "mode": "SameModelDualRole",
  "maxAttempts": 2,
  "persistQuestions": true,
  "isPublic": true,
  "retrievalQuery": null,
  "language": "java",
  "maxPackedTokens": 2400
}
```

Field interpretation:

- `chapterKeys` is the current preferred field for chapter scope
- `chapterKey` still exists as compatibility input, but FE should prefer `chapterKeys`
- `difficultyProfile` is the preferred field for difficulty selection
- `difficulty` remains as a compatibility field and single-bucket fallback
- `count` must always be the total requested count after FE sums all active difficulty rows
- `targetTopics` remains required
- `maxPackedTokens` is optional and defaults to `2400`

## 3. Backend execution strategy

The backend does not generate all mixed difficulties in one LLM prompt.

Current execution strategy is:

`resolve scope/context once -> split into difficulty buckets -> run generation/review per bucket -> merge results`

Important reason:

- prompt control is stronger when each bucket has one clear difficulty target
- review is easier to defend because each bucket is judged against its own target difficulty
- shortfall reporting becomes clearer when only some buckets fail

## 3.1 Question-quality hardening that landed with this flow

The mixed-difficulty contract was not only a UI or payload change.
It was updated together with several backend quality upgrades so that mixed requests would not amplify old quality problems.

Key quality upgrades already applied in BE:

- chunk payload sent to generator/reviewer was reduced and cleaned so prompts focus on higher-signal evidence
- FE explanation grounding was tightened so explanations must stay closer to cited chunk evidence
- thin-scope shortfall handling was strengthened so the system returns fewer grounded questions instead of fabricating enough count
- FE mixed/default attempts were reduced to protect latency and avoid wasteful retry loops on weak scopes
- PE generation guidance was hardened to reduce drift into invented requirements or unsupported implementation details
- repair flow was strengthened so malformed outputs are normalized back into the expected schema instead of leaking broken structure forward

Important interpretation for FE:

- mixed difficulty should not be explained as only "more rows"
- it is coupled with stricter backend quality control
- this is why some mixed requests may return partial fulfillment together with explicit shortfall reasoning

## 4. Retrieval reuse behavior

Mixed mode tries to avoid disturbing the stable retrieval flow.

Current behavior:

- if the source request is retrieval-based, backend resolves the source once first
- if a reusable context pack exists, bucket requests reuse that context pack
- otherwise bucket requests reuse the same resolved chunk set directly

This means FE should understand:

- mixed mode is mainly a generation/review orchestration extension
- it is not a new retrieval model

## 5. Request fields FE must send

The following request DTOs now support mixed difficulty:

- `POST /api/ai/questions/generate-review/system`
- `POST /api/ai/questions/generate-review/byos`
- admin `POST /api/ai/questions/generate-review`

Preferred field:

- `difficultyProfile`

Existing fields still required:

- chapter scope
- target topics
- subject
- question type
- count

Recommended current FE request-building rule:

1. build `chapterKeys` from selected chapter rows
2. build `difficultyProfile` from active difficulty rows
3. compute `count = sum(difficultyProfile[].count)`
4. send `difficultyMode = "single"` when one row exists, `difficultyMode = "mixed"` when two or three rows exist
5. still send a fallback `difficulty` value for compatibility, usually the first selected difficulty row

This means the resend or retry request after user edits scope/count should follow the same normalized shape and should not go back to the old `difficulty + count only` payload unless FE is intentionally calling legacy single mode.

## 6. FE selection model

Recommended FE interaction:

### 6.1 Difficulty rows

Use one row by default:

- difficulty selector
- count input
- add button

When user adds more rows:

- allow at most 3 rows total
- do not allow duplicate difficulty choices across rows
- show total requested count live

Recommended interaction:

- one row selected => behaves like single
- two or three rows selected => behaves like mixed

FE should:

- compute total count from the three inputs
- block submit when total count is 0
- keep the same total-count limit rules already enforced by BE

Recommended FE state shape:

```js
{
  difficultyProfile: [
    { difficulty: "Medium", count: 10 }
  ],
  count: 10
}
```

Note:

- FE may keep legacy `difficulty` state internally for migration, but request building should prefer `difficultyProfile`
- backend still accepts legacy single mode for compatibility, but new FE work should move to the row-based difficulty profile

## 7. Response fields FE should use

The result now includes:

- `difficultyMode`
- `difficultyProfile`
- `difficultyReports`

### 7.1 `difficultyProfile`

This reflects the normalized requested distribution.

Example:

```json
[
  { "difficulty": "Easy", "count": 2 },
  { "difficulty": "Medium", "count": 3 },
  { "difficulty": "Hard", "count": 1 }
]
```

### 7.2 `difficultyReports`

Each bucket report contains:

- `difficulty`
- `requestedCount`
- `generatedCount`
- `reviewStatus`
- `needsRevision`
- `persistedCount`
- `shortfallReport`

FE should render this as a per-difficulty breakdown.

## 8. Shortfall semantics in mixed mode

Mixed mode may partially succeed.

Example:

- Easy requested 2, generated 2
- Medium requested 3, generated 2
- Hard requested 1, generated 0

This is expected behavior when the scope is not strong enough for all requested buckets.

Important product rule:

- backend should not fabricate harder questions just to satisfy the count
- FE should explain partial fulfillment clearly

That means FE should show:

- overall shortfall summary
- per-bucket shortfall detail

## 9. Runtime limits and why they exist

Mixed difficulty must be stricter than current single-difficulty generation to avoid slow or timeout-prone runs.

Current backend limits:

- maximum difficulty buckets per request: `3`
- FE single total: `10..50`
- PE single total: `1..5`
- FE mixed total: `1..50`
- PE mixed total: `1..5`

Important interpretation:

- mixed difficulty is designed for a few targeted buckets in one run
- FE may distribute the mixed total unevenly across 2 or 3 difficulty rows as long as the request total stays within the backend limit
- PE may distribute the mixed total unevenly across 2 or 3 difficulty rows as long as the request total stays within the backend limit
- FE single still keeps the higher minimum because the feature is aimed at batch objective-question generation
- PE single and PE mixed remain intentionally tight to protect latency and review stability
- it is not intended to replace very large single-difficulty bulk generation
- these limits exist mainly to protect latency and generation stability

These limits also protect question quality.

Reason:

- larger mixed runs multiply generator/reviewer passes
- larger mixed runs increase the chance of thin-context buckets
- larger mixed runs increase latency and make retries more expensive
- tighter caps help keep generation in a range where grounding and review remain defensible

## 10. Current limitations

This backend extension is intentionally conservative.

Current limitations:

- each bucket is still billed/run separately inside the service
- retrieval is reused, but generation/review is not merged into one LLM call
- metrics are aggregated for presentation, not modeled as a brand-new mixed-run billing entity yet

This is acceptable for now because the main goal is:

- safe extension
- low regression risk
- clear FE contract

## 11. Recommended FE rollout

Recommended order:

1. add row-based difficulty selection in UI
2. build request payload from `difficultyProfile`
3. render `difficultyReports` in result panel
4. keep outer result shell unchanged
5. render per-difficulty detail as dropdown/accordion under the shared result shell
6. smoke-test both BYOS and SYSTEM with one mixed request each

## 12. Result-panel suggestion for FE

Recommended display structure:

- keep current outer result shell:
  - source scope
  - run summary
  - review
  - overall shortfall
  - context pack
  - metrics
- inside question/result detail:
  - group by difficulty
  - use dropdown/accordion items such as:
    - `Easy (8/8)`
    - `Medium (7/10)`
    - `Hard (0/6)`

This keeps the current result page stable while making mixed-difficulty output readable.

## 13. Demo interpretation

If FE is asked why mixed mode may return uneven results across buckets, the correct answer is:

- the system keeps scope-grounded generation
- each difficulty bucket must still be supported by evidence
- the system prefers partial grounded output over fabricated full output

If FE is asked what changed recently in BE around this feature, the short answer is:

- request shape was normalized around `difficultyProfile`
- count and bucket limits were tightened and documented
- backend quality control for grounding, shortfall handling, and FE explanation discipline was strengthened at the same time
