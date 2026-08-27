# AI_Policies

## Muc tieu

Collection `AI_Policies` dung de luu cac cau hinh nghiep vu runtime cho tung AI function.

`Policy` khac voi:

- `Prompt`: AI se duoc noi gi
- `Rubric`: AI se duoc danh gia theo tieu chi nao
- `GroundTruth`: AI se duoc test bang bo du lieu nao

`Policy` tra loi cau hoi:

- he thong cho phep gi
- rang buoc nghiep vu nao dang duoc ap dung
- taxonomy/tag rule/threshold/runtime rule nao dang duoc su dung

## Vi sao can collection rieng

Khong nen gop `policy` vao:

- `AI_Prompt_Versions`
- `AI_Rubrics`
- `AI_GroundTruth_Sets`

Ly do:

- prompt la noi dung goi AI
- rubric la tieu chi review/cham
- ground truth la bo du lieu test
- policy la config nghiep vu runtime

Do do, `AI_Policies` nen la mot collection rieng.

## Schema de nghi

```js
{
  _id: ObjectId,

  policy_id: String,              // vd: GATEKEEPER_POLICY, EMBEDDING_TAGGING_POLICY
  policy_name: String,            // ten hien thi tren admin/UI
  function_name: String,          // Gatekeeper | ExtractedContent | EmbeddingTagging | CodeMentor

  subject_code: String,           // nullable, neu policy gan theo mon hoc
  language: String,               // nullable, neu policy phu thuoc ngon ngu

  version: String,                // vd: v1, v2
  is_active: Boolean,

  description: String,            // mo ta ngan muc dich policy
  content_json: Object,           // toan bo noi dung policy co cau truc

  created_at: Date,
  updated_at: Date
}
```

## Giai thich field

- `policy_id`: ma dinh danh on dinh cua policy
- `policy_name`: ten de doc tren admin/UI
- `function_name`: policy nay thuoc AI function nao
- `subject_code`: neu policy dung rieng cho 1 mon hoc thi luu ma mon; neu ap dung chung co the de `null`
- `language`: neu policy phu thuoc ngon ngu thi luu; neu khong thi co the de `null`
- `version`: version cua policy
- `is_active`: policy dang duoc ap dung hay khong
- `description`: mo ta ngan noi dung policy
- `content_json`: phan cau truc chinh cua policy
- `created_at`, `updated_at`: field audit co ban

## Cac function nen dung `AI_Policies`

### 1. Gatekeeper

Vi du `content_json` co the chua:

```js
{
  supported_subjects: [...],
  verdict_rules: {...},
  precheck_rules: {...}
}
```

Dung de:

- whitelist mon hoc
- quy tac `supported | unsupported | ambiguous`
- precheck min word count / empty content

### 2. ExtractedContent

Vi du `content_json` co the chua:

```js
{
  supported_modes: [...],
  image_markers: [...],
  normalization_rules: {...},
  warning_rules: {...}
}
```

Dung de:

- dieu khien normalize text
- warning neu co image ma khong co vision model
- warning neu content rong / sau normalize khong con noi dung huu ich

### 3. EmbeddingTagging

Vi du `content_json` co the chua:

```js
{
  chunk_defaults: {...},
  tagging_rules: {...},
  subject_taxonomy: {...}
}
```

Dung de:

- chunking strategy mac dinh
- retrieval enabled / status mac dinh
- max tag per chunk
- taxonomy tag theo mon hoc

### 4. CodeMentor

Vi du `content_json` co the chua:

```js
{
  supported_languages: [...],
  issue_categories: [...],
  verdicts: [...],
  feedback_rules: {...}
}
```

Dung de:

- rang buoc ngon ngu lap trinh duoc ho tro
- nhom loi duoc phep su dung
- tap verdict duoc phep tra ve
- quy tac feedback co hanh dong cu the

## Nguyen tac du lieu

Nen giu 3 nguyen tac:

1. `policy_id` on dinh
2. `content_json` la source of truth runtime
3. moi lan sua lon thi tang `version`, khong ghi de version cu da benchmark

## Index de nghi

```js
{ policy_id: 1, version: 1 }
{ function_name: 1, is_active: 1 }
{ function_name: 1, subject_code: 1, is_active: 1 }
```

## Mapping voi prototype hien tai

Trong prototype hien tai, policy dang duoc luu bang file JSON payload runtime:

- `App_Data/gatekeeper-policy.json`
- `App_Data/extracted-content-policy.json`
- `App_Data/embedding-tagging-policy.json`
- `App_Data/code-mentor-policy.json`

Luu y quan trong:

- cac file tren **khong phai full DB record**
- cac file tren chi la **payload runtime**
- khi dua vao DB that, payload nay se duoc dat vao field `content_json`

Noi cach khac:

- file prototype = `content_json source`
- DB production = `wrapper record + content_json`

### Vi du mapping

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

### Ket luan mapping

Chot theo `Cach 1`:

- giu DB schema wrapper record
- coi file `App_Data/*-policy.json` la payload runtime
- import payload vao `content_json`

## Quan he voi 3 collection AI con lai

### `AI_Prompt_Versions`

- Prompt cho biet AI se nhan chi dan gi

### `AI_Rubrics`

- Rubric cho biet AI output se duoc danh gia theo tieu chi nao

### `AI_GroundTruth_Sets`

- Ground truth cho biet AI se duoc test bang dataset nao

### `AI_Policies`

- Policy cho biet he thong dang ap rang buoc runtime nao

## Ket luan

Bo 4 collection AI config hop ly de dua vao document DB va production:

- `AI_Prompt_Versions`
- `AI_Rubrics`
- `AI_GroundTruth_Sets`
- `AI_Policies`

Trong do:

- `AI_Policies` la collection can thiet de luu runtime business rules mot cach dung ban chat va de quan ly version ro rang.
