# Hướng dẫn đối chiếu `App_Data` và DB thật

## Mục tiêu

Tài liệu này tổng hợp:

- các file trong `App_Data` đang đóng vai trò source of truth tạm thời trong prototype
- khi chuyển vào DB thật thì chúng sẽ được map như thế nào
- điểm khác nhau giữa file prototype và record DB wrapper

## 1. Nhóm Prompt

### File prototype

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- `src-dotnet/Ape.AiModule.Api/App_Data/ai-config-history/prompts/`

### Bản chất

- đây là file wrapper list prompt
- mỗi item đã gần giống với 1 record prompt
- file `ai-prompts.json` là current active snapshot
- thư mục `ai-config-history/prompts/` là lịch sử immutable append-history

### Collection DB tương ứng

- `AI_Prompt_Versions`

### Mapping

| Prototype | DB |
|---|---|
| `Key` | `agent_id` + `key logic` hoặc map sang agent role |
| `Version` | `version` |
| `Description` | `description` nếu DB có thêm field này, hoặc bỏ qua |
| `SystemPrompt` | `system_prompt` |
| `UserPrompt` | có thể đưa vào `system_prompt` hoặc tách thành field phụ nếu mở rộng schema |
| `IsActive` | `is_active` |
| `UpdatedAt` | `updated_at` |

### Ghi chú versioning

- `ai-prompts.json` không còn là nơi duy nhất giữ prompt
- mỗi lần `PUT /api/ai-module/prompts/{key}` sẽ:
  - cập nhật current snapshot
  - append thêm 1 file immutable vào `ai-config-history/prompts/{key}/`
- khi đưa vào DB thật, nên import cả current và history

## 2. Nhóm Policy

### File prototype

- `App_Data/gatekeeper-policy.json`
- `App_Data/extracted-content-policy.json`
- `App_Data/embedding-tagging-policy.json`
- `App_Data/code-mentor-policy.json`
- `App_Data/ai-config-history/policies/`

### Bản chất

- đây **không phải full DB record**
- đây là **payload runtime**
- file `*-policy.json` là current active payload
- thư mục `ai-config-history/policies/` là append-history snapshot

### Collection DB tương ứng

- `AI_Policy_Versions`

### Mapping

Prototype file:

```json
{
  "policy_id": "gatekeeper_policy_v1",
  "version": "v1",
  "function_name": "Gatekeeper",
  "supported_subjects": [...],
  "verdict_rules": {...},
  "precheck_rules": {...}
}
```

DB record:

```js
{
  _id: ObjectId,
  policy_id: "gatekeeper_policy_v1",
  policy_name: "Gatekeeper Whitelist Policy",
  function_name: "Gatekeeper",
  subject_code: null,
  language: "mixed",
  version: "v1",
  is_active: true,
  description: "Whitelist and precheck runtime policy for gatekeeper.",
  content_json: {
    supported_subjects: [...],
    verdict_rules: {...},
    precheck_rules: {...}
  },
  created_at: Date,
  updated_at: Date
}
```

### Kết luận policy

- prototype file = `content_json source`
- DB = `wrapper record + content_json`
- cần import cả snapshot history nếu muốn audit đầy đủ

## 3. Nhóm Rubric

### File prototype

- `App_Data/ai-rubrics/*.json`
- `App_Data/ai-config-history/rubrics/`

Bao gồm:

- `GATEKEEPER.json`
- `EXTRACTED_CONTENT.json`
- `EMBEDDING_TAGGING.json`
- `CODE_MENTOR.json`
- `C_FE.json`, `C_PE.json`, `JAVA_OOP_FE.json`

### Bản chất

- mỗi file gần như là 1 rubric payload hoàn chỉnh
- file trong `ai-rubrics/` là current active snapshot
- thư mục `ai-config-history/rubrics/` là append-history snapshot

### Collection DB tương ứng

- `AI_Rubric_Versions`

### Mapping

DB record wrapper:

```js
{
  _id: ObjectId,
  rubric_id: "...",
  rubric_name: "...",
  function_name: "...",
  subject_code: "...",
  question_type: "...",
  language: "en",
  version: "v1",
  institution_context: "...",
  description: "...",
  content_json: { ...payload của file... },
  is_active: true,
  created_at: Date,
  updated_at: Date
}
```

### Kết luận rubric

- file prototype = có thể xem là payload source
- DB = wrapper record + `content_json`
- không nên chỉ import bản current nếu cần audit benchmark

## 4. Nhóm Ground Truth

### File prototype

- `App_Data/ai-groundtruth/*.json`
- `App_Data/ai-config-history/groundtruth/`

Bao gồm:

- `GATEKEEPER_CORE.json`
- `EXTRACTED_CONTENT_CORE.json`
- `EMBEDDING_TAGGING_CORE.json`
- `CODE_MENTOR_CORE.json`
- `QGEN_*.json`

### Bản chất

- mỗi file là 1 dataset payload
- file trong `ai-groundtruth/` là current active dataset
- thư mục `ai-config-history/groundtruth/` là append-history snapshot

### Collection DB tương ứng

- `AI_GroundTruth_Sets`

### Mapping

DB record wrapper:

```js
{
  _id: ObjectId,
  dataset_id: "...",
  dataset_name: "...",
  function_name: "...",
  subject_code: "...",
  question_type: "...",
  language: "...",
  version: "v1",
  institution_context: "...",
  description: "...",
  evaluation_cases: [...],
  is_active: true,
  created_at: Date,
  updated_at: Date
}
```

Nếu giữ đúng wrapper design, payload file hiện tại sẽ được đưa vào phần:

- `dataset_id`
- `dataset_name`
- `function_name`
- `description`
- `evaluation_cases`

và bổ sung thêm metadata wrapper còn thiếu.

## 5. Khác nhau cốt lõi giữa prototype và DB thật

### Prototype file

- ưu tiên để sửa nhanh
- ưu tiên cho runtime đọc trực tiếp
- không nhất thiết đầy đủ metadata admin/audit

### DB thật

- ưu tiên versioning
- ưu tiên admin/filter/query
- ưu tiên audit
- ưu tiên bộ 4 collection đồng nhất
- ưu tiên trace version cho mọi run

## 6. Nhóm runtime artifacts

Ngoài 4 nhóm source-of-truth cấu hình bên trên, `App_Data` hiện tại còn chứa runtime artifacts.

Những artifact này **không nên map 1:1** vào bộ collection cấu hình AI.

### 6.1 Run history

Thư mục:

- `App_Data/run-history/{function-name}/*.json`

Bản chất:

- đây là artifact runtime/benchmark
- lưu request/response/totals
- phục vụ audit, benchmark, production test harness

Trạng thái hiện tại của mỗi run file đã có thêm:

- `modelFields`
- `usageSource`
- `error`
- `rawUsageJson` trong usage log nếu provider trả usage thật

Hướng map vào BE thật:

- map vào `AI_Usage_Logs`
- hoặc thêm collection session/report riêng nếu muốn lưu benchmark dài hạn

### 6.2 Upload artifacts

Thư mục:

- `App_Data/uploads/{date}/...`

Bản chất:

- đây là file ingest tạm thời cho operator console
- phục vụ parser `docx/pptx/pdf/image`
- không phải source-of-truth cấu hình

Hướng map vào BE thật:

- object storage hoặc file storage riêng
- document metadata sẽ nằm ở collection document thật của hệ thống

## 7. Hướng chốt hiện tại

1. `App_Data` là source of truth current cho prototype
2. `App_Data/ai-config-history/` là append-history immutable cho prototype
3. `AI_Prompt_Versions`, `AI_Policy_Versions`, `AI_Rubric_Versions`, `AI_GroundTruth_Sets` là bộ collection DB thật
4. policy/rubric/ground truth sẽ map theo **wrapper record**
5. `content_json` là nơi chứa payload runtime
6. `run-history` và `uploads` được xem là runtime artifacts, không thuộc bộ collection cấu hình

## Kết luận

Khi đưa vào DB thật:

- không copy file JSON 1:1 làm document production
- cần import hoặc chuyển đổi sang wrapper schema

Riêng với `AI_Policies`:

- đây là collection mà prototype file và DB khác nhau rõ nhất
- file prototype là payload
- DB là wrapper + payload
