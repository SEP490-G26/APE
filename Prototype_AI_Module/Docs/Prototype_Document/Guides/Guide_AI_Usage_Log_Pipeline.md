# Hướng dẫn collection log sử dụng AI theo pipeline

## Mục tiêu

Tài liệu này mô tả cách lưu log cho các pipeline AI, đặc biệt là luồng `Generation + Review`.

Mục tiêu của log:

- đo chi phí AI theo từng lần gọi
- đo token từng stage
- phân tích độ chính xác theo model
- debug pipeline khi generation/review sai
- phục vụ benchmark và vận hành production

## Nguyên tắc

Không lưu 1 log tổng hợp duy nhất cho cả pipeline.

Thay vào đó:

- mỗi lần gọi AI là 1 `AI_Usage_Log`
- pipeline có thể có nhiều log con
- nếu cần, FE/BE có thể tổng hợp lại theo `pipeline_run_id`

## Collection `AI_Usage_Logs` cập nhật

Để hỗ trợ pipeline nhiều stage, nên mở rộng collection thành:

```js
{
  _id: ObjectId,
  triggered_by: ObjectId,
  agent_id: ObjectId,

  pipeline_run_id: String,
  stage_name: String,
  attempt_index: Number,

  provider: String,
  model_name: String,

  credits_deducted: Number,
  input_tokens: Number,
  output_tokens: Number,
  tokens_used: Number,
  input_cost_usd: Number,
  output_cost_usd: Number,
  cost_usd: Number,
  usage_source: String,
  status: String,

  model_fields: {
    provider: String,
    primary_model: String,
    generator_model: String,
    reviewer_model: String,
    embedding_model: String,
    tagging_model: String,
    vision_model: String,
    mentor_model: String
  },

  error: {
    category: String,
    code: String,
    provider_status: Number,
    retryable: Boolean,
    stage: String,
    raw_message: String
  },

  raw_usage_json: String,

  created_at: Date,
  payload_data: Object
}
```

## Tại sao cần thêm các field mới

### `usage_source`

- cần để phân biệt usage/cost thật và estimate

### `model_fields`

- cần để tổng hợp đúng model ở các pipeline có nhiều role

### `error`

- cần để benchmark quota, provider overload, sai model, auth, bad request

### `raw_usage_json`

- cần để đối chiếu lại token/cost khi viết báo cáo hoặc nghiệm thu

### `pipeline_run_id`

Dùng để nhóm:

- AI1 attempt 1
- AI2 attempt 1
- AI1 attempt 2
- AI2 attempt 2

vào cùng 1 flow.

### `stage_name`

Cho biết bản ghi này là:

- gatekeeper
- question_generation
- question_review
- code_mentor

### `attempt_index`

Rất quan trọng cho vòng lặp generation-review.

### `provider` và `model_name`

Cần để:

- so sánh model
- tổng hợp chi phí từng provider
- phân tích độ chính xác theo từng model

### `input_tokens`, `output_tokens`, `input_cost_usd`, `output_cost_usd`

Đây là nhóm field nên bổ sung vào collection thực tế.

Lý do:

- benchmark hiện tại cần tách riêng giá input và output
- khi sang FE report, không cần parse lại `payload_data`
- để đối chiếu trực tiếp với bảng benchmark trong Excel

## Log cho pipeline Generation + Review

### Single Agent

Chỉ có 1 log mỗi attempt:

1. `question_generation`

Reviewer bị bỏ qua và system auto-accept.

### Same Model Dual Role

Mỗi attempt có 2 log:

1. `question_generation`
2. `question_review`

Provider/model giống nhau, nhưng `agent_id` khác nhau do role khác nhau.

### Dual Agent

Mỗi attempt có 2 log:

1. `question_generation`
2. `question_review`

Provider/model có thể khác nhau.

## `payload_data` đề nghị cho generation

```json
{
  "pipeline_type": "generation_review",
  "subject": "JAVA_OOP",
  "question_type": "FE",
  "difficulty": "Medium",
  "result": "generated",
  "input": {
    "mode": "DualAgent",
    "count": 2,
    "chunks": [],
    "revisionFeedback": "...",
    "previousQuestions": []
  },
  "output": [
    {}
  ]
}
```

## `payload_data` đề nghị cho review

```json
{
  "pipeline_type": "generation_review",
  "subject": "JAVA_OOP",
  "question_type": "FE",
  "difficulty": "Medium",
  "result": "needs_revision",
  "input": {
    "mode": "DualAgent",
    "chunks": [],
    "questions": []
  },
  "output": {
    "reviewStatus": "needs_revision",
    "issues": [],
    "suggestions": [],
    "score": 0.61,
    "schemaValid": true,
    "contentGrounded": false,
    "needsRevision": true
  }
}
```

## Mapping agent

Trong pipeline generation-review:

- `AI_Generator`
- `AI_Reviewer`

Trong pipeline mentor:

- `AI_CodeMentor`

Trong các pipeline khác:

- `AI_Gatekeeper`
- `AI_ExtractedContent`
- `AI_Embedding`
- `AI_AutoTagging`

## Trạng thái hiện tại trong module

Module hiện tại chưa lưu DB thật, nhưng đã trả về object `AIUsageLogEntry` từ BE cho tất cả function chính để:

- FE render
- benchmark
- map thẳng vào collection `AI_Usage_Logs` sau này

Bao gồm:

- gatekeeper
- extracted content
- embedding
- auto tagging
- generation
- review
- code mentor
- full pipeline gồm usage log của ingestion + generation/review

## Lưu ý

`payload_data` có thể lớn, nên chỉ lưu nháp và đặt TTL xoá sau 30 ngày là hợp lý.

Nếu cần tổng hợp báo cáo dài hạn, nên có bảng summary riêng thay vì giữ toàn bộ payload mãi mãi.
