# AI Config Version History

Tài liệu này tổng hợp lịch sử version đã được backfill từ git cho prompt, policy, rubric và ground truth.

## Phạm vi và giới hạn

- Nguồn khôi phục là git history hiện còn trong repo.
- Nếu một artifact trước đây đã từng thay đổi nhưng chưa từng được commit, thì không thể khôi phục lại chính xác.
- Policy và ground truth hiện chỉ có bản khởi tạo trong git, nên lịch sử của hai nhóm này còn rất ngắn.
- Một số rubric đã thay đổi nội dung nhưng version trong JSON chưa được tăng; vì vậy cần đọc đồng thời `change_reason` và `change_summary`, không chỉ nhìn `version`.

## File xuất dùng cho report

- CSV: `Docs/Prototype_Document/Guides/Templates/AI_Report/ai_config_version_changelog.csv`
- Snapshot history: `src-dotnet/Ape.AiModule.Api/App_Data/ai-config-history/`

## groundtruth / CODE_MENTOR_CORE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / EMBEDDING_TAGGING_CORE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / EXTRACTED_CONTENT_CORE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / GATEKEEPER_CORE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / QGEN_C_FE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / QGEN_C_PE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / QGEN_DSA_JAVA_FE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / QGEN_DSA_JAVA_PE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / QGEN_JAVA_OOP_FE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## groundtruth / QGEN_JAVA_OOP_PE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## policies / code_mentor_policy_v1

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## policies / embedding_tagging_policy_v1

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## policies / extracted_content_policy_v1

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## policies / gatekeeper_policy_v1

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## prompts / auto_tagging

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v2 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v2 | 2026-07-17T19:00:42.0000000Z | f79e31c | fix ai module | No content change relative to previous snapshot. |
| v2 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: UpdatedAt |

## prompts / code_mentor

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v3 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v3 | 2026-07-17T19:00:42.0000000Z | f79e31c | fix ai module | No content change relative to previous snapshot. |
| v3 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: UpdatedAt |

## prompts / extract_content_vision

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v2 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v2 | 2026-07-17T19:00:42.0000000Z | f79e31c | fix ai module | No content change relative to previous snapshot. |
| v2 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: UpdatedAt |

## prompts / gatekeeper

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v2 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v2 | 2026-07-17T19:00:42.0000000Z | f79e31c | fix ai module | No content change relative to previous snapshot. |
| v2 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: UpdatedAt |

## prompts / question_generation

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v2 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v4 | 2026-07-17T19:00:42.0000000Z | f79e31c | fix ai module | Changed fields: SystemPrompt, UserPrompt, Version |
| v8 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: UpdatedAt, UserPrompt, Version |

## prompts / question_generation_repair

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Initial snapshot imported from git history. |

## prompts / question_review

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v2 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v4 | 2026-07-17T19:00:42.0000000Z | f79e31c | fix ai module | Changed fields: SystemPrompt, UserPrompt, Version |
| v7 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: SystemPrompt, UpdatedAt, UserPrompt, Version |

## rubrics / C_FE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## rubrics / C_PE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v1 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: few_shot_examples, quality_dimensions, quality_exemplars, quality_red_flags, quality_regeneration_hints |

## rubrics / CODE_MENTOR

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## rubrics / DSA_JAVA_FE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## rubrics / DSA_JAVA_PE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v1 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: few_shot_examples, quality_dimensions, quality_exemplars, quality_red_flags, quality_regeneration_hints |

## rubrics / EMBEDDING_TAGGING

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## rubrics / EXTRACTED_CONTENT

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## rubrics / GATEKEEPER

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## rubrics / JAVA_OOP_FE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |

## rubrics / JAVA_OOP_PE

| Version | Commit Date (UTC) | Commit | Reason | Summary |
|---|---|---|---|---|
| v1 | 2026-07-16T06:59:08.0000000Z | 511aa50 | Add AI module prototype, operator UI, and docs | Initial snapshot imported from git history. |
| v1 | 2026-07-17T20:20:33.0000000Z | 414b1d8 | update FE/PE question generation prompt quality | Changed fields: few_shot_examples, quality_dimensions, quality_exemplars, quality_red_flags, quality_regeneration_hints |


