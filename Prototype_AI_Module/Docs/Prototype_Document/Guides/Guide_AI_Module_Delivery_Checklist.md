# Hướng dẫn checklist bàn giao AI Module

## Mục tiêu

Tài liệu này dùng để chốt nhanh trạng thái bàn giao của prototype AI module trước khi:

- ghép vào BE thật
- hoặc thiết kế UI vận hành và test prototype

## 1. Trạng thái tổng quát

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| 6 AI functions đã được mô hình hoá | `Done` | Gatekeeper, Extracted Content, Embedding + Auto Tagging, Generation, Review, Code Mentor |
| Prompt file-backed | `Done` | Đang lưu trong `App_Data/ai-prompts.json` |
| Policy theo function | `Done` | Đang lưu trong `App_Data/*-policy.json` |
| Rubric theo function | `Done` | Đang lưu trong `App_Data/ai-rubrics/*.json` |
| Ground truth theo function | `Done` | Đang lưu trong `App_Data/ai-groundtruth/*.json` |
| Debug routes theo function | `Partial` | Gatekeeper, Extracted, Embedding, Mentor có debug payload riêng; Question Generation và Generation-Review vẫn là placeholder |
| Build solution xanh | `Done` | `dotnet build` thành công |
| Provider raw usage capture | `Done` | Có `usage_source`, `raw_usage_json` nếu provider trả usage |
| Normalized error runtime | `Done` | Có error catalog ở mức runtime/history/export |
| Upload ingest route | `Done` | Có `uploads/ingest` cho `docx/pptx/pdf/image` |

## 2. Checklist theo AI function

### AI Gatekeeper

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Prompt | `Done` | Gatekeeper `v2` |
| Policy | `Done` | `gatekeeper-policy.json` |
| Rubric | `Done` | `GATEKEEPER.json` |
| Ground truth | `Done` | `GATEKEEPER_CORE.json` |
| Debug route | `Done` | Có `policy/rubric/ground-truth/debug` |
| Precheck | `Done` | Empty/short content |

### AI Extracted Content

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Prompt | `Done` | `extract_content_vision v2` |
| Policy | `Done` | `extracted-content-policy.json` |
| Rubric | `Done` | `EXTRACTED_CONTENT.json` |
| Ground truth | `Done` | `EXTRACTED_CONTENT_CORE.json` |
| Debug route | `Done` | Có `policy/rubric/ground-truth/debug` |
| Warning system | `Done` | Empty, no vision model, weak normalized text |

### AI Embedding + Auto Tagging

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Prompt | `Done` | `auto_tagging v2` |
| Policy | `Done` | `embedding-tagging-policy.json` |
| Rubric | `Done` | `EMBEDDING_TAGGING.json` |
| Ground truth | `Done` | `EMBEDDING_TAGGING_CORE.json` |
| Debug route | `Done` | Có `policy/rubric/ground-truth/debug` |
| KnowledgeChunk DB-ready | `Done` | Đã map theo schema chốt |
| Embedding/tagging totals tách riêng | `Done` | `EmbeddingTotals`, `TaggingTotals` |

### AI Question Generation

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Prompt | `Done` | `question_generation v2` |
| FE/PE schema output | `Done` | Đã normalize/validate |
| Rubric | `Done` | Theo `C/JAVA_OOP/DSA_JAVA` + `FE/PE` |
| Ground truth | `Done` | `QGEN_*` starter sets |
| Regression route | `Done` | Có regression runner |
| Export package | `Done` | Có route export JSON |
| Debug route | `Partial` | Route có tồn tại, nhưng hiện đang trả cùng payload runtime như route thường |

### AI Review Result of AI Question Generation

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Prompt | `Done` | `question_review v2` |
| 1-2 attempt loop | `Done` | Đã giới hạn retry |
| Review + schema + difficulty alignment | `Done` | Đã nối runtime |
| Debug/export route | `Partial` | `export` đã dùng đầy đủ; `generation-review/debug` hiện vẫn là placeholder |

### AI Code Mentor

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Prompt | `Done` | `code_mentor v3` |
| Policy | `Done` | `code-mentor-policy.json` |
| Rubric | `Done` | `CODE_MENTOR.json` |
| Ground truth | `Done` | `CODE_MENTOR_CORE.json` |
| Debug route | `Done` | Có `policy/rubric/ground-truth/debug` |
| Structured feedback | `Done` | verdict, categories, issues, suggestions, failing scenarios, confidence |

## 3. Collection và DB design support

| Collection | Trạng thái | Ghi chú |
|---|---|---|
| `AI_Prompt_Versions` | `Documented` | Đã có tài liệu |
| `AI_Policies` | `Documented` | Đã có tài liệu + mapping wrapper |
| `AI_Rubrics` | `Documented` | Đã có tài liệu |
| `AI_GroundTruth_Sets` | `Documented` | Đã có tài liệu |
| `KnowledgeChunks` | `Documented` | Đã có schema chốt |

## 4. App_Data source of truth

| Nhóm | Trạng thái | Ghi chú |
|---|---|---|
| `ai-prompts.json` | `Done` | Prompt runtime |
| `*-policy.json` | `Done` | Policy runtime |
| `ai-rubrics/*.json` | `Done` | Rubric runtime |
| `ai-groundtruth/*.json` | `Done` | Dataset runtime |
| `run-history/*` | `Done` | Runtime artifact cho benchmark/test harness |
| `uploads/*` | `Done` | Runtime artifact cho upload ingest |
| Mapping sang DB wrapper | `Done` | Đã có tài liệu đối chiếu |

## 5. Route API

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Runtime routes | `Done` | Đã có cho 6 function |
| Debug routes | `Partial` | QGen/GenReview chưa có debug payload riêng |
| Policy/rubric/ground-truth routes | `Done` | Đã có theo function |
| Prompt management routes | `Done` | Đã có |
| Provider management routes | `Done` | `providers`, `models`, `ping`, `reload` |
| Upload ingest route | `Done` | `uploads/ingest` |
| Benchmark session routes | `Done` | save/list/open session |

## 6. Những gì chưa xong cho production

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Mongo repository thật | `Pending` | Hiện vẫn in-memory/file-backed |
| Judge0 compile/run | `Pending` | Cho PE và Mentor |
| OCR/parser production-final | `Pending` | Hiện tại đã có parser prototype thực dụng, nhưng chưa là bản semantic/OCR cuối cùng |
| Price catalog production-final | `Pending` | Cost vẫn theo logic prototype, ưu tiên usage thật nếu provider trả được |
| Vector duplicate detection thật | `Pending` | Hiện mới heuristic text similarity |
| Admin UI production | `Pending` | Chưa làm |

## 7. Sẵn sàng cho bước tiếp theo

Prototype hiện tại đã sẵn sàng cho:

1. thiết kế UI vận hành và test prototype
2. nối vào DB thật sau
3. demo benchmark
4. làm tài liệu mô tả hệ thống AI

## Kết luận

Có thể xem module AI prototype hiện tại đã đạt mức:

- `Prototype Ready`

Và bước tiếp theo hợp lý nhất là:

- thiết kế UI đơn giản để vận hành, test và quan sát toàn bộ 6 AI function
