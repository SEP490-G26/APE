# Bảng route API của AI Module

## Mục đích

Tài liệu này gom toàn bộ route chính để nhóm FE, QA và BE tổng tra cứu nhanh.

## Prompt management

| Method | Route | Mục đích |
|---|---|---|
| `GET` | `/api/ai-module/prompts` | Lấy danh sách prompt hiện tại |
| `GET` | `/api/ai-module/prompts/{key}` | Lấy một prompt theo key |
| `GET` | `/api/ai-module/prompts/{key}/history` | Lấy lịch sử snapshot version của prompt theo key |
| `POST` | `/api/ai-module/prompts/reload` | Reload prompt từ file |
| `PUT` | `/api/ai-module/prompts/{key}` | Cập nhật prompt |
| `POST` | `/api/ai-module/prompts/render` | Render prompt với variables |

## Gatekeeper

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/gatekeeper` | Chạy gatekeeper |
| `POST` | `/api/ai-module/gatekeeper/debug` | Chạy gatekeeper kèm policy/rubric/ground truth |
| `GET` | `/api/ai-module/gatekeeper/policy` | Lấy policy gatekeeper |
| `GET` | `/api/ai-module/gatekeeper/rubric` | Lấy rubric gatekeeper |
| `GET` | `/api/ai-module/gatekeeper/ground-truth` | Lấy ground truth gatekeeper |

## Extracted Content

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/extracted-content` | Chạy extracted content |
| `POST` | `/api/ai-module/extracted-content/debug` | Chạy extracted content debug |
| `GET` | `/api/ai-module/extracted-content/policy` | Lấy policy extracted content |
| `GET` | `/api/ai-module/extracted-content/rubric` | Lấy rubric extracted content |
| `GET` | `/api/ai-module/extracted-content/ground-truth` | Lấy ground truth extracted content |

## Embedding + Auto Tagging

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/embedding-tagging` | Chạy embedding + auto tagging |
| `POST` | `/api/ai-module/embedding-tagging/debug` | Chạy embedding + auto tagging debug |
| `GET` | `/api/ai-module/embedding-tagging/policy` | Lấy policy embedding/tagging |
| `GET` | `/api/ai-module/embedding-tagging/rubric` | Lấy rubric embedding/tagging |
| `GET` | `/api/ai-module/embedding-tagging/ground-truth` | Lấy ground truth embedding/tagging |

## Ingestion / full flow

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/ingestion` | Chạy flow ingestion: gatekeeper -> extract -> embed/tag |
| `POST` | `/api/ai-module/full-pipeline` | Chạy full flow ingestion + generation/review |

## Question generation

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/question-generation` | Chạy generation |
| `POST` | `/api/ai-module/question-generation/debug` | Route debug hiện có, nhưng đang trả cùng payload runtime như generation thường |
| `GET` | `/api/ai-module/question-generation/rubric` | Lấy rubric generation/review theo subject + questionType |
| `GET` | `/api/ai-module/question-generation/ground-truth` | Lấy ground truth generation theo subject + questionType |
| `POST` | `/api/ai-module/question-generation/regression` | Chạy regression theo ground truth |

## Question review / generation review

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/question-review` | Review bộ câu hỏi có sẵn |
| `POST` | `/api/ai-module/generation-review` | Chạy generation + review |
| `POST` | `/api/ai-module/generation-review/debug` | Route debug hiện có, nhưng đang trả cùng payload runtime như generation-review thường |
| `POST` | `/api/ai-module/generation-review/export` | Export package JSON chuẩn hoá |

### Ghi chú flow end-to-end cho generation/review

- `generation-review` hiện hỗ trợ 3 kiểu input:
  - `chunks`
  - `contextPackId`
  - `retrieval`
- Nếu muốn test gần production:
  1. gọi `POST /api/ai-module/retrieval/plan`
  2. lấy `contextPack.packId`
  3. gọi `POST /api/ai-module/generation-review` với `contextPackId`
- Nếu muốn test nhanh quality từ file chunk nhỏ:
  - có thể gửi trực tiếp `chunks`, nhưng cost sẽ phản ánh kiểu benchmark hơn là production retrieval flow

## Code mentor

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/code-mentor` | Chạy mentor cho bài nộp `PE` |
| `POST` | `/api/ai-module/code-mentor/debug` | Chạy mentor cho `PE` và trả thêm policy/rubric/ground truth |
| `GET` | `/api/ai-module/code-mentor/policy` | Lấy policy mentor |
| `GET` | `/api/ai-module/code-mentor/rubric` | Lấy rubric mentor |
| `GET` | `/api/ai-module/code-mentor/ground-truth` | Lấy ground truth mentor |

### Ghi chú cho code mentor

- `AI Code Mentor` hiện chỉ áp dụng cho `PE`, không dùng cho `FE`.
- Kết quả hiện tại là `static AI review` dựa trên problem statement + code source.
- Chưa có compile/run verification thật ở route này cho tới khi nối `Judge0`.
- Output hiện đã theo structured schema mới:
  - `quality_score`
  - `performance_summary`
  - `error_analysis`
  - `improvement_suggestions`

## Provider / connection

| Method | Route | Mục đích |
|---|---|---|
| `GET` | `/api/ai-module/providers` | Lấy danh sách provider + summary |
| `GET` | `/api/ai-module/providers/{provider}/models` | Lấy catalog model của provider |
| `GET` | `/api/ai-module/providers/{provider}/ping` | Ping provider thực tế |
| `POST` | `/api/ai-module/providers/reload` | Reload provider config từ `.env` / config |

## Upload / prototype ingest

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/uploads/ingest` | Upload file thật, ingest prototype, trả `suggestedRawContent` + parser metadata |

## Run history

| Method | Route | Mục đích |
|---|---|---|
| `GET` | `/api/ai-module/history?functionName=&take=` | Lấy lịch sử tổng hoặc lọc theo function |
| `GET` | `/api/ai-module/history/{functionName}?take=` | Lấy lịch sử benchmark theo function |
| `GET` | `/api/ai-module/history/{functionName}/{runId}` | Lấy file JSON chi tiết của một run |

## Benchmark sessions

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/ai-module/benchmark-sessions` | Lưu một đợt benchmark/session |
| `GET` | `/api/ai-module/benchmark-sessions` | Lấy danh sách session |
| `GET` | `/api/ai-module/benchmark-sessions/{sessionId}` | Lấy chi tiết một session |

## Ghi chú

- `providers/reload` dùng khi vừa sửa `.env`.
- `prompts/reload` dùng khi vừa sửa `App_Data/ai-prompts.json`.
- `prompts/{key}/history` dùng để audit version cũ đã được snapshot trong prototype.
- `uploads/ingest` là route hữu ích nhất để đưa `docx/pptx/pdf/image` vào UI prototype mà không cần tự viết tay `rawContent`.
- `question-generation/debug` và `generation-review/debug` hiện là route debug placeholder; nếu cần payload debug riêng thì phải bổ sung thêm ở orchestrator/controller.

Tài liệu này chỉ tập trung vào route. Phần flow nghiệp vụ xem thêm trong `Specs/`.


## Retrieval + Context Packs

| Method | Route | Mục đích |
| --- | --- | --- |
| POST | /api/ai-module/retrieval/plan | Lập retrieval plan và build/load context pack phù hợp |
| POST | /api/ai-module/retrieval/plan/debug | Trả plan debug gồm candidate pools, rejected chunks, scoring weights |
| POST | /api/ai-module/context-packs/build | Build context pack từ danh sách chunk đã biết |
| GET | /api/ai-module/context-packs/{packId} | Lấy chi tiết một context pack |
| GET | /api/ai-module/context-packs?subject=&questionType=&difficulty=&topic=&status= | Lấy danh sách context pack theo filter |
| POST | /api/ai-module/context-packs/{packId}/mark-stale | Đánh dấu context pack cần rebuild |

### Dùng route nào trong thực tế

- Benchmark nhỏ, kiểm tra prompt nhanh:
  - dùng `chunks` trực tiếp trong `generation-review`
- Benchmark gần production:
  - `retrieval/plan` -> `contextPackId` -> `generation-review`
- Chạy lại đúng cùng một ngữ cảnh để so model:
  - dùng `contextPackId`

