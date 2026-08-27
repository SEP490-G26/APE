# App_Data Source Of Truth vs DB Mapping

## Muc tieu

Tai lieu nay tong hop:

- cac file trong `App_Data` dang dong vai tro source of truth tam thoi trong prototype
- khi chuyen vao DB that thi chung se duoc map nhu the nao
- diem khac nhau giua file prototype va record DB wrapper

## 1. Nhom Prompt

### File prototype

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

### Ban chat

- day la file wrapper list prompt
- moi item da gan gan voi 1 record prompt

### Collection DB tuong ung

- `AI_Prompt_Versions`

### Mapping

| Prototype | DB |
|---|---|
| `Key` | `agent_id` + `key logic` hoac map sang agent role |
| `Version` | `version` |
| `Description` | `description` neu DB co them field nay, hoac bo qua |
| `SystemPrompt` | `system_prompt` |
| `UserPrompt` | co the dua vao `system_prompt` hoac tach thanh field phu neu mo rong schema |
| `IsActive` | `is_active` |
| `UpdatedAt` | `updated_at` |

## 2. Nhom Policy

### File prototype

- `App_Data/gatekeeper-policy.json`
- `App_Data/extracted-content-policy.json`
- `App_Data/embedding-tagging-policy.json`
- `App_Data/code-mentor-policy.json`

### Ban chat

- day **khong phai full DB record**
- day la **payload runtime**

### Collection DB tuong ung

- `AI_Policies`

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

### Ket luan policy

- Prototype file = `content_json source`
- DB = `wrapper record + content_json`

## 3. Nhom Rubric

### File prototype

- `App_Data/ai-rubrics/*.json`

Bao gom:

- `GATEKEEPER.json`
- `EXTRACTED_CONTENT.json`
- `EMBEDDING_TAGGING.json`
- `CODE_MENTOR.json`
- `C_FE.json`, `C_PE.json`, `JAVA_OOP_FE.json`, ...

### Ban chat

- moi file gan gan la 1 rubric payload hoan chinh

### Collection DB tuong ung

- `AI_Rubrics`

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
  content_json: { ...payload cua file... },
  is_active: true,
  created_at: Date,
  updated_at: Date
}
```

### Ket luan rubric

- File prototype = co the xem la payload source
- DB = wrapper record + `content_json`

## 4. Nhom Ground Truth

### File prototype

- `App_Data/ai-groundtruth/*.json`

Bao gom:

- `GATEKEEPER_CORE.json`
- `EXTRACTED_CONTENT_CORE.json`
- `EMBEDDING_TAGGING_CORE.json`
- `CODE_MENTOR_CORE.json`
- `QGEN_*.json`

### Ban chat

- moi file la 1 dataset payload

### Collection DB tuong ung

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

Neu giu dung wrapper design, payload file hien tai se duoc dua vao phan:

- `dataset_id`
- `dataset_name`
- `function_name`
- `description`
- `evaluation_cases`

va bo sung them metadata wrapper con thieu.

## 5. Su khac nhau cot loi giua Prototype va DB That

### Prototype file

- uu tien de sua nhanh
- uu tien cho runtime doc truc tiep
- khong nhat thiet day du metadata admin/audit

### DB that

- uu tien versioning
- uu tien admin/filter/query
- uu tien audit
- uu tien bo 4 collection dong nhat

## 6. Nhom Runtime Artifacts

Ngoai 4 nhom source-of-truth cau hinh ben tren, `App_Data` hien tai con chua runtime artifacts.

Nhung artifact nay **khong nen map 1:1** vao bo collection cau hinh AI.

### 6.1 Run history

Thu muc:

- `App_Data/run-history/{function-name}/*.json`

Ban chat:

- day la artifact runtime/benchmark
- luu request/response/totals
- phuc vu audit, benchmark, production test harness

Trang thai hien tai cua moi run file da co them:

- `modelFields`
- `usageSource`
- `error`
- `rawUsageJson` trong usage log neu provider tra usage that

Huong map vao BE that:

- map vao `AI_Usage_Logs`
- va/hoac them collection session/report rieng neu muon luu benchmark dai han

### 6.2 Upload artifacts

Thu muc:

- `App_Data/uploads/{date}/...`

Ban chat:

- day la file ingest tam thoi cho operator console
- phuc vu parser `docx/pptx/pdf/image`
- khong phai source-of-truth cau hinh

Huong map vao BE that:

- object storage / file storage rieng
- document metadata se nam o collection document that cua he thong

## 7. Chot theo huong dung

Huong duoc chot:

1. `App_Data` la source of truth tam thoi cho prototype
2. `AI_Prompt_Versions`, `AI_Policies`, `AI_Rubrics`, `AI_GroundTruth_Sets` la bo collection DB that
3. Policy/rubric/ground truth se map theo **wrapper record**
4. `content_json` la noi chua payload runtime
5. `run-history` va `uploads` duoc xem la runtime artifacts, khong thuoc bo collection cau hinh

## Ket luan

Khi dua vao DB that:

- khong copy file JSON 1:1 lam document production
- can import/chuyen doi sang wrapper schema

Rieng voi `AI_Policies`:

- day la collection ma prototype file va DB khac nhau ro nhat
- file prototype la payload
- DB la wrapper + payload
