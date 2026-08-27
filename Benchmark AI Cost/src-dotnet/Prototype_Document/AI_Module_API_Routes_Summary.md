# AI Module API Routes Summary

## Muc tieu

Tai lieu nay tong hop nhanh toan bo route API cua module AI de:

- de doc
- de grep
- de dua cho FE / QA / nhom BE tong

## 1. Prompt Management

| Method | Route | Muc dich |
|---|---|---|
| `GET` | `/api/ai-module/prompts` | Lay danh sach prompt hien tai |
| `GET` | `/api/ai-module/prompts/{key}` | Lay 1 prompt theo key |
| `POST` | `/api/ai-module/prompts/reload` | Reload prompt tu file |
| `PUT` | `/api/ai-module/prompts/{key}` | Cap nhat/ghi de prompt |
| `POST` | `/api/ai-module/prompts/render` | Render prompt voi variables |

## 2. Gatekeeper

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/gatekeeper` | Chay gatekeeper |
| `POST` | `/api/ai-module/gatekeeper/debug` | Chay gatekeeper va tra them policy/rubric/ground truth |
| `GET` | `/api/ai-module/gatekeeper/policy` | Lay policy gatekeeper |
| `GET` | `/api/ai-module/gatekeeper/rubric` | Lay rubric gatekeeper |
| `GET` | `/api/ai-module/gatekeeper/ground-truth` | Lay ground truth gatekeeper |

## 3. Extracted Content

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/extracted-content` | Chay extract content |
| `POST` | `/api/ai-module/extracted-content/debug` | Chay extract content va tra them policy/rubric/ground truth |
| `GET` | `/api/ai-module/extracted-content/policy` | Lay policy extracted content |
| `GET` | `/api/ai-module/extracted-content/rubric` | Lay rubric extracted content |
| `GET` | `/api/ai-module/extracted-content/ground-truth` | Lay ground truth extracted content |

## 4. Embedding + Auto Tagging

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/embedding-tagging` | Chay embedding + auto tagging |
| `POST` | `/api/ai-module/embedding-tagging/debug` | Chay embedding + auto tagging va tra them policy/rubric/ground truth |
| `GET` | `/api/ai-module/embedding-tagging/policy` | Lay policy embedding/tagging |
| `GET` | `/api/ai-module/embedding-tagging/rubric` | Lay rubric embedding/tagging |
| `GET` | `/api/ai-module/embedding-tagging/ground-truth` | Lay ground truth embedding/tagging |

## 5. Ingestion / Full Flow

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/ingestion` | Chay flow ingestion: gatekeeper -> extract -> embed/tag |
| `POST` | `/api/ai-module/full-pipeline` | Chay full flow ingestion + generation/review |

## 6. Question Generation

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/question-generation` | Chay generation |
| `POST` | `/api/ai-module/question-generation/debug` | Route debug hien co, nhung dang tra cung payload runtime nhu generation thuong |
| `GET` | `/api/ai-module/question-generation/rubric` | Lay rubric generation/review theo subject + questionType |
| `GET` | `/api/ai-module/question-generation/ground-truth` | Lay ground truth generation theo subject + questionType |
| `POST` | `/api/ai-module/question-generation/regression` | Chay regression theo ground truth |

## 7. Question Review / Generation Review

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/question-review` | Review bo cau hoi co san |
| `POST` | `/api/ai-module/generation-review` | Chay generation + review |
| `POST` | `/api/ai-module/generation-review/debug` | Route debug hien co, nhung dang tra cung payload runtime nhu generation-review thuong |
| `POST` | `/api/ai-module/generation-review/export` | Export package JSON chuan hoa |

## 8. Code Mentor

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/code-mentor` | Chay mentor |
| `POST` | `/api/ai-module/code-mentor/debug` | Chay mentor va tra them policy/rubric/ground truth |
| `GET` | `/api/ai-module/code-mentor/policy` | Lay policy mentor |
| `GET` | `/api/ai-module/code-mentor/rubric` | Lay rubric mentor |
| `GET` | `/api/ai-module/code-mentor/ground-truth` | Lay ground truth mentor |

## 9. Provider / Connection

| Method | Route | Muc dich |
|---|---|---|
| `GET` | `/api/ai-module/providers` | Lay danh sach provider + summary |
| `GET` | `/api/ai-module/providers/{provider}/models` | Lay catalog model cua provider |
| `GET` | `/api/ai-module/providers/{provider}/ping` | Ping provider thuc te |
| `POST` | `/api/ai-module/providers/reload` | Reload provider config tu `.env` / config |

## 10. Upload / Prototype Ingest

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/uploads/ingest` | Upload file that, ingest prototype, tra `suggestedRawContent` + parser metadata |

## 11. Run History

| Method | Route | Muc dich |
|---|---|---|
| `GET` | `/api/ai-module/history?functionName=&take=` | Lay lich su tong hoac loc theo function |
| `GET` | `/api/ai-module/history/{functionName}?take=` | Lay lich su benchmark theo function |
| `GET` | `/api/ai-module/history/{functionName}/{runId}` | Lay file JSON chi tiet cua 1 run |

## 12. Benchmark Sessions

| Method | Route | Muc dich |
|---|---|---|
| `POST` | `/api/ai-module/benchmark-sessions` | Luu 1 dot benchmark/session |
| `GET` | `/api/ai-module/benchmark-sessions` | Lay danh sach session |
| `GET` | `/api/ai-module/benchmark-sessions/{sessionId}` | Lay chi tiet 1 session |

## Ghi chu van hanh

- `providers/reload` dung khi vua sua `.env`.
- `prompts/reload` dung khi vua sua `App_Data/ai-prompts.json`.
- `uploads/ingest` la route huu ich nhat de dua `docx/pptx/pdf/image` vao UI prototype ma khong can tu viet tay `rawContent`.
- `question-generation/debug` va `generation-review/debug` hien la route debug-placeholder; neu can payload debug rieng thi phai bo sung them o orchestrator/controller.

## Ket luan

Module AI hien tai da co:

- route runtime
- route debug
- route policy/rubric/ground truth
- route prompt management
- route provider management
- route upload ingest
- route benchmark session

Nen kha de noi FE test, QA va BE tong.
