# Fulfillment And Semantic Dedup Plan

## Goal

Close the last two important gaps after batching hardening:

1. if the selected scope is rich enough, the backend should keep refilling until it reaches the requested count
2. duplicate prevention should move beyond lexical fingerprint/title matching and start blocking semantic near-duplicates

This plan does not redesign the external API contract.

## Why This Exists

Current behavior is better than before, but two risks still remain:

- a request can still stop short even when the retrieval scope likely has enough material
- duplicate checking is still mostly lexical, so semantically repeated questions can slip through with different wording

These are product-quality issues, not basic pipeline-stability issues.

## Target Semantics

### Fulfillment

- if scope is strong enough, requested count should be fulfilled
- if scope is genuinely thin, return the maximum safe count with explicit shortfall reasons
- do not silently stop at a lower count just because one attempt underperformed

### Dedup

Dedup should happen at three layers:

1. lexical duplicate
2. structural duplicate
3. semantic near-duplicate

The system should reject both:

- exact repeats
- reworded questions that test the same concept with the same effective task shape in the same scope

## Current Baseline

Already present:

- request-level accept-and-fill loop
- accepted/rejected fingerprint tracking
- title-based deconfliction
- fingerprint checks against DB in the same scope

Missing or weak:

- stronger fulfillment loop that keeps going while evidence remains strong
- semantic duplicate signal inside the same request
- semantic duplicate signal against persisted questions in the same scope
- clearer stop reason distinction between "scope thin" and "quality underperformed"

## Phase 1 - Stronger Fulfillment

### Required changes

- make refill continue while:
  - `accepted < requested`
  - retry budget remains
  - scope is not classified as truly thin
- distinguish these stop reasons clearly:
  - `fulfilled`
  - `partial_shortfall_scope_thin`
  - `partial_shortfall_quality_rejection`
  - `partial_shortfall_retry_budget`

### Required runtime state

- accepted question fingerprints
- rejected question fingerprints
- accepted structural signatures
- rejected structural signatures
- remaining needed count
- attempt-level quality summary

### Refill guidance should include

- accepted shapes already used
- rejected shapes to avoid
- uncovered concepts still worth targeting
- current remaining needed count

## Phase 2 - Structural Duplicate Layer

Before semantic embeddings, add a cheap structural layer:

- task pattern signature
- concept signature
- answer/explanation structure shape

Examples:

- repeated FE code-trace shape with only renamed identifiers
- repeated PE queue simulation shape with different story labels
- repeated abstract-class subclass override shape with cosmetic domain changes

This layer is cheaper than semantic similarity and should filter obvious paraphrases.

## Phase 3 - Semantic Duplicate Layer

### In-request semantic dedup

- generate normalized duplicate text per candidate
- embed that text once
- compare against accepted and rejected candidates in the same request
- reject when semantic similarity crosses threshold

### In-scope DB semantic dedup

- compare against a narrowed candidate pool only:
  - same subject
  - same question type
  - similar difficulty
  - overlapping chapter/topic scope
- do not scan all historical questions

### Threshold policy

Starting policy:

- `>= 0.95`: reject as semantic duplicate
- `0.88 - 0.95`: reject or demote as near-duplicate depending on scope richness
- `< 0.88`: allow to continue through normal quality review

Thresholds should be tuned by probe evidence, not guessed once and frozen.

## Phase 4 - Reporting

Expose duplicate and fulfillment reasons clearly for FE/debug:

- `duplicate_lexical`
- `duplicate_structural`
- `duplicate_semantic`
- `shortfall_scope_thin`
- `shortfall_quality_rejection`
- `shortfall_retry_budget`

This is important for mentor/demo questions and council review.

## Phase 5 - Regression

Run at least:

- FE strong-scope request where requested count should be fully reached
- FE strong-scope mixed difficulty request
- PE strong-scope request where refill is required to reach full count
- thin-scope request that should partial-stop with clean warning
- duplicate-heavy scope where semantic paraphrases would previously slip through

## Done Criteria

This plan is complete when:

- rich-scope requests reliably fulfill the requested count or stop with a justified explicit reason
- duplicate prevention is no longer only lexical
- semantic near-duplicates are blocked within-request and in-scope
- FE-visible shortfall reasons cleanly distinguish thin scope from underperforming generation quality

## Progress

### Completed in code

- all multi-question single-difficulty requests now go through request-level batch/refill orchestration instead of only oversized requests
- refill now continues until:
  - requested count is satisfied, or
  - repeated no-progress / thin-scope stop reasons are reached
- in-request duplicate control now has three active layers:
  - lexical fingerprint/title
  - structural signature + keyword overlap
  - semantic embedding similarity
- the core generation flow now also seeds duplicate history from a narrow same-scope DB pool:
  - same source scope
  - same owner for BYOS or same course for SYSTEM
  - same difficulty
  - overlapping requested topics when available
- final accepted responses no longer leak intermediate failed-batch review issues into the FE-facing final `review` payload
- PE medium/hard runs now use a more conservative execution profile:
  - batch size reduced to `1`
  - tighter generation/review max-token caps
  - slimmer retry feedback payload
  - previous-question serialization drops bulky PE code/test payload during refill guidance
- DSA hashing PE prompt routing is now stricter in batched runs:
  - hashing prompt filtering no longer depends only on retrieval topics
  - same-chapter anchor chunks are preferred even when the batched request is chunk-only
  - runtime guidance explicitly blocks drift into collision/fixed-table simulation when the cited chunk only supports folding/hash-index computation

### Verified

- FE probe on `Slot_02_03_04_Basic_Computation-variant.pptx` with `count=3` now refills to full count and returns a clean accepted final review instead of stopping dirty with historical attempt issues mixed into the final output
- FE probe on `Polymorphism.pdf` with `JAVA_OOP / FE / Medium / count=3 / topic=polymorphism` completed in a single batch with `3/3` accepted and no final review noise after the dedup changes
- mixed FE probe on `Polymorphism.pdf` with `difficultyProfile=Easy:1,Medium:1,Hard:1` still returns the expected scoped warning behavior: only the safe Easy bucket is produced and the final review explains why Medium/Hard were skipped
- the latest PE probe on `7-Hashing-converted.pptx` with `DSA_JAVA / PE / Medium / count=2 / topic=hashing` now reaches `2/2` with:
  - `BatchCount=2`
  - `RefillAttempts=0`
  - `AttemptsUsed=3`
  - total batched runtime around `49.9s`
  - one accepted batch already using `ChunkCount=1` prompt focus on the anchor hashing chapter

### Still open

- PE attempt-2 is reduced but not eliminated on narrow hashing-like scopes; one batch can still miss on attempt 1 because reviewer grounding remains strict around lecture-like folding tasks
- strong-scope fulfillment still needs more regression coverage on PE and mixed-difficulty runs
- semantic thresholds may still need tuning on narrow FE scopes where concept-near questions can remain too similar even after the new structural filter
- PE latency is improved but still above the ideal target on harder or narrower scopes because a single remaining attempt-2 still dominates end-to-end time
