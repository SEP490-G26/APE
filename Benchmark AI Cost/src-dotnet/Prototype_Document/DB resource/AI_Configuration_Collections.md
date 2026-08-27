# AI Configuration Collections

## Muc tieu

Tai lieu nay chot khung collection de dua vao DB that cho nhom cau hinh AI:

- `AI_Rubrics`
- `AI_GroundTruth_Sets`
- `AI_Prompt_Versions`

Muc tieu la:

- prototype va BE tong co cung mot huong field
- prompt, rubric, va ground truth co version ro rang
- de benchmark va production co the trace lai dang dung version nao

## 1. AI_Prompt_Versions

Collection nay la ban doi ten/chuan hoa tu collection `Prompts` ma nhom hien da co.

### Muc dich

- luu version prompt cua tung AI agent
- cho phep bat/tat prompt version
- de trace benchmark/prod dang dung prompt nao

### Schema de nghi

```js
{
  _id: ObjectId,
  agent_id: ObjectId,          // ref AI_Agents._id
  version: String,             // vd: v1, v2, genrev-2026-07
  is_active: Boolean,
  system_prompt: String,
  variables: [String],         // vd: subject, question_type, chunks_json
  created_at: Date,
  updated_at: Date
}
```

### Mapping voi prototype hien tai

Trong prototype `.NET`, object gan tuong ung la:

- `AiPromptVersionRecord`

### Ghi chu

- moi `agent_id` co the co nhieu version
- chi nen co 1 version active tai 1 thoi diem cho 1 `agent_id`
- benchmark co the override prompt version de so sanh

## 2. AI_Rubrics

Collection nay dung de luu rubric co cau truc cho cac tac vu can danh gia theo tieu chi hoc thuat/nghiep vu.

### Tac vu chinh nen dung

- `Question Generation / Review`
- `Code Mentor`

Co the mo rong sau cho:

- `Gatekeeper` policy rubric nhe

### Schema de nghi

```js
{
  _id: ObjectId,
  rubric_id: String,           // vd: C_FE, JAVA_OOP_PE, CODE_MENTOR_JAVA
  function_name: String,       // QuestionGenerationReview | CodeMentor
  subject_code: String,        // C | JAVA_OOP | DSA_JAVA
  question_type: String,       // FE | PE | null neu khong ap dung
  version: String,             // vd: v1, v2
  language: String,            // en
  is_active: Boolean,
  content_json: Object,        // toan bo rubric JSON co cau truc
  created_at: Date,
  updated_at: Date
}
```

### content_json nen chua gi

Voi `Question Generation / Review`, `content_json` nen chua:

- `difficulty_definitions`
- `review_checks`
- `difficulty_mismatch_signals`
- `acceptance_rules`
- `regeneration_hints`

Voi `Code Mentor`, `content_json` co the chua:

- `verdict_levels`
- `issue_categories`
- `feedback_style_rules`
- `difficulty_expectations`
- `suggestion_quality_rules`

### Mapping voi prototype hien tai

Trong prototype `.NET`, object gan tuong ung la:

- `AiRubricRecord`

### Ghi chu

- file JSON hien tai trong `App_Data/ai-rubrics/` la source of truth tam thoi
- khi dua vao production, cac file nay co the import vao `AI_Rubrics`

## 3. AI_GroundTruth_Sets

Collection nay dung de luu bo ground truth de:

- benchmark
- accuracy evaluation
- regression test

### Tac vu chinh nen dung

- `Question Generation / Review`
- `Code Mentor`
- co the co bo nhe cho `Gatekeeper`

### Schema de nghi

```js
{
  _id: ObjectId,
  dataset_name: String,        // vd: mentor_20_cases_v1
  function_name: String,       // Gatekeeper | QuestionGenerationReview | CodeMentor
  subject_code: String,        // C | JAVA_OOP | DSA_JAVA | MIXED
  question_type: String,       // FE | PE | null neu khong ap dung
  version: String,             // vd: v1
  language: String,            // en | vi | mixed
  is_active: Boolean,
  content_json: Object,        // toan bo dataset / expected answers / labels
  created_at: Date,
  updated_at: Date
}
```

### content_json nen chua gi

Voi `Question Generation / Review`:

- case list
- source chunks hoac dataset reference
- expected difficulty
- expected topic tags
- expected accept/reject note

Voi `Code Mentor`:

- problem statement
- student code
- expected issues
- expected verdict
- expected suggestion direction

Voi `Gatekeeper`:

- file case id
- supported / unsupported / ambiguous
- expected domain

### Mapping voi prototype hien tai

Trong prototype `.NET`, object gan tuong ung la:

- `AiGroundTruthSetRecord`

## 4. Quan he giua 3 collection

### AI_Prompt_Versions

Tra loi cau hoi:

- agent nay dang noi gi voi AI

### AI_Rubrics

Tra loi cau hoi:

- agent nay danh gia / quy chuan theo bo tieu chi nao

### AI_GroundTruth_Sets

Tra loi cau hoi:

- agent nay duoc test/cham dua tren bo du lieu chuan nao

## 5. Collection nao lien quan toi chuc nang nao

### Question Generation / Review

- can `AI_Prompt_Versions`
- can `AI_Rubrics`
- can `AI_GroundTruth_Sets`

### Code Mentor

- can `AI_Prompt_Versions`
- can `AI_Rubrics`
- can `AI_GroundTruth_Sets`

### Gatekeeper

- can `AI_Prompt_Versions`
- co the co `AI_GroundTruth_Sets`
- co the co `AI_Rubrics` nhe theo policy

### Extracted Content

- can `AI_Prompt_Versions`
- thuong khong can rubric hoc thuat
- co the co test set quality rieng neu can

### Embedding

- thuong khong can prompt
- khong can rubric
- co the can benchmark dataset, nhung khong nhat thiet vao `AI_GroundTruth_Sets` neu chi tinh cost

### Auto Tagging

- can prompt neu tagging bang LLM
- co the co taxonomy/tagging guideline
- co the co ground truth nho de check tag alignment neu sau nay muon danh gia accuracy

## 6. Index de nghi

### AI_Prompt_Versions

- `{ agent_id: 1, version: 1 }`
- `{ agent_id: 1, is_active: 1 }`

### AI_Rubrics

- `{ rubric_id: 1, version: 1 }`
- `{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }`

### AI_GroundTruth_Sets

- `{ dataset_name: 1, version: 1 }`
- `{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }`

## 7. Khuyen nghi chuyen tu prototype sang production

### Giai doan hien tai

- prompt co the van load tu file/registry tam
- rubric source dang nam o file JSON
- ground truth dang nam trong dataset file JSON

### Khi ghep BE tong

1. import prompt versions vao `AI_Prompt_Versions`
2. import rubric files vao `AI_Rubrics`
3. import benchmark/accuracy datasets vao `AI_GroundTruth_Sets`
4. luu `version`/`rubric_id`/`dataset_name` vao log benchmark neu can

## 8. Question Generation con gi nua khong

Sau nhung gi da lam, `Question Generation` van con cac khoang trong chinh:

1. `AI_Rubrics` chua duoc noi runtime vao code review flow
2. `AI_GroundTruth_Sets` chua duoc dung cho regression/accuracy testing tu dong
3. duplicate check voi DB moi dang o muc heuristic text similarity
4. chua co vector-based duplicate detection
5. chua co compile/run verification cho `PE`
6. chua co difficulty alignment service doc lap
7. chua co publish workflow cho FE/PE question bank
8. chua co Mongo repository that

## Ket luan

Ba collection nay la khung hop ly de dua module AI vao BE tong:

- `AI_Prompt_Versions` = prompt versioning
- `AI_Rubrics` = evaluation/policy criteria
- `AI_GroundTruth_Sets` = benchmark/accuracy source of truth

Tu day tro di, cac AI function khac co the duoc chuyen doi sang cung mot cach quan ly thong nhat.
