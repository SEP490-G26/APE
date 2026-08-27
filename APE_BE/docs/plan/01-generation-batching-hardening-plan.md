# Generation Batching Hardening Plan

## Status

Status: completed for the original batching-hardening scope.

Completed outcome:

- internal batching exists for large single-difficulty requests
- request-level chunk/context reuse is in place
- accept-and-fill orchestration exists and keeps accepted items across attempts
- parse-repair-rule-review-llm-review flow is split more safely per batch
- reviewer parse collapse no longer wipes the whole request as easily
- shortfall reporting and FE-visible partial-result semantics exist
- queue generic retrieval no longer drifts into specialized PE outputs and now fails safe when the retrieved pack is too thin

What was intentionally left for the next plan:

- stronger guarantee that scope-rich requests fulfill the full requested count before stopping
- semantic duplicate detection beyond lexical fingerprint/title checks
- further queue-core retrieval enrichment so generic queue scopes can pull richer core support instead of stopping on a thin pack
- latency/cost tuning after fulfillment and duplicate semantics are tightened

## Scope

This plan does **not** introduce FE `50` / request or PE `10` / request as new backend capabilities. Those limits already exist in the current BE request contract and validation flow.

The goal of this patch is to harden the existing generation pipeline so those upper bounds remain usable under real AI runtime behavior.

Primary targets:

- keep FE support up to `50` questions per request
- keep PE support up to `10` questions per request
- support both `single` and `mixed` difficulty requests
- prevent one bad large AI response from collapsing the whole request
- reduce:
  - parse failure
  - truncated output
  - timeout risk
  - full-request collapse
- keep groundedness and review strict enough for academic use

## Current Problem

The current pipeline is still vulnerable when one request asks for a large number of questions:

- one generation response may become too large or malformed
- one parse failure can wipe out the entire request result
- one review failure can reject a very large output set at once
- mixed-difficulty requests amplify prompt size and response size

This is a robustness problem in orchestration, not only a prompt problem.

## Design Direction

Keep the API contract as-is, but change the **internal execution model**:

- `1 external request`
- `1 request-level plan`
- `N internal sub-batches`
- `1 final aggregate result`

This keeps FE/BE contract stable while making runtime execution less fragile.

## Key Constraint: Cost Must Not Explode

Naive batching would increase cost badly.

Example risk:

- FE request = `50`
- mixed difficulty = `3 buckets`
- if split too aggressively and each batch also gets a full LLM review + repair
- total calls can explode

That is not acceptable.

So batching must be combined with **cost controls**:

### Cost Guard Rules

1. Reuse retrieval/context once per request.
   - Do not rebuild retrieval pack for every sub-batch.
   - Resolve context once, then reuse the same chunk pack unless a bucket is explicitly short-circuited.

2. Use moderate batch sizes, not tiny batch sizes.
   - FE should not become `50` single-question calls.
   - PE should not become `10` single-question calls by default.

3. Do not run full LLM review on every tiny fragment if rule review already proves a hard failure.
   - If parse fails or schema is clearly invalid, skip expensive LLM review for that batch.
   - If a batch is already unrecoverable, stop early.

4. Reserve repair for actual parse/shape failures only.
   - Do not run repair automatically for every rejected batch.

5. Apply LLM review selectively when possible.
   - Rule review always runs.
   - LLM review should run only when the batch is structurally valid and worth judging semantically.

6. Aggregate persistence at request end.
   - Do not persist per batch unless necessary.
   - This avoids repeated DB-side duplicate checks and partial side effects during generation.

## Planned Internal Batch Sizes

These are starting values, not final constants.

### FE

- single difficulty:
  - target `8-12` questions per batch
  - default start: `10`
- mixed difficulty:
  - split by difficulty bucket first
  - then chunk each bucket at max `10`

### PE

- single difficulty:
  - target `2-3` questions per batch
  - default start: `2`
- mixed difficulty:
  - split by difficulty bucket first
  - then chunk each bucket at max `2`

This keeps call count bounded without making each response too large.

## Execution Model

### Request Level

1. Normalize request
2. Validate FE/PE count limits and difficulty profile
3. Resolve retrieval/context pack once
4. Build execution plan
5. Execute planned sub-batches
6. Accumulate accepted questions and track missing slots
7. Regenerate only the missing slots while retry budget remains
8. Aggregate questions, review, shortfall, metrics, and persistence

### Batch Level

For each batch:

1. generate
2. parse
3. repair only if parse failed and repair is allowed
4. normalize questions
5. run rule review
6. run LLM review only if batch is still structurally valid
7. classify each candidate as accepted/rejected/duplicate/invalid
8. keep accepted items, discard rejected ones, and emit batch result with stop reason

## Failure Model

Each batch should end in one of these states:

- `accepted`
- `accepted_with_scope_warning`
- `parse_failed`
- `repair_failed`
- `review_rejected`
- `short_circuited_scope_thin`
- `duplicate_filtered`

The whole request should fail hard only when:

- input is invalid, or
- no batch produces any usable output

Otherwise return partial results and clear shortfall reasons.

## Accept-And-Fill Requirement

This is a required change, not an optional enhancement.

The request objective must become:

- `AcceptedQuestionCount == RequestedCount`

not:

- `one generated set happened to return RequestedCount items`

### Why this is needed

Current user-facing risk:

- generator returns `10`
- review rejects `5`
- final result may fall below `10`
- scope may still have enough evidence, but the request stops short

That creates the perception that the system is "eating" questions even when the source still has enough content.

### New request-level loop

For each request:

1. start with `remainingNeeded = requestedCount`
2. run a batch plan for `remainingNeeded`
3. keep only accepted questions
4. record rejected question fingerprints and reject reasons
5. reduce `remainingNeeded`
6. if `remainingNeeded > 0` and retry budget remains, generate only replacement questions for the missing slots
7. stop when:
   - `remainingNeeded == 0`, or
   - retry budget is exhausted, or
   - scope is truly too thin to continue safely

### Candidate-level status model

Each generated question should be classified into one of these states:

- `accepted`
- `rejected_quality`
- `rejected_grounding`
- `duplicate`
- `invalid_schema`
- `parse_lost`

This lets the pipeline keep good items instead of throwing away an entire partially-good set.

### Current implementation checkpoint

Current BE patch status:

- oversized single-difficulty requests are already routed through internal batching
- reviewer parse failure already falls back to rule review instead of collapsing a whole batch
- core single-difficulty execution now keeps accepted candidates across attempts and only refills the remaining slots
- request-level batching reuses the resolved chunk set instead of rebuilding retrieval per sub-batch

Latest live probe snapshot:

- file: `Inheritance.pdf`
- subject: `JAVA_OOP`
- type: `FE`
- difficulty: `Medium`
- requested: `12`
- result after current patch: `8/12` accepted

This means the full-request collapse behavior is materially reduced, but FE quality drift and repetition on narrow constructor/super scopes still need another tightening pass.

### Regeneration input for missing slots

When regenerating missing slots, the prompt/runtime state should include:

- accepted question fingerprints/shapes
- rejected question fingerprints/shapes
- reject reasons
- current missing count
- scope summary and remaining allowed target range

This reduces repeated regeneration of the same weak or duplicate shapes.

### Final request outcomes

The final result should clearly distinguish:

- `fulfilled`
- `fulfilled_with_warning`
- `partial_shortfall_scope_thin`
- `partial_shortfall_quality_rejection`
- `partial_shortfall_parse_instability`
- `failed_no_usable_output`

This is required for FE and user reporting.

## Dedup Strategy

Dedup must happen at three levels:

1. inside the same batch
2. across all batches in the same request
3. against stored DB questions in the same scope

The request aggregator should keep a `seenFingerprints` set across sub-batches.

It should also keep:

- `acceptedFingerprints`
- `rejectedFingerprints`

so replacement batches do not regenerate known-bad or already-accepted shapes.

## Reporting Changes

The final result should clearly distinguish:

- `parse failure`
- `repair failure`
- `review rejection`
- `scope shortfall`
- `partial acceptance`

Recommended additions to result payload:

- `RequestedCount`
- `GeneratedCount`
- `AcceptedCount`
- `BatchCount`
- `BatchReports[]`
- `FailureSummary`
- `StopReasons[]`

This is important for FE and for reviewer/debug visibility.

## Implementation Plan

### Phase 1 - Planner

- add internal batch plan model
- add batch-size policy for FE and PE
- normalize mixed difficulty into bucket plans

### Phase 2 - Batch Executor

- refactor current single-run logic into reusable batch execution
- add parse -> repair -> review sequence per batch
- stop expensive review when batch is already invalid
- emit candidate-level acceptance/rejection outcomes

### Phase 3 - Accept-And-Fill Aggregation

- merge accepted questions from all batches
- dedup cross-batch
- track remaining required count
- trigger targeted regeneration for missing slots
- aggregate review/metrics/shortfall
- keep final persistence at request level

### Phase 4 - Cost Controls

- skip LLM review for obviously invalid batches
- keep retrieval/context reuse at request level
- ensure repair is only attempted on parse failures
- cap batch count and batch size conservatively
- cap fill retries so quantity recovery does not explode call count

### Phase 5 - Regression

- FE single: `10`, `20`, `50`
- FE mixed: `2-3 buckets`, total up to `50`
- PE single: `3`, `5`, `10`
- PE mixed: total up to `10`
- thin-scope shortfall case
- forced parse-failure case
- duplicate cross-batch case
- partial accept case where initial `10` yields only `5-7` accepted and pipeline must refill the remainder

## Non-Goals For This Patch

- no FE API contract redesign
- no retrieval algorithm redesign
- no prompt artifact redesign as the first step
- no attempt to make AI output perfectly stable by prompt-only tightening

This patch is about execution resilience first.

## Recommended Patch Order

1. planner
2. internal batch-size policy
3. batch execution wrapper around current generation logic
4. aggregate result builder
5. accept-and-fill loop for missing slots
6. cross-batch dedup
7. clearer stop reasons and reporting
8. regression probes on real FE/PE cases

## Decision Note

The most important tradeoff is:

- fewer, larger calls reduce overhead but increase collapse risk
- more, smaller calls reduce collapse risk but increase cost

So the chosen design should be **bounded batching**, not micro-batching.

That means:

- FE large requests should usually land in around `4-6` generation batches, not `20-50`
- PE large requests should usually land in around `4-5` generation batches, not `10`
- LLM review should be skipped when a batch is already invalid by rule review or parse failure
- refill generation should only target missing slots, not regenerate the full request again

This is the cost-safe way to harden the pipeline.
