# FE Handoff After AI Flow Change

Date: 2026-07-30

This document is the FE-facing handoff note for the latest backend AI changes.
It is written as an implementation guide, not just a high-level summary.

The goal is:

- tell FE exactly which screens are affected
- explain which BE behaviors changed
- show which request/response fields FE must update
- reduce guesswork when FE refactors the AI-related UI

## 1. Executive summary

The most important backend changes FE must understand are:

1. generation flow is now **scope-driven**
2. backend supports **multi-chapter selection** through `chapterKeys`
3. retrieval and context pack data are now important UI evidence, not just hidden debug detail
4. mentor flow is still `PE-only`, but the backend has been prepared for future execution-evidence input

Old FE mental model:

`single chapter -> pick topics from that chapter -> generate`

New FE mental model:

`document -> chapter scope -> topic refinement -> retrieval -> context pack -> generation/review`

## 2. Screens that must be reviewed

The following FE areas are the most affected by the BE changes:

### 2.1 System question generation screen

Purpose:

- generate from system-approved course/document data

Main impact:

- chapter selection must move from single value to multi-value scope
- topic summary must be able to load against multiple chapter keys
- result UI should expose retrieval and review evidence more clearly

### 2.2 BYOS question generation screen

Purpose:

- generate from the student's own uploaded document

Main impact:

- same multi-chapter scope change as system flow
- FE must not assume chapter summary and topic summary are single-chapter only
- ownership-sensitive BYOS flow still uses different endpoints from system flow

### 2.3 Generation result panel

Purpose:

- render generation output, review, persistence, metrics, and retrieval context

Main impact:

- panel must stop treating retrieval/context data as optional noise
- review outcome and context pack quality indicators should be visible in the main hierarchy
- panel must surface generation shortfall clearly when BE stops early to preserve groundedness

### 2.4 Student submission / mentor result screen

Purpose:

- render mentor output for PE submissions

Main impact:

- keep mentor as `PE-only`
- prepare UI for two modes:
  - static review only
  - evidence-aware feedback later when Judge0-backed data is integrated

### 2.5 Admin smoke / AI demo screens

Purpose:

- smoke AI flows for admin/debug/demo

Main impact:

- fixture/demo semantics must be kept separate from production semantics
- mentor smoke wording and structure should not imply full Judge0-backed behavior yet

## 3. Backend behavior changes FE must follow

## 3.1 Chapter selection is no longer single-chapter-first

Backend now resolves requested chapter scope using:

- legacy `chapterKey`
- new `chapterKeys`

But FE should treat:

- `chapterKeys` as the real current direction
- `chapterKey` as compatibility support only

Validation enforced by backend:

- at least one requested chapter is required
- more than 3 chapter keys is rejected

FE implication:

- use multi-select chapter state
- keep a hard cap of 3 chapters visible in UI
- do not build new UI around one `selectedChapterKey`

## 3.2 Topic loading is now scope-based

Instead of asking "what topics are in this exact one chapter?", FE should think:

- "what topics are available inside the selected chapter scope?"

That means topic selection becomes:

- a refinement layer inside the selected scope
- not the only way FE or users reason about grounding

## 3.3 Retrieval/context pack is part of output quality

The BE now leans more heavily on retrieval/context pack quality.

That means FE should surface:

- what scope was selected
- what chunks or evidence were used
- what topics were actually covered
- whether the result was persisted

This is important because if the output is weak, the issue may be retrieval quality, not only prompt wording.

## 4. API changes and expectations

## 4.1 System generation flow

### Topic summary endpoint

Use:

- `GET /api/ai/courses/{courseId}/documents/{documentId}/chapter-topics?chapterKeys=...`

Expected FE behavior:

- send multiple `chapterKeys`
- use this endpoint when user selected 1..3 chapters
- treat response as aggregated scope summary, not one chapter snapshot

### Generation endpoint

Use:

- `POST /api/ai/questions/generate-review/system`

Important request fields:

- `courseId`
- `documentId`
- `chapterKeys`
- `subject`
- `questionType`
- `difficulty`
- `count`
- `targetTopics`
- optional `retrievalQuery`
- optional `language`
- optional `maxPackedTokens`

Legacy compatibility note:

- BE still resolves `chapterKey` if present
- FE should not rely on that as the main UI contract

## 4.2 BYOS generation flow

### Topic summary endpoint

Use:

- `GET /api/student/documents/{id}/chapter-topics?chapterKeys=...`

Expected FE behavior:

- same multi-chapter scope behavior as system flow
- same aggregated topic interpretation

### Generation endpoint

Use:

- `POST /api/ai/questions/generate-review/byos`

Important request fields:

- `documentId`
- `chapterKeys`
- `subject`
- `questionType`
- `difficulty`
- `count`
- `targetTopics`
- optional `retrievalQuery`
- optional `language`
- optional `maxPackedTokens`

## 4.3 Chapter summary endpoints

These are still useful for chapter picker UI:

- system chapter summary comes from AI retrieval endpoints
- BYOS chapter summary comes from student document endpoints

Expected FE usage:

- load chapters first
- let user build chapter scope
- then load aggregated topic summary using `chapterKeys`

## 5. Request field changes FE must make

## 5.1 Replace single chapter state

Old FE-style state:

- `selectedChapterKey`

New FE-style state:

- `selectedChapterKeys`

Recommended shape:

```js
{
  selectedChapterKeys: [],
  selectedTopics: [],
  scopeSummary: null
}
```

## 5.2 Update generation payload building

Old assumption:

- payload contains one `chapterKey`

New requirement:

- payload should send `chapterKeys`

FE should still be careful to:

- trim empty keys
- deduplicate keys on the client side if possible
- block submit if no chapter is selected
- block submit if more than 3 chapters are selected

## 5.3 Update topic summary request building

FE must build query params as:

- repeated `chapterKeys`
- or the backend-compatible array style produced by the HTTP client

The important point is:

- the request must reach BE as multiple chapter keys, not a single concatenated fake value in app state

## 6. Response fields FE should surface more clearly

The exact DTO structure may continue evolving, but FE should prioritize these groups in the response:

## 6.1 Review group

Show prominently:

- review status
- score
- attempts used
- whether revision was needed

## 6.2 Persistence group

Show clearly:

- persisted or not
- created FE/PE question ids when relevant

## 6.3 Context/retrieval group

Show clearly:

- context pack summary
- selected or retrieved chunk count
- covered topics
- uncovered topics when available
- dominant chapter or scope signal when available

## 6.4 Shortfall/capacity group

Show clearly when present:

- requested count
- generated count
- stop reason
- coverage ratio
- packed token count
- dominant chapter
- covered topics
- uncovered topics
- notes
- suggested actions

Important FE rule:

- do not invent your own explanation when generation returns fewer questions
- treat backend `shortfallReport` as the source of truth for this explanation block
- FE should focus on readable presentation, not recomputing quality semantics

## 6.5 Metrics group

Keep lower emphasis but still available:

- provider/model
- token usage
- runtime metrics

## 7. Recommended UI changes by screen

## 7.1 System generation page

FE should change:

- chapter picker: single-select -> multi-select
- topic fetch: one chapter -> chapter scope
- request builder: `chapterKey` -> `chapterKeys`
- result panel: generic output -> review + context + retrieval evidence

UI notes:

- show selected chapter count
- show selected topic count
- show current question type and difficulty together with scope summary

## 7.2 BYOS generation page

FE should change:

- same chapter scope behavior as system page
- BYOS ownership and document-specific UX should remain distinct
- avoid pretending BYOS generation is just a clone of system flow

UI notes:

- show selected document identity clearly
- show scope summary before submit

## 7.3 Generation result panel

Recommended visual hierarchy:

1. shortfall/capacity warning when present
2. review result
3. context/retrieval evidence
4. generated questions
5. metrics / runtime details

This hierarchy better matches how the backend now explains quality.

Current FE implementation note:

- result panel now renders a dedicated shortfall/capacity block with summary, requested vs generated counts, stop reason, coverage, token count, notes, and suggested actions
- this block is intended to explain why fewer questions were returned in a defensible way for demo, mentor review, and thesis defense

## 7.4 Mentor / submission page

Recommended hierarchy:

1. verdict and short summary
2. execution evidence or fallback note
3. fixes to try next

Important behavior:

- if compile error exists, FE should prioritize it
- if visible failed cases exist later, FE should show them before generic advice
- if mentor output is static-review only, FE should not imply hidden execution certainty

## 7.5 Smoke/admin demo pages

FE should make it visually obvious whether a screen is:

- fixture-backed
- hybrid
- real API-backed

This is especially important for:

- admin smoke generation
- admin smoke mentor

## 8. BE semantics FE must not misrepresent

FE should not present the system as if:

- generation is still single-chapter-first
- mentor is already fully Judge0-backed
- retrieval/context data is only debug noise
- local file AI rules are the runtime truth

The correct interpretation is:

- runtime AI behavior is DB-first
- retrieval is scope-driven
- mentor is prepared for evidence-aware mode but not fully validated end-to-end yet

## 9. Concrete FE checklist

### Required

1. replace `selectedChapterKey`-only logic with `selectedChapterKeys`
2. update topic summary calls to multi-chapter endpoints
3. update system generation payload to send `chapterKeys`
4. update BYOS generation payload to send `chapterKeys`
5. block generation when chapter scope is empty
6. block generation when chapter scope exceeds 3 chapters
7. update result panel to surface review + retrieval/context info
8. update result panel to surface backend `shortfallReport` when present
9. keep mentor UI ready for future evidence-aware output
10. mark smoke/demo semantics clearly

### Strongly recommended

1. add a compact scope summary card before submit
2. show topic coverage data when available
3. keep metrics visible but secondary
4. group mentor suggestions as actionable next steps

## 10. Done definition for FE

FE can be considered aligned with the current BE AI flow when:

1. both `SYSTEM` and `BYOS` generation flows work with `chapterKeys`
2. topic selection works as scope refinement, not single-chapter truth
3. generation result UI exposes retrieval/context evidence clearly
4. generation result UI explains backend shortfall decisions clearly instead of hiding under-delivery
5. mentor page reflects current backend semantics without overclaiming Judge0 integration
6. smoke/demo screens are no longer confused with production runtime behavior
