# AI Runtime DB-First And Audit Notes

Date: 2026-07-30

This document records the runtime truth model for AI artifacts and the latest audit status.

## 1. Runtime truth

The backend must be understood as:

- local/dev: DB-first, optional file fallback
- production/runtime truth: DB active artifacts

Meaning:

- prompts, policies, rubrics, and related AI rules should be treated as DB-driven
- local files under `Infrastructure/Data/App_Data` are reference/edit sources
- production behavior should be explained using active DB versions, not just file contents

## 2. Seed and audit workflow

Recommended workflow:

1. update local artifact reference
2. reseed DB
3. run artifact audit
4. only then treat the new version as active runtime behavior

## 3. Latest audit conclusion

Latest audit conclusion for current AI work:

- the important active AI artifacts for the current flow are seeded correctly in DB
- `ExtractedStructure` runtime should be read through the active DB `Extraction` agent route
- chapter detection is no longer treated as a separate code-default provider path when DB agent routing is available

Confirmed active matches included:

- `code_mentor` -> `v5`
- `code-mentor-policy.json` -> `v2`
- `ai-rubrics/CODE_MENTOR.json` -> `v2`
- `question_generation` active DB -> `v11`
- `question_generation_repair` active DB -> `v4`
- `question_review` -> `v7`
- `question-generation-review-policy.json` -> `v1`

Interpretation of apparent prompt mismatches:

- local files may still contain older historical prompt entries
- this is not the same as active runtime mismatch
- the active DB version is the important source of truth

## 4. Known note

There is still a taxonomy note to keep in mind:

- local taxonomy reference file may be missing while DB still has an active taxonomy version

This is not the main blocker for the current AI flow, but it should be documented if taxonomy maintenance becomes active work again.

## 5. What this means for future AI changes

Any future AI rule change should aim for:

- artifact update first
- DB reseed
- DB audit
- minimal hard-coded logic in services

This is especially important for:

- PE/FE academic quality rules
- retrieval pack behavior
- mentor feedback rules

## 6. Runtime validation note

Recent end-to-end validation confirmed:

- BYOS upload billing is deducting VND correctly again
- API upload responses, user wallet balance, and `AI_VND_Billing_Transactions` records matched on the same real uploads
- chapter detection stability should now be evaluated from the DB-routed extraction path, not from the older hard-coded fallback assumption
