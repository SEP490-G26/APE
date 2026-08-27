# Current AI Backend State

Date: 2026-07-30

This document is the current-state summary for the AI part of `APE_BE`.
It is meant to replace scattered phase notes for normal day-to-day work.

## 1. Main direction

The current backend direction is:

- DB-first AI runtime
- scope-driven retrieval
- stronger academic quality control for both `FE` and `PE`
- fewer hard-coded heuristics in services

Important interpretation:

- local artifact JSON is edit/reference input before reseed
- active DB artifacts are the runtime truth
- test materials are used to improve the system, not to define the final academic standard

## 2. Core AI flows currently in place

### 2.1 Ingestion

Current ingestion flow:

`preview extract -> gatekeeper -> full extract -> normalize -> vision reinjection phase 1 -> chapter-aware chunking -> embedding/tagging`

What is already in place:

- duplicate guard on file and normalized content
- gatekeeper on preview text
- normalized content pipeline
- knowledge chunk creation
- chapter-aware metadata
- embedding and topic tagging

Current runtime note:

- `ExtractedStructure` should now be understood as DB-routed through the active `Extraction` agent, not as a separate hard-coded DeepSeek-only step
- chapter detection uses the same DB-first routing truth as the rest of extraction runtime
- a heuristic structure-prep view now runs before chapter detection AI so the model sees high-signal structure lines instead of raw noisy body text

### 2.2 Retrieval and context pack

Current retrieval should be understood as:

`document -> chapter scope -> topic refinement -> retrieval -> context pack`

Important current-state notes:

- retrieval is no longer modeled as a strict single-chapter truth flow
- backend supports chapter scope via `chapterKeys`
- context pack is part of quality control, not just debug output
- recent hardening reduced weak support chunks like generic textbook content

### 2.3 Question generation and review

Current production routes:

- `POST /api/ai/questions/generate-review/system`
- `POST /api/ai/questions/generate-review/byos`

Current quality direction:

- better grounding to selected chunks
- less FE/PE drift
- less leakage of internal implementation detail into learner-facing requirements
- retrieval evidence matters as much as prompt quality

### 2.4 Code mentor

Current mentor status:

- `PE-only`
- structured feedback is stored in DB
- prompt/policy/rubric were updated to accept `judge0_evidence_json`
- full end-to-end Judge0-backed validation is still deferred

This means:

- mentor is ready for evidence-aware behavior at rule level
- mentor is not yet a fully validated Judge0-driven final grading engine

## 3. What was recently fixed or improved

Recent AI-core work improved these areas:

- retrieval pack is tighter and less likely to pull irrelevant support chunks
- generation/review quality is more stable for FE and PE
- PE schema normalization was tightened
- mentor artifacts were upgraded for future Judge0 evidence
- legacy `PE_Submission` data is now more tolerant when old DB shapes are read
- chapter detection no longer falls back to the old unstable routing path and was validated again through real BYOS ingestion runs across C, DSA, and Java OOP
- upload billing for BYOS ingestion was rechecked end-to-end and confirmed both in API response fields and persisted VND billing transactions

## 3.1 Current runtime explainability and performance notes

These notes are important for the current FE/BE integration:

- BE now emits stage-timing logs for document upload, extracted-content normalization, embedding/tagging, and question generation/review
- the current timing logs are meant for backend diagnosis only; FE should not keep its own temporary console timing probes
- question generation now returns an explicit `shortfallReport` when the selected scope cannot safely support the requested count

Important interpretation of `shortfallReport`:

- this is not a frontend-generated warning
- this is a backend quality-control output derived from retrieval coverage, packed token budget, uncovered topics, and review outcome
- it exists so the system can stop early instead of fabricating extra questions

What `shortfallReport` currently tells the client:

- requested question count
- generated grounded count
- stop reason
- coverage ratio
- packed token count
- dominant chapter
- covered topics
- uncovered topics
- notes explaining why the system stopped early
- suggested actions for the next retry

This behavior is now part of the intended product semantics for both `SYSTEM` and `BYOS` generation.

## 4. DB-first rule

Operational rule for this backend:

1. edit local artifact reference
2. reseed DB
3. runtime uses active DB version

Do not assume a local JSON change is live until DB reseed and audit confirm it.

## 5. Current stop line

The AI core is in a practical stabilization phase.

The main open item intentionally deferred for now is:

- real mentor validation on Judge0-backed evidence after Judge0-side design and merge are ready

The next improvements should be incremental and measured:

- optimize performance bottlenecks by stage, not by rewriting the whole flow
- tighten difficult retrieval scopes without replacing the current stable scope-driven design
- continue moving FE/PE academic heuristics from code to DB artifacts in small batches with fallback safety
