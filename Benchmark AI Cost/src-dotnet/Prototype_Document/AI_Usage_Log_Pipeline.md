# AI Usage Log Pipeline

## Muc tieu

Tai lieu nay mo ta cach luu log cho cac pipeline AI, dac biet la luong `Generation + Review`.

Muc tieu cua log:

- do chi phi AI theo tung lan goi
- do token tung stage
- phan tich do chinh xac theo model
- debug pipeline khi generation/review sai
- phuc vu benchmark va van hanh production

## Nguyen tac

Khong luu 1 log tong hop duy nhat cho ca pipeline.

Thay vao do:

- moi lan goi AI la 1 `AI_Usage_Log`
- pipeline co the co nhieu log con
- neu can, FE/BE co the tong hop lai theo `pipeline_run_id`

## Collection AI_Usage_Logs cap nhat

De ho tro pipeline nhieu stage, nen mo rong collection thanh:

```js
{
  _id: ObjectId,
  triggered_by: ObjectId,      // UserAccount._id
  agent_id: ObjectId,          // AI_Agents._id

  pipeline_run_id: String,     // gom nhieu log con ve 1 pipeline
  stage_name: String,          // question_generation | question_review | code_mentor ...
  attempt_index: Number,       // 1 | 2

  provider: String,            // openai | gemini | cohere
  model_name: String,          // gpt-4o | gemini-2.5-flash | command-r7b...

  credits_deducted: Number,
  input_tokens: Number,
  output_tokens: Number,
  tokens_used: Number,
  input_cost_usd: Number,
  output_cost_usd: Number,
  cost_usd: Number,
  usage_source: String,        // raw | estimated | mixed
  status: String,              // generated | accepted | needs_revision | failed

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

  raw_usage_json: String,      // nullable, compact provider usage snapshot

  created_at: Date,
  payload_data: Object         // tu xoa sau 30 ngay
}
```

## Tai sao can them cac field moi

`usage_source`:

- can de phan biet usage/cost that va estimate

`model_fields`:

- can de tong hop dung model o cac pipeline co nhieu role

`error`:

- can de benchmark quota, provider overload, sai model, auth, bad request

`raw_usage_json`:

- can de doi chieu lai token/cost khi viet bao cao hoac nghiem thu

### pipeline_run_id

Dung de nhom:

- AI1 attempt 1
- AI2 attempt 1
- AI1 attempt 2
- AI2 attempt 2

vao cung 1 flow.

### stage_name

Cho biet ban ghi nay la:

- gatekeeper
- question_generation
- question_review
- code_mentor

### attempt_index

Rat quan trong cho vong lap generation-review.

### provider / model_name

Can de:

- so sanh model
- tong hop chi phi tung provider
- phan tich do chinh xac theo tung model

### input_tokens / output_tokens / input_cost_usd / output_cost_usd

Day la nhom field nen bo sung vao collection thuc te.

Ly do:

- benchmark hien tai can tach rieng gia input va output
- khi sang FE report, khong can parse lai `payload_data`
- de doi chieu truc tiep voi bang benchmark trong Excel

## Log cho pipeline Generation + Review

### Single Agent

Chi co 1 log moi attempt:

1. `question_generation`

Reviewer bi bo qua va system auto-accept.

### Same Model Dual Role

Moi attempt co 2 log:

1. `question_generation`
2. `question_review`

Provider/model giong nhau, nhung `agent_id` khac nhau do role khac nhau.

### Dual Agent

Moi attempt co 2 log:

1. `question_generation`
2. `question_review`

Provider/model co the khac nhau.

## payload_data de nghi cho generation

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

## payload_data de nghi cho review

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

Trong cac pipeline khac:

- `AI_Gatekeeper`
- `AI_ExtractedContent`
- `AI_Embedding`
- `AI_AutoTagging`

## Trang thai hien tai trong module

Module hien tai chua luu DB that, nhung da tra ve object `AIUsageLogEntry` tu BE cho tat ca cac function chinh:

- FE render
- benchmark
- de sau nay map thang vao collection `AI_Usage_Logs`

Bao gom:

- gatekeeper
- extracted content
- embedding
- auto tagging
- generation
- review
- code mentor
- full pipeline gom usage log cua ingestion + generation/review

## Luu y

`payload_data` co the lon, nen chi luu nhap va set TTL xoa sau 30 ngay la hop ly.

Neu can tong hop bao cao lau dai, nen co bang summary rieng thay vi giu toan bo payload mai mai.
