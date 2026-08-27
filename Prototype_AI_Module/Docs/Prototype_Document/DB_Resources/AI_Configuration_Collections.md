# AI Configuration Collections

## Mục tiêu

Tài liệu này chốt khung collection để đưa vào DB thật cho nhóm cấu hình AI và collection nghiệp vụ hỗ trợ pipeline tài liệu:

- `AI_Rubric_Versions`
- `AI_GroundTruth_Sets`
- `AI_Prompt_Versions`
- `AI_Policy_Versions`
- `DocumentExtractionDrafts`
- `DocumentExtractionDraftContents`

Mục tiêu là:

- prototype và BE tổng có cùng một hướng field
- prompt, rubric, ground truth, policy đều có snapshot version rõ ràng
- để benchmark và production có thể trace lại đang dùng version nào

## 1. AI_Prompt_Versions

Collection này là bản đổi tên và chuẩn hoá từ collection `Prompts` mà nhóm hiện đã có.

### Mục đích

- lưu version prompt của từng AI agent
- cho phép bật và tắt prompt version
- để trace benchmark hoặc production đang dùng prompt nào

### Schema đề nghị

```js
{
  _id: ObjectId,
  agent_id: ObjectId,          // ref AI_Agents._id
  version: String,             // vd: v1, v2, genrev-2026-07
  is_active: Boolean,
  system_prompt: String,
  variables: [String],         // vd: subject, question_type, chunks_json
  change_reason: String,       // nullable, vì sao version này được tạo
  change_summary: String,      // nullable, khác gì so với version trước
  source_commit: String,       // nullable, commit dùng để backfill hoặc release trace
  captured_from: String,       // nullable, file hoặc nguồn đã capture snapshot
  content_hash: String,        // hash để audit đúng nội dung
  created_at: Date,
  updated_at: Date
}
```

### Mapping với prototype hiện tại

Trong prototype `.NET`, object gần tương ứng là:

- `AiPromptVersionRecord`

### Ghi chú

- mỗi `agent_id` có thể có nhiều version
- chỉ nên có 1 version active tại 1 thời điểm cho 1 `agent_id`
- benchmark có thể override prompt version để so sánh

## 2. AI_Rubric_Versions

Collection này dùng để lưu rubric có cấu trúc theo kiểu versioned snapshot cho các tác vụ cần đánh giá theo tiêu chí học thuật hoặc nghiệp vụ.

### Tác vụ chính nên dùng

- `Question Generation / Review`
- `Code Mentor`

Có thể mở rộng sau cho:

- `Gatekeeper` policy rubric nhẹ

### Schema đề nghị

```js
{
  _id: ObjectId,
  rubric_id: String,           // vd: C_FE, JAVA_OOP_PE, CODE_MENTOR_JAVA
  function_name: String,       // QuestionGenerationReview | CodeMentor
  subject_code: String,        // C | JAVA_OOP | DSA_JAVA
  question_type: String,       // FE | PE | null nếu không áp dụng
  version: String,             // vd: v1, v2
  language: String,            // en
  is_active: Boolean,
  change_reason: String,       // nullable
  change_summary: String,      // nullable
  source_commit: String,       // nullable
  captured_from: String,       // nullable
  content_hash: String,        // hash chống mất dấu version
  content_json: Object,        // toàn bộ rubric JSON có cấu trúc
  created_at: Date,
  updated_at: Date
}
```

### `content_json` nên chứa gì

Với `Question Generation / Review`, `content_json` nên chứa:

- `difficulty_definitions`
- `review_checks`
- `difficulty_mismatch_signals`
- `acceptance_rules`
- `regeneration_hints`

Với `Code Mentor`, `content_json` có thể chứa:

- `verdict_levels`
- `issue_categories`
- `feedback_style_rules`
- `difficulty_expectations`
- `suggestion_quality_rules`

### Mapping với prototype hiện tại

Trong prototype `.NET`, object gần tương ứng là:

- `AiRubricRecord`

### Ghi chú

- file JSON hiện tại trong `App_Data/ai-rubrics/` là source of truth current tạm thời
- lịch sử snapshot file-backed hiện nằm trong `App_Data/ai-config-history/rubrics/`
- khi đưa vào production, các file này nên import vào `AI_Rubric_Versions`

## 3. AI_GroundTruth_Sets

Collection này dùng để lưu bộ ground truth để:

- benchmark
- accuracy evaluation
- regression test

### Tác vụ chính nên dùng

- `Question Generation / Review`
- `Code Mentor`
- có thể có bộ nhẹ cho `Gatekeeper`

### Schema đề nghị

```js
{
  _id: ObjectId,
  dataset_name: String,        // vd: mentor_20_cases_v1
  function_name: String,       // Gatekeeper | QuestionGenerationReview | CodeMentor
  subject_code: String,        // C | JAVA_OOP | DSA_JAVA | MIXED
  question_type: String,       // FE | PE | null nếu không áp dụng
  version: String,             // vd: v1
  language: String,            // en | vi | mixed
  is_active: Boolean,
  change_reason: String,       // nullable
  change_summary: String,      // nullable
  source_commit: String,       // nullable
  captured_from: String,       // nullable
  content_hash: String,        // hash chống mất dấu dataset
  content_json: Object,        // toàn bộ dataset / expected answers / labels
  created_at: Date,
  updated_at: Date
}
```

### `content_json` nên chứa gì

Với `Question Generation / Review`:

- case list
- source chunks hoặc dataset reference
- expected difficulty
- expected topic tags
- expected accept/reject note

Với `Code Mentor`:

- problem statement
- student code
- expected issues
- expected verdict
- expected suggestion direction

Với `Gatekeeper`:

- file case id
- supported / unsupported / ambiguous
- expected domain

### Mapping với prototype hiện tại

Trong prototype `.NET`, object gần tương ứng là:

- `AiGroundTruthSetRecord`

### Ghi chú versioning

- current file trong `App_Data/ai-groundtruth/` là active dataset
- lịch sử snapshot file-backed hiện nằm trong `App_Data/ai-config-history/groundtruth/`
- khi nội dung dataset thay đổi, cần giữ snapshot cũ để benchmark/report không mất căn cứ

## 4. AI_Policy_Versions

Collection này dùng để lưu policy runtime theo kiểu versioned snapshot.

### Vai trò

- giữ business rules runtime cho từng function AI
- lưu lịch sử thay đổi policy thay vì chỉ giữ 1 file current
- giúp trace đúng policy version nào đã được dùng cho run cụ thể

### Schema đề nghị

```js
{
  _id: ObjectId,
  policy_id: String,           // vd: gatekeeper_policy_v1
  policy_name: String,         // tên đọc được trên admin/UI
  function_name: String,       // Gatekeeper | ExtractedContent | EmbeddingTagging | CodeMentor
  subject_code: String,        // nullable
  question_type: String,       // nullable
  language: String,            // nullable
  version: String,             // vd: v1, v2
  is_active: Boolean,
  description: String,
  change_reason: String,       // nullable
  change_summary: String,      // nullable
  source_commit: String,       // nullable
  captured_from: String,       // nullable
  content_hash: String,        // hash để trace content thực tế
  content_json: Object,
  created_at: Date,
  updated_at: Date
}
```

### Mapping với prototype hiện tại

- current payload nằm trong:
  - `App_Data/gatekeeper-policy.json`
  - `App_Data/extracted-content-policy.json`
  - `App_Data/embedding-tagging-policy.json`
  - `App_Data/code-mentor-policy.json`
- history snapshot file-backed nằm trong:
  - `App_Data/ai-config-history/policies/`

### Ghi chú

- file current chỉ là active payload cho runtime
- collection DB thật phải giữ toàn bộ version history
- nếu policy đổi nhưng `version` chưa đổi thì vẫn nên lưu snapshot mới kèm `content_hash`
- nên lưu cả `change_reason`, `change_summary`, `source_commit`, `captured_from` để phục vụ report và audit production

## 5. Quan hệ giữa 4 collection

### AI_Prompt_Versions

Trả lời câu hỏi:

- agent này đang nói gì với AI

### AI_Rubric_Versions

Trả lời câu hỏi:

- agent này đánh giá hoặc quy chuẩn theo bộ tiêu chí nào

### AI_GroundTruth_Sets

Trả lời câu hỏi:

- agent này được test hoặc chấm dựa trên bộ dữ liệu chuẩn nào

### AI_Policy_Versions

Trả lời câu hỏi:

- agent này đang chạy theo business rule runtime nào

## 5A. DocumentExtractionDrafts và DocumentExtractionDraftContents

Hai collection này không phải cấu hình AI, nhưng là collection nghiệp vụ cần có để pipeline tài liệu chạy đúng trong production.

### Vai trò

- `DocumentExtractionDrafts`: giữ metadata cấp tài liệu, trạng thái pipeline, cost, credit, preview
- `DocumentExtractionDraftContents`: giữ nội dung dài theo từng segment để human review và approve
- tránh upload lại tài liệu và tránh gọi OCR/vision lặp lại không cần thiết

### Tài liệu schema chi tiết

- `Docs/Prototype_Document/DB_Resources/DocumentExtractionDrafts.md`
- `Docs/Prototype_Document/DB_Resources/DocumentExtractionDraftContents.md`

## 6. Collection nào liên quan tới chức năng nào

### Question Generation / Review

- cần `AI_Prompt_Versions`
- cần `AI_Rubric_Versions`
- cần `AI_GroundTruth_Sets`
- cần `AI_Policy_Versions` nếu function có business rules runtime

### Code Mentor

- cần `AI_Prompt_Versions`
- cần `AI_Rubric_Versions`
- cần `AI_GroundTruth_Sets`
- cần `AI_Policy_Versions`

### Gatekeeper

- cần `AI_Prompt_Versions`
- có thể có `AI_GroundTruth_Sets`
- có thể có `AI_Rubric_Versions` nhẹ theo policy
- cần `AI_Policy_Versions`

### Extracted Content

- cần `AI_Prompt_Versions`
- thường không cần rubric học thuật
- có thể có test set quality riêng nếu cần
- cần `AI_Policy_Versions`
- production nên đi kèm:
  - `DocumentExtractionDrafts`
  - `DocumentExtractionDraftContents`

### Embedding

- thường không cần prompt
- không cần rubric
- có thể cần benchmark dataset, nhưng không nhất thiết vào `AI_GroundTruth_Sets` nếu chỉ tính cost
- production nên đọc từ `DocumentExtractionDraftContents.approved_markdown` thay vì nhận file thô trực tiếp

### Auto Tagging

- cần prompt nếu tagging bằng LLM
- có thể có taxonomy hoặc tagging guideline
- có thể có ground truth nhỏ để check tag alignment nếu sau này muốn đánh giá accuracy
- cần `AI_Policy_Versions`

## 7. Index đề nghị

### AI_Prompt_Versions

- `{ agent_id: 1, version: 1 }`
- `{ agent_id: 1, is_active: 1 }`

### AI_Rubric_Versions

- `{ rubric_id: 1, version: 1 }`
- `{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }`

### AI_GroundTruth_Sets

- `{ dataset_name: 1, version: 1 }`
- `{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }`

### AI_Policy_Versions

- `{ policy_id: 1, version: 1 }`
- `{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }`

## 8. Khuyến nghị chuyển từ prototype sang production

### Giai đoạn hiện tại

- prompt current đang nằm trong `App_Data/ai-prompts.json`
- rubric current đang nằm ở file JSON
- ground truth current đang nằm trong dataset file JSON
- policy current đang nằm ở `App_Data/*-policy.json`
- lịch sử immutable append-history đang nằm trong `App_Data/ai-config-history/`

### Khi ghép BE tổng

1. import prompt versions vào `AI_Prompt_Versions`
2. import rubric files vào `AI_Rubric_Versions`
3. import benchmark hoặc accuracy datasets vào `AI_GroundTruth_Sets`
4. import policy files vào `AI_Policy_Versions`
5. lưu `prompt_version`, `rubric_version`, `dataset_version`, `policy_version` vào log benchmark/run history nếu cần

## 9. Question Generation còn gì nữa không

Sau những gì đã làm, `Question Generation` vẫn còn các khoảng trống chính:

1. `AI_Rubric_Versions` chưa được nối runtime bằng Mongo repository thật
2. `AI_GroundTruth_Sets` chưa được dùng cho regression hoặc accuracy testing tự động bằng DB thật
3. duplicate check với DB mới đang ở mức heuristic text similarity
4. chưa có vector-based duplicate detection
5. chưa có compile/run verification cho `PE`
6. chưa có difficulty alignment service độc lập
7. chưa có publish workflow cho FE/PE question bank
8. chưa có Mongo repository thật

## Kết luận

Bốn collection versioned này là khung hợp lý để đưa module AI vào BE tổng:

- `AI_Prompt_Versions` = prompt versioning
- `AI_Rubric_Versions` = evaluation criteria
- `AI_GroundTruth_Sets` = benchmark/accuracy source of truth
- `AI_Policy_Versions` = runtime business rules

Từ đây trở đi, các AI function khác có thể được chuyển đổi sang cùng một cách quản lý thống nhất.
