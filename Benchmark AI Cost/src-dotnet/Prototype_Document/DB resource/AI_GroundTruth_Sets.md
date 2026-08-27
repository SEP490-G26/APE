# AI_GroundTruth_Sets

## Muc tieu

Collection `AI_GroundTruth_Sets` luu bo du lieu chuan dung de:

- regression test
- benchmark co doi chieu
- accuracy evaluation
- so sanh model theo cung mot bo case

Trong prototype hien tai, nguon tam thoi dang nam o:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/`

Khi dua vao BE that, cac file JSON nay co the import vao collection nay.

## Schema de nghi

```js
{
  _id: ObjectId,

  dataset_id: String,              // id logic cua dataset, vd: QGEN_C_FE
  dataset_name: String,            // ten de doc trong UI / bao cao
  function_name: String,           // QuestionGenerationReview | CodeMentor | Gatekeeper

  subject_code: String,            // C | JAVA_OOP | DSA_JAVA | MIXED
  question_type: String,           // FE | PE | null neu khong ap dung
  language: String,                // en | vi | mixed

  version: String,                 // vd: v1
  institution_context: String,     // FPT University, PRF / PRO / CSD style...
  description: String,             // mo ta ngan muc dich dataset

  evaluation_cases: [Object],      // danh sach test case chi tiet

  is_active: Boolean,
  created_at: Date,
  updated_at: Date
}
```

## Giai thich field

- `dataset_id`: ma dataset on dinh de runtime goi ra dung bo test
- `dataset_name`: ten hien thi cho nguoi dung / bao cao
- `function_name`: dataset thuoc chuc nang AI nao
- `subject_code`: mon hoc hoac nhom mon hoc
- `question_type`: dung cho FE/PE; neu Gatekeeper hoac Mentor co the de `null`
- `language`: ngon ngu bo dataset
- `version`: version cua bo ground truth
- `institution_context`: boi canh truong / hoc phan / phong cach de bai
- `description`: ghi chu de mo ta bo test
- `evaluation_cases`: noi dung chinh cua dataset
- `is_active`: bo dataset dang hoat dong hay da ngung dung
- `created_at`, `updated_at`: thoi gian quan tri du lieu

## Cau truc `evaluation_cases`

### Doi voi Question Generation / Review

Moi case nen co toi thieu:

```js
{
  case_id: String,
  difficulty: String,              // Easy | Medium | Hard
  objective: String,               // muc tieu can test
  expected_topics: [String],       // tag mong doi
  expected_question_traits: [String],
  expected_review_outcome: String, // accepted | needs_revision
  notes: String
}
```

Neu muon chat hon cho production/test tu dong, co the them:

```js
{
  source_chunk_ids: [String],
  source_chunk_snapshot: [Object],
  expected_schema_valid: Boolean,
  expected_difficulty: String,
  forbidden_topics: [String],
  forbidden_errors: [String]
}
```

### Doi voi Code Mentor

Moi case nen co:

```js
{
  case_id: String,
  language: String,                // c | java
  problem_statement: String,
  student_code: String,
  expected_verdict: String,
  expected_issue_categories: [String],
  expected_feedback_direction: [String]
}
```

### Doi voi Gatekeeper

Moi case nen co:

```js
{
  case_id: String,
  file_reference: String,
  expected_supported: Boolean,
  expected_domain: String,
  scenario_group: String,          // supported | unsupported | ambiguous | noise
  notes: String
}
```

## Index de nghi

```js
{ dataset_id: 1, version: 1 }
{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }
{ dataset_name: 1 }
```

## Runtime mapping trong prototype

Trong `.NET prototype`, collection nay tuong ung voi:

- `AiGroundTruthSetRecord`

Runtime hien da co:

- load ground truth theo `subject + questionType`
- dung cho route regression cua `Question Generation + Review`

## Khuyen nghi khi import vao DB that

Nen giu nguyen 3 nguyen tac:

1. `dataset_id` la dinh danh on dinh, khong doi theo UI
2. `evaluation_cases` la source of truth, khong de rong
3. moi lan sua lon thi tao `version` moi, khong ghi de version cu
