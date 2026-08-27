# AI_GroundTruth_Sets

## Mục tiêu

Collection `AI_GroundTruth_Sets` lưu bộ dữ liệu chuẩn dùng để:

- regression test
- benchmark có đối chiếu
- accuracy evaluation
- so sánh model theo cùng một bộ case

Trong prototype hiện tại, nguồn tạm thời đang nằm ở:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/`

Khi đưa vào BE thật, các file JSON này có thể import vào collection này.

## Schema đề nghị

```js
{
  _id: ObjectId,

  dataset_id: String,              // id logic của dataset, vd: QGEN_C_FE
  dataset_name: String,            // tên để đọc trong UI / báo cáo
  function_name: String,           // QuestionGenerationReview | CodeMentor | Gatekeeper

  subject_code: String,            // C | JAVA_OOP | DSA_JAVA | MIXED
  question_type: String,           // FE | PE | null nếu không áp dụng
  language: String,                // en | vi | mixed

  version: String,                 // vd: v1
  institution_context: String,     // FPT University, PRF / PRO / CSD style...
  description: String,             // mô tả ngắn mục đích dataset

  evaluation_cases: [Object],      // danh sách test case chi tiết

  is_active: Boolean,
  created_at: Date,
  updated_at: Date
}
```

## Giải thích field

- `dataset_id`: mã dataset ổn định để runtime gọi ra đúng bộ test
- `dataset_name`: tên hiển thị cho người dùng hoặc báo cáo
- `function_name`: dataset thuộc chức năng AI nào
- `subject_code`: môn học hoặc nhóm môn học
- `question_type`: dùng cho FE/PE; nếu Gatekeeper hoặc Mentor có thể để `null`
- `language`: ngôn ngữ bộ dataset
- `version`: version của bộ ground truth
- `institution_context`: bối cảnh trường, học phần hoặc phong cách đề bài
- `description`: ghi chú để mô tả bộ test
- `evaluation_cases`: nội dung chính của dataset
- `is_active`: bộ dataset đang hoạt động hay đã ngừng dùng
- `created_at`, `updated_at`: thời gian quản trị dữ liệu

## Cấu trúc `evaluation_cases`

### Đối với Question Generation / Review

Mỗi case nên có tối thiểu:

```js
{
  case_id: String,
  difficulty: String,              // Easy | Medium | Hard
  objective: String,               // mục tiêu cần test
  expected_topics: [String],       // tag mong đợi
  expected_question_traits: [String],
  expected_review_outcome: String, // accepted | needs_revision
  notes: String
}
```

Nếu muốn chặt hơn cho production hoặc test tự động, có thể thêm:

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

### Đối với Code Mentor

Mỗi case nên có:

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

### Đối với Gatekeeper

Mỗi case nên có:

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

## Index đề nghị

```js
{ dataset_id: 1, version: 1 }
{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }
{ dataset_name: 1 }
```

## Runtime mapping trong prototype

Trong `.NET prototype`, collection này tương ứng với:

- `AiGroundTruthSetRecord`

Runtime hiện đã có:

- load ground truth theo `subject + questionType`
- dùng cho route regression của `Question Generation + Review`

## Khuyến nghị khi import vào DB thật

Nên giữ nguyên 3 nguyên tắc:

1. `dataset_id` là định danh ổn định, không đổi theo UI
2. `evaluation_cases` là source of truth, không để rỗng
3. mỗi lần sửa lớn thì tạo `version` mới, không ghi đè version cũ
