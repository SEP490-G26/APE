# AI_Policies

## Mục tiêu

Collection `AI_Policies` dùng để lưu các cấu hình nghiệp vụ runtime cho từng AI function.

`Policy` khác với:

- `Prompt`: AI sẽ được nói gì
- `Rubric`: AI sẽ được đánh giá theo tiêu chí nào
- `GroundTruth`: AI sẽ được test bằng bộ dữ liệu nào

`Policy` trả lời câu hỏi:

- hệ thống cho phép gì
- ràng buộc nghiệp vụ nào đang được áp dụng
- taxonomy, tag rule, threshold hay runtime rule nào đang được sử dụng

## Vì sao cần collection riêng

Không nên gộp `policy` vào:

- `AI_Prompt_Versions`
- `AI_Rubrics`
- `AI_GroundTruth_Sets`

Lý do:

- prompt là nội dung gọi AI
- rubric là tiêu chí review hoặc chấm
- ground truth là bộ dữ liệu test
- policy là config nghiệp vụ runtime

Do đó, `AI_Policies` nên là một collection riêng.

## Schema đề nghị

```js
{
  _id: ObjectId,

  policy_id: String,              // vd: GATEKEEPER_POLICY, EMBEDDING_TAGGING_POLICY
  policy_name: String,            // tên hiển thị trên admin/UI
  function_name: String,          // Gatekeeper | ExtractedContent | EmbeddingTagging | CodeMentor

  subject_code: String,           // nullable, nếu policy gắn theo môn học
  language: String,               // nullable, nếu policy phụ thuộc ngôn ngữ

  version: String,                // vd: v1, v2
  is_active: Boolean,

  description: String,            // mô tả ngắn mục đích policy
  content_json: Object,           // toàn bộ nội dung policy có cấu trúc

  created_at: Date,
  updated_at: Date
}
```

## Giải thích field

- `policy_id`: mã định danh ổn định của policy
- `policy_name`: tên để đọc trên admin/UI
- `function_name`: policy này thuộc AI function nào
- `subject_code`: nếu policy dùng riêng cho 1 môn học thì lưu mã môn; nếu áp dụng chung có thể để `null`
- `language`: nếu policy phụ thuộc ngôn ngữ thì lưu; nếu không thì có thể để `null`
- `version`: version của policy
- `is_active`: policy đang được áp dụng hay không
- `description`: mô tả ngắn nội dung policy
- `content_json`: phần cấu trúc chính của policy
- `created_at`, `updated_at`: field audit cơ bản

## Các function nên dùng `AI_Policies`

### 1. Gatekeeper

Ví dụ `content_json` có thể chứa:

```js
{
  supported_subjects: [...],
  verdict_rules: {...},
  precheck_rules: {...}
}
```

Dùng để:

- whitelist môn học
- quy tắc `supported | unsupported | ambiguous`
- precheck min word count hoặc empty content

### 2. ExtractedContent

Ví dụ `content_json` có thể chứa:

```js
{
  supported_modes: [...],
  image_markers: [...],
  normalization_rules: {...},
  warning_rules: {...}
}
```

Dùng để:

- điều khiển normalize text
- warning nếu có image mà không có vision model
- warning nếu content rỗng hoặc sau normalize không còn nội dung hữu ích

### 3. EmbeddingTagging

Ví dụ `content_json` có thể chứa:

```js
{
  chunk_defaults: {...},
  tagging_rules: {...},
  subject_taxonomy: {...}
}
```

Dùng để:

- chunking strategy mặc định
- retrieval enabled hoặc status mặc định
- max tag per chunk
- taxonomy tag theo môn học

### 4. CodeMentor

Ví dụ `content_json` có thể chứa:

```js
{
  supported_languages: [...],
  issue_categories: [...],
  verdicts: [...],
  feedback_rules: {...}
}
```

Dùng để:

- ràng buộc ngôn ngữ lập trình được hỗ trợ
- nhóm lỗi được phép sử dụng
- tập verdict được phép trả về
- quy tắc feedback có hành động cụ thể

## Nguyên tắc dữ liệu

Nên giữ 3 nguyên tắc:

1. `policy_id` ổn định
2. `content_json` là source of truth runtime
3. mỗi lần sửa lớn thì tăng `version`, không ghi đè version cũ đã benchmark

## Index đề nghị

```js
{ policy_id: 1, version: 1 }
{ function_name: 1, is_active: 1 }
{ function_name: 1, subject_code: 1, is_active: 1 }
```

## Mapping với prototype hiện tại

Trong prototype hiện tại, policy đang được lưu bằng file JSON payload runtime:

- `App_Data/gatekeeper-policy.json`
- `App_Data/extracted-content-policy.json`
- `App_Data/embedding-tagging-policy.json`
- `App_Data/code-mentor-policy.json`

Lưu ý quan trọng:

- các file trên **không phải full DB record**
- các file trên chỉ là **payload runtime**
- khi đưa vào DB thật, payload này sẽ được đặt vào field `content_json`

Nói cách khác:

- file prototype = `content_json source`
- DB production = `wrapper record + content_json`

### Ví dụ mapping

#### Prototype file

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

#### DB record sau khi import

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

### Kết luận mapping

Chốt theo `Cách 1`:

- giữ DB schema wrapper record
- coi file `App_Data/*-policy.json` là payload runtime
- import payload vào `content_json`

## Quan hệ với 3 collection AI còn lại

### `AI_Prompt_Versions`

- Prompt cho biết AI sẽ nhận chỉ dẫn gì

### `AI_Rubrics`

- Rubric cho biết AI output sẽ được đánh giá theo tiêu chí nào

### `AI_GroundTruth_Sets`

- Ground truth cho biết AI sẽ được test bằng dataset nào

### `AI_Policies`

- Policy cho biết hệ thống đang áp ràng buộc runtime nào

## Kết luận

Bộ 4 collection AI config hợp lý để đưa vào document DB và production:

- `AI_Prompt_Versions`
- `AI_Rubrics`
- `AI_GroundTruth_Sets`
- `AI_Policies`

Trong đó:

- `AI_Policies` là collection cần thiết để lưu runtime business rules một cách đúng bản chất và để quản lý version rõ ràng.
