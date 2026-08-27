# Active DB Artifacts And Runtime Truth

Date: 2026-08-03

This note exists to prevent FE/BE drift about which AI rule versions are actually live.

## Update history

### 2026-08-03

This note was added after direct DB verification work.

It records:

- the DB-only runtime rule for AI artifacts
- the currently active prompt/policy versions observed in DB
- the distinction between local reference files and active runtime artifacts
- the recent question-quality hardening context so version labels are not read without behavioral meaning

## 1. Runtime truth

For `APE_BE`, the runtime source of truth is:

- active records in MongoDB collection `AI_Rule_Artifacts`

Operational interpretation:

- local JSON files are edit/reference material
- local JSON files are also seed input
- local JSON files are not runtime truth by themselves
- a change is only live after reseed to DB and active-version verification

Short rule:

1. edit local artifact reference
2. reseed DB
3. verify active DB versions
4. only then treat the change as live

## 2. Current active versions checked in DB

Checked directly from `APE_DB.AI_Rule_Artifacts` on 2026-08-03.

### 2.1 Prompt artifacts

| Artifact key | Active version | Source path in artifact row |
| --- | --- | --- |
| `question_generation` | `v14` | `ai-prompts.json` |
| `question_generation_repair` | `v5` | `ai-prompts.json` |
| `question_review` | `v12` | `ai-prompts.json` |

### 2.2 Policy artifacts

| Artifact key | Active version | Source path in artifact row |
| --- | --- | --- |
| `question-generation-review-policy.json` | `v1` | `question-generation-review-policy.json` |
| `extracted-content-policy.json` | `v2` | `extracted-content-policy.json` |
| `gatekeeper-policy.json` | `v2` | `gatekeeper-policy.json` |
| `code-mentor-policy.json` | `v1` | `code-mentor-policy.json` |

## 3. Important interpretation

The active DB state above means:

- generation/review runtime is currently using `question_generation v14`
- generation repair runtime is currently using `question_generation_repair v5`
- review runtime is currently using `question_review v12`
- generation/review policy runtime is currently using `question-generation-review-policy.json v1`

This is the runtime truth even if another local file copy shows a different version label.

## 3.1 What these active versions represent in behavior

These active versions are not only metadata labels.
They correspond to a set of BE updates that were made to improve question quality and reduce previously observed drift.

The relevant quality-hardening themes in the current active runtime are:

- stronger grounding discipline between retrieved chunks and generated questions
- stricter FE explanation behavior so explanations stay closer to supported chunk claims
- better shortfall handling so the system can stop early and explain why the requested count could not be fulfilled safely
- tighter PE anti-drift guidance so generated coding tasks do not invent unsupported public requirements
- stronger repair normalization so broken schema or malformed output is corrected before downstream use
- mixed-difficulty safeguards so expanded request flexibility does not explode latency or lower review quality

For FE and BE communication, this matters because:

- an artifact version bump should be understood as a behavioral change, not only a storage change
- when quality changes are discussed, the active DB version should be cited together with the intended effect on grounding, review, and shortfall behavior

## 4. Repo note about local references

At the time of this check, the repo still contains local artifact/reference files that can diverge from the active DB state.

That is acceptable only under this rule:

- local files are drafting and seeding material
- DB active artifact rows are the only runtime truth

If local references and DB active rows differ, FE and BE must align to the DB active rows until a new reseed is completed and verified.

## 4.1 Local artifact role

Local artifact files still matter operationally, but only in this sequence:

1. draft or edit the local reference artifact
2. review the intended quality/prompt/policy change
3. reseed to DB
4. verify the active DB row
5. update docs and handoff based on the verified active DB state

This means local files are the preparation layer for:

- better prompt wording
- tighter review rules
- improved FE explanation discipline
- PE anti-drift constraints
- future artifact history and rollback

But they are never the final source of truth until the DB activation step is complete.

## 5. FE and BE coordination note

When discussing prompt/policy behavior, both FE and BE should refer to this wording:

- "active runtime version" means the active artifact version in DB
- "local reference version" means the file version prepared for future reseed

Do not use local file version labels alone in handoff or testing notes without stating whether they are:

- reference only
- seeded but not verified
- active in DB

## 6. Recommended update rule for this note

Update this note whenever one of these happens:

- prompt or policy reseed
- active artifact rollback
- prompt/policy hotfix activation
- FE handoff that depends on changed AI output semantics

Minimum verification before updating this note:

- confirm the active artifact row in `AI_Rule_Artifacts`
- confirm the version shown in DB matches the intended release

## 7. Recommended wording when reporting AI quality changes

When the team reports AI quality updates to FE, mentors, or reviewers, the wording should combine:

- the active DB version
- the date of verification
- the practical quality effect

Example:

- "`question_review v12` was verified active in DB on 2026-08-03 and is part of the current stricter grounding/review runtime."
- "`question_generation v14` was verified active in DB on 2026-08-03 and is part of the current anti-drift generation runtime."

This avoids a common documentation problem where the team mentions a local file version but not the runtime truth.
