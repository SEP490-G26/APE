# AI Module Delivery Checklist

## Muc tieu

Tai lieu nay dung de chot nhanh trang thai ban giao cua prototype AI module truoc khi:

- ghep vao BE that
- hoac thiet ke UI van hanh/test prototype

## 1. Trang thai tong quat

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| 6 AI functions da duoc mo hinh hoa | `Done` | Gatekeeper, Extracted Content, Embedding + Auto Tagging, Generation, Review, Code Mentor |
| Prompt file-backed | `Done` | Dang luu trong `App_Data/ai-prompts.json` |
| Policy theo function | `Done` | Dang luu trong `App_Data/*-policy.json` |
| Rubric theo function | `Done` | Dang luu trong `App_Data/ai-rubrics/*.json` |
| Ground truth theo function | `Done` | Dang luu trong `App_Data/ai-groundtruth/*.json` |
| Debug routes theo function | `Partial` | Gatekeeper / Extracted / Embedding / Mentor co debug payload rieng; Question Generation va Generation-Review debug route hien dang la placeholder |
| Build solution xanh | `Done` | `dotnet build` thanh cong |
| Provider raw usage capture | `Done` | Co `usage_source`, `raw_usage_json` neu provider tra usage |
| Normalized error runtime | `Done` | Co error catalog muc runtime/history/export |
| Upload ingest route | `Done` | Co `uploads/ingest` cho `docx/pptx/pdf/image` |

## 2. Checklist theo AI function

### AI Gatekeeper

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Prompt | `Done` | Gatekeeper `v2` |
| Policy | `Done` | `gatekeeper-policy.json` |
| Rubric | `Done` | `GATEKEEPER.json` |
| Ground truth | `Done` | `GATEKEEPER_CORE.json` |
| Debug route | `Done` | Co `policy/rubric/ground-truth/debug` |
| Precheck | `Done` | Empty/short content |

### AI Extracted Content

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Prompt | `Done` | `extract_content_vision v2` |
| Policy | `Done` | `extracted-content-policy.json` |
| Rubric | `Done` | `EXTRACTED_CONTENT.json` |
| Ground truth | `Done` | `EXTRACTED_CONTENT_CORE.json` |
| Debug route | `Done` | Co `policy/rubric/ground-truth/debug` |
| Warning system | `Done` | Empty, no vision model, weak normalized text |

### AI Embedding + Auto Tagging

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Prompt | `Done` | `auto_tagging v2` |
| Policy | `Done` | `embedding-tagging-policy.json` |
| Rubric | `Done` | `EMBEDDING_TAGGING.json` |
| Ground truth | `Done` | `EMBEDDING_TAGGING_CORE.json` |
| Debug route | `Done` | Co `policy/rubric/ground-truth/debug` |
| KnowledgeChunk DB-ready | `Done` | Da map theo schema chot |
| Embedding/tagging totals tach rieng | `Done` | `EmbeddingTotals`, `TaggingTotals` |

### AI Question Generation

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Prompt | `Done` | `question_generation v2` |
| FE/PE schema output | `Done` | Da normalize/validate |
| Rubric | `Done` | Theo `C/JAVA_OOP/DSA_JAVA` + `FE/PE` |
| Ground truth | `Done` | `QGEN_*` starter sets |
| Regression route | `Done` | Co regression runner |
| Export package | `Done` | Co route export JSON |
| Debug route | `Partial` | Route co ton tai, nhung hien dang tra cung payload runtime nhu route thuong |

### AI Review Result of AI Question Generation

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Prompt | `Done` | `question_review v2` |
| 1-2 attempt loop | `Done` | Da gioi han retry |
| Review + schema + difficulty alignment | `Done` | Da noi runtime |
| Debug/export route | `Partial` | `export` da dung day du; `generation-review/debug` hien dang la placeholder |

### AI Code Mentor

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Prompt | `Done` | `code_mentor v3` |
| Policy | `Done` | `code-mentor-policy.json` |
| Rubric | `Done` | `CODE_MENTOR.json` |
| Ground truth | `Done` | `CODE_MENTOR_CORE.json` |
| Debug route | `Done` | Co `policy/rubric/ground-truth/debug` |
| Structured feedback | `Done` | verdict, categories, issues, suggestions, failing scenarios, confidence |

## 3. Collection / DB design support

| Collection | Trang thai | Ghi chu |
|---|---|---|
| `AI_Prompt_Versions` | `Documented` | Da co tai lieu |
| `AI_Policies` | `Documented` | Da co tai lieu + mapping wrapper |
| `AI_Rubrics` | `Documented` | Da co tai lieu |
| `AI_GroundTruth_Sets` | `Documented` | Da co tai lieu |
| `KnowledgeChunks` | `Documented` | Da co schema chot |

## 4. App_Data source of truth

| Nhom | Trang thai | Ghi chu |
|---|---|---|
| `ai-prompts.json` | `Done` | Prompt runtime |
| `*-policy.json` | `Done` | Policy runtime |
| `ai-rubrics/*.json` | `Done` | Rubric runtime |
| `ai-groundtruth/*.json` | `Done` | Dataset runtime |
| `run-history/*` | `Done` | Runtime artifact cho benchmark/test harness |
| `uploads/*` | `Done` | Runtime artifact cho upload ingest |
| Mapping sang DB wrapper | `Done` | Da co tai lieu doi chieu |

## 5. Route API

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Runtime routes | `Done` | Da co cho 6 function |
| Debug routes | `Partial` | QGen/GenReview chua co debug payload rieng |
| Policy/rubric/ground-truth routes | `Done` | Da co theo function |
| Prompt management routes | `Done` | Da co |
| Provider management routes | `Done` | `providers`, `models`, `ping`, `reload` |
| Upload ingest route | `Done` | `uploads/ingest` |
| Benchmark session routes | `Done` | save/list/open session |

## 6. Nhung gi CHUA xong cho production

| Hang muc | Trang thai | Ghi chu |
|---|---|---|
| Mongo repository that | `Pending` | Hien van in-memory/file-backed |
| Judge0 compile/run | `Pending` | Cho PE va Mentor |
| OCR/parser production-final | `Pending` | Hien tai da co parser prototype thuc dung, nhung chua la ban semantic/OCR cuoi cung |
| Price catalog production-final | `Pending` | Cost van theo logic prototype/provider usage co duoc thi uu tien |
| Vector duplicate detection that | `Pending` | Hien moi heuristic text similarity |
| Admin UI production | `Pending` | Chua lam |

## 7. San sang cho buoc tiep theo

Prototype hien tai da san sang cho:

1. thiet ke UI van hanh/test prototype
2. noi vao DB that sau
3. demo benchmark
4. lam tai lieu mo ta he thong AI

## Ket luan

Co the xem module AI prototype hien tai da dat muc:

- `Prototype Ready`

Va buoc tiep theo hop ly nhat la:

- thiet ke UI don gian de van hanh, test, va quan sat toan bo 6 AI function.
