# AI Pipeline Log Storage

## Muc tieu

Tai lieu nay mo ta cach luu log cho module AI theo huong:

- moi lan goi AI la 1 ban ghi log
- 1 pipeline co nhieu ban ghi log con
- co the map thang sang collection `AI_Usage_Logs`
- giu du lieu JSON-first de FE va BE tong de xu ly tiep

## Nguyen tac luu

Khong luu 1 ban ghi tong hop duy nhat cho ca pipeline.

Thay vao do:

1. Moi stage AI tao 1 `AIUsageLogEntry`
2. Cac log con duoc nhom bang `pipeline_run_id`
3. Vong lap generation-review duoc phan biet bang `attempt_index`
4. `payload_data` giu input/output tho de debug va benchmark

## Cac stage hien tai

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

## Mapping agent_id de nghi

- `AI_Gatekeeper`
- `AI_ExtractedContent`
- `AI_Embedding`
- `AI_AutoTagging`
- `AI_Generator`
- `AI_Reviewer`
- `AI_CodeMentor`

Neu ve sau co collection `AI_Agents`, BE tong co the map cac role nay thanh `_id` that.

## Cau truc log de nghi

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
  usage_source: String,        // raw | estimated | mixed

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

  status: String,
  created_at: Date,
  payload_data: Object
}
```

## Cach dung theo tung function

### Gatekeeper

- 1 log / 1 request
- `status`: `supported` hoac `rejected`
- `payload_data.output`: verdict cua AI

### Extracted Content

- 1 log / 1 request
- `status`: `completed`
- `payload_data.output`: text da parse / markdown da normalize

### Embedding + Auto Tagging

- 2 log / 1 chunk
- log 1: `embedding`
- log 2: `auto_tagging`

Ly do tach doi:

- de tinh chinh xac chi phi embedding
- de tinh rieng chi phi tagging
- de de so sanh khi thay doi model 1 trong 2 stage

### Generation + Review

- `SingleAgent`: chi co log `question_generation`
- `SameModelDualRole`: co `question_generation` va `question_review`
- `DualAgent`: co `question_generation` va `question_review`, co the khac provider/model

Neu reviewer reject:

- tang `attempt_index`
- chay lai generator
- chay lai reviewer
- toi da 2 vong

### Code Mentor

- 1 log / 1 request
- `status`: co the la `accepted`, `needs_improvement`, `incorrect`, ...

## Field moi quan trong trong prototype hien tai

### `usage_source`

Cho biet:

- token/cost nay la usage that tu provider
- hay la estimate
- hay tong hop tu nhieu stage co nguon khac nhau

### `model_fields`

Giup chuan hoa field model theo pipeline:

- pipeline don: `primary_model`
- generation/review: `generator_model`, `reviewer_model`
- embedding/tagging: `embedding_model`, `tagging_model`
- extraction: `vision_model`
- mentor: `mentor_model`

### `error`

Dung de benchmark va production test harness:

- model not found
- quota exceeded
- auth fail
- invalid request
- provider overload

### `raw_usage_json`

- luu compact payload usage goc neu provider tra ve
- huu ich khi doi chieu token/cost sau nay

## payload_data nen chua gi

`payload_data` khong phai du lieu tong hop lau dai. Day la du lieu debug ngan han.

Nen chua:

- input request thuc te gui vao AI
- output JSON tra ve
- context chunk da dung
- revision feedback neu la generation-review

Khong nen nhet cac object qua lon neu vuot qua muc can thiet.

## Cach FE co the render

FE khong can parse ca object lon mot cach thu cong.

Chi can:

1. Hien metadata chinh:
   - stage_name
   - provider
   - model_name
   - usage_source
   - input_tokens
   - output_tokens
   - input_cost_usd
   - output_cost_usd
   - cost_usd
   - error neu co
   - latency neu co
2. Cho phep mo rong `payload_data`
3. Gom nhom theo `pipeline_run_id`
4. Sap xep theo `attempt_index`, `created_at`

## Huong map vao collection AI_Usage_Logs

Trong module benchmark/module AI rieng:

- `agent_id` dang la role string
- `triggered_by` dang la user id string
- `_id` dang la `usage-...`

Khi merge vao BE tong:

1. map `usageLogId` -> `_id`
2. map role -> `AI_Agents._id`
3. map user string -> `UserAccount._id`
4. giu nguyen `pipeline_run_id`, `stage_name`, `attempt_index`

## Luu y ve TTL

`payload_data` nen dat TTL xoa sau 30 ngay.

Neu can bao cao dai han:

- khong query truc tiep tren `payload_data`
- tao bang summary/report rieng tu log goc
