# AI_Prompt_Versions

## Mục tiêu

Collection `AI_Prompt_Versions` dùng để lưu **toàn bộ lịch sử version** của prompt runtime.

Collection này không chỉ giữ prompt đang active, mà phải giữ được:

- bản prompt nào đã được dùng cho benchmark
- bản prompt nào đã được dùng cho production
- prompt thay đổi ở version nào
- run nào đang tham chiếu tới prompt version nào

Điều này rất quan trọng để:

- so sánh kết quả giữa các lần tinh chỉnh prompt
- đảm bảo benchmark có thể lặp lại
- phục vụ audit học thuật cho đồ án

## Schema đề nghị

```js
{
  _id: ObjectId,

  prompt_key: String,          // vd: question_generation, question_review, code_mentor
  function_name: String,       // Gatekeeper | ExtractedContent | EmbeddingTagging | QuestionGeneration | QuestionReview | CodeMentor
  subject_code: String,        // nullable, dùng khi prompt tách theo môn
  question_type: String,       // nullable, FE | PE nếu áp dụng
  language: String,            // nullable, vd: en

  agent_id: ObjectId,          // ref AI_Agents._id nếu hệ thống có quản lý agent riêng
  version: String,             // vd: v1, v2, v8, 2026-07-18-a
  is_active: Boolean,

  description: String,         // mô tả ngắn mục đích của prompt version
  system_prompt: String,
  user_prompt: String,
  variables: [String],         // danh sách biến template như subject, chunks_json, rubric_json
  change_summary: String,      // nullable, ghi rõ đã chỉnh gì ở version này
  change_reason: String,       // nullable, lý do business/quality khiến version này được tạo ra
  source_commit: String,       // nullable, commit đã sinh ra snapshot này nếu backfill từ git
  captured_from: String,       // nullable, file/nguồn đã capture snapshot
  content_hash: String,        // hash để audit nội dung thật

  created_at: Date,
  updated_at: Date
}
```

## Ý nghĩa từng field

- `prompt_key`: mã ổn định của prompt theo chức năng
- `function_name`: function AI mà prompt phục vụ
- `subject_code`: dùng khi muốn tách prompt riêng theo môn
- `question_type`: dùng khi prompt phụ thuộc FE/PE
- `language`: ngôn ngữ chính của prompt
- `agent_id`: liên kết tới AI agent trong hệ thống thật nếu có
- `version`: version business, không nên dựa duy nhất vào timestamp
- `is_active`: prompt này có đang được runtime dùng hay không
- `description`: mô tả ngắn giúp operator đọc nhanh
- `system_prompt`: phần system
- `user_prompt`: phần user template
- `variables`: bộ biến template cho prompt render
- `change_summary`: ghi rõ thay đổi giữa version cũ và mới
- `change_reason`: giải thích vì sao cần đổi prompt
- `source_commit`: liên kết ngược tới commit khi cần audit report hoặc rollback
- `captured_from`: biết snapshot này đến từ file current, DB, hay git backfill
- `content_hash`: dùng để chống mất dấu khi ai đó sửa cùng version

## Quy tắc versioning nên áp dụng

1. Không overwrite prompt cũ đã dùng cho benchmark/report.
2. Mỗi lần tinh chỉnh đáng kể phải tạo version mới.
3. Nếu nội dung prompt đổi nhưng `version` không đổi thì vẫn phải lưu snapshot mới và giữ `content_hash` khác nhau.
4. Chỉ nên có 1 prompt active cho cùng một `prompt_key + subject_code + question_type` trong một thời điểm.

## Mapping với prototype hiện tại

### Current runtime file

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

### Current history file store

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-config-history/prompts/{prompt_key}/*.json`

Trong prototype hiện tại:

- `ai-prompts.json` là snapshot active/current cho runtime
- `ai-config-history/prompts/...` là lịch sử immutable append-only của từng prompt key

## Route prototype liên quan

- `GET /api/ai-module/prompts`
- `GET /api/ai-module/prompts/{key}`
- `GET /api/ai-module/prompts/{key}/history`
- `PUT /api/ai-module/prompts/{key}`
- `POST /api/ai-module/prompts/reload`
- `POST /api/ai-module/prompts/render`

## Gợi ý index

- `{ prompt_key: 1, version: 1 }`
- `{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }`
- `{ content_hash: 1 }`

## Cách ghép vào BE thật

1. Khi import từ prototype, đọc `App_Data/ai-prompts.json` để lấy current active.
2. Nếu cần audit đầy đủ, import luôn toàn bộ snapshot trong `App_Data/ai-config-history/prompts/`.
3. Mỗi run benchmark hoặc production nên lưu:
   - `prompt_key`
   - `prompt_version`
   - `content_hash`
   - `source_commit` nếu snapshot đến từ lịch sử backfill hoặc release commit
4. `run history` hoặc `AI_Usage_Logs` nên có `prompt_version_refs`.

## Kết luận

`AI_Prompt_Versions` là collection bắt buộc nếu muốn:

- chứng minh quá trình tinh chỉnh prompt
- benchmark có thể lặp lại
- trace được version prompt nào gây ra kết quả nào
