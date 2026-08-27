# Hướng dẫn lưu log theo pipeline AI

## Mục tiêu

Tài liệu này mô tả cách lưu log cho module AI theo hướng:

- mỗi lần gọi AI là một bản ghi log
- một pipeline có nhiều bản ghi log con
- có thể map thẳng sang collection `AI_Usage_Logs`
- giữ dữ liệu kiểu `JSON-first` để FE và BE tổng xử lý tiếp

## Nguyên tắc lưu

Không lưu một bản ghi tổng hợp duy nhất cho cả pipeline.

Thay vào đó:

1. Mỗi stage AI tạo một `AIUsageLogEntry`.
2. Các log con được nhóm bằng `pipeline_run_id`.
3. Vòng lặp generation-review được phân biệt bằng `attempt_index`.
4. `payload_data` giữ input/output thô để debug và benchmark.

## Các stage hiện tại

### Ingestion pipeline

- `gatekeeper`
- `extract_content`
- `embedding`
- `auto_tagging`

### Generation-review pipeline

- `question_generation`
- `question_review`

### Mentor pipeline

- `code_mentor`

## Mapping `agent_id` đề nghị

- `AI_Gatekeeper`
- `AI_ExtractedContent`
- `AI_Embedding`
- `AI_AutoTagging`
- `AI_Generator`
- `AI_Reviewer`
- `AI_CodeMentor`

Nếu về sau có collection `AI_Agents`, BE tổng có thể map các role này thành `_id` thật.

## Cấu trúc log đề nghị

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

  status: String,
  created_at: Date,
  payload_data: Object
}
```

## Cách dùng theo từng function

### Gatekeeper

- 1 log cho 1 request
- `status`: `supported` hoặc `rejected`
- `payload_data.output`: verdict của AI

### Extracted Content

- 1 log cho 1 request
- `status`: `completed`
- `payload_data.output`: text đã parse hoặc markdown đã normalize

### Embedding + Auto Tagging

- 2 log cho 1 chunk
- log 1: `embedding`
- log 2: `auto_tagging`

Lý do tách đôi:

- để tính chính xác chi phí embedding
- để tính riêng chi phí tagging
- để dễ so sánh khi thay đổi model ở một trong hai stage

### Generation + Review

- `SingleAgent`: chỉ có log `question_generation`
- `SameModelDualRole`: có `question_generation` và `question_review`
- `DualAgent`: có `question_generation` và `question_review`, có thể khác provider/model

Nếu reviewer reject:

- tăng `attempt_index`
- chạy lại generator
- chạy lại reviewer
- tối đa 2 vòng

### Code Mentor

- 1 log cho 1 request
- `status`: có thể là `accepted`, `needs_improvement`, `incorrect`

## Trường quan trọng trong prototype hiện tại

### `usage_source`

Cho biết:

- token/cost này là usage thật từ provider
- hay là estimate
- hay là tổng hợp từ nhiều stage có nguồn khác nhau

### `model_fields`

Giúp chuẩn hoá field model theo pipeline:

- pipeline đơn: `primary_model`
- generation/review: `generator_model`, `reviewer_model`
- embedding/tagging: `embedding_model`, `tagging_model`
- extraction: `vision_model`
- mentor: `mentor_model`

### `error`

Dùng để benchmark và production test harness:

- model not found
- quota exceeded
- auth fail
- invalid request
- provider overload

### `raw_usage_json`

- lưu compact payload usage gốc nếu provider trả về
- hữu ích khi đối chiếu token/cost sau này

## `payload_data` nên chứa gì

`payload_data` không phải dữ liệu tổng hợp lâu dài. Đây là dữ liệu debug ngắn hạn.

Nên chứa:

- input request thực tế gửi vào AI
- output JSON trả về
- context chunk đã dùng
- revision feedback nếu là generation-review

Không nên nhét object quá lớn nếu vượt quá mức cần thiết.

## Cách FE có thể render

FE không cần parse cả object lớn theo cách thủ công.

Chỉ cần:

1. Hiện metadata chính: `stage_name`, `provider`, `model_name`, `usage_source`, `input_tokens`, `output_tokens`, `input_cost_usd`, `output_cost_usd`, `cost_usd`, `error`, `latency`.
2. Cho phép mở rộng `payload_data`.
3. Gom nhóm theo `pipeline_run_id`.
4. Sắp xếp theo `attempt_index`, `created_at`.

## Hướng map vào collection `AI_Usage_Logs`

Trong module AI hiện tại:

- `agent_id` đang là role string
- `triggered_by` đang là user id string
- `_id` đang là `usage-...`

Khi merge vào BE tổng:

1. map `usageLogId` sang `_id`
2. map role sang `AI_Agents._id`
3. map user string sang `UserAccount._id`
4. giữ nguyên `pipeline_run_id`, `stage_name`, `attempt_index`

## Lưu ý về TTL

`payload_data` nên đặt TTL xoá sau 30 ngày.

Nếu cần báo cáo dài hạn:

- không query trực tiếp trên `payload_data`
- tạo bảng summary/report riêng từ log gốc
