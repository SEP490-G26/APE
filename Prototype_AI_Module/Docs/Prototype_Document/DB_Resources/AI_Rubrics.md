# AI_Rubrics

## Mục tiêu

Collection `AI_Rubrics` lưu bộ tiêu chí đánh giá có cấu trúc cho các AI function cần review theo quy chuẩn rõ ràng.

Trong giai đoạn hiện tại, rubric đang được lưu tạm ở:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/`

Khi đưa vào BE thật, các file này có thể import vào collection `AI_Rubrics`.

## Schema đề nghị

```js
{
  _id: ObjectId,

  rubric_id: String,               // vd: C_FE, JAVA_OOP_PE, DSA_JAVA_FE
  rubric_name: String,             // tên để đọc trên UI / admin
  function_name: String,           // QuestionGenerationReview | CodeMentor

  subject_code: String,            // C | JAVA_OOP | DSA_JAVA
  question_type: String,           // FE | PE | null nếu rubric không theo FE/PE
  language: String,                // en

  version: String,                 // vd: v1
  institution_context: String,     // FPT University, PRF / PRO / CSD style
  description: String,             // mô tả ngắn mục đích rubric

  content_json: Object,            // nội dung rubric chi tiết

  is_active: Boolean,
  created_at: Date,
  updated_at: Date
}
```

## Giải thích field

- `rubric_id`: mã rubric ổn định cho runtime
- `rubric_name`: tên hiển thị
- `function_name`: rubric thuộc AI function nào
- `subject_code`: rubric gắn với môn học nào
- `question_type`: FE hay PE; nếu rubric cho mentor có thể để `null`
- `language`: ngôn ngữ viết rubric, hiện tại nên để `en`
- `version`: version của rubric
- `institution_context`: bối cảnh học thuật của FPT University
- `description`: mô tả mục đích rubric
- `content_json`: nội dung chi tiết dùng cho runtime
- `is_active`: rubric đang được sử dụng hay đã ngừng
- `created_at`, `updated_at`: thời gian quản trị dữ liệu

## Cấu trúc `content_json`

### Đối với Question Generation / Review

`content_json` nên có tối thiểu:

```js
{
  difficulty_definitions: Object,
  review_checks: [Object],
  acceptance_rules: [String],
  rejection_rules: [String],
  difficulty_mismatch_signals: [String],
  regeneration_hints: [String]
}
```

Ý nghĩa:

- `difficulty_definitions`: mô tả `Easy`, `Medium`, `Hard` theo từng môn
- `review_checks`: danh sách tiêu chí cần reviewer check
- `acceptance_rules`: điều kiện để accept
- `rejection_rules`: điều kiện bắt buộc reject / needs_revision
- `difficulty_mismatch_signals`: dấu hiệu sai độ khó
- `regeneration_hints`: hướng dẫn để AI generator sửa lại

### Đối với Code Mentor

`content_json` nên có:

```js
{
  verdict_levels: [String],
  issue_categories: [String],
  feedback_style_rules: [String],
  suggestion_quality_rules: [String],
  severity_guidelines: [Object]
}
```

## Rubric đã có trong prototype hiện tại

Phần `Question Generation + Review` đã có 6 rubric:

- `C_FE`
- `C_PE`
- `JAVA_OOP_FE`
- `JAVA_OOP_PE`
- `DSA_JAVA_FE`
- `DSA_JAVA_PE`

Ý nghĩa:

- `C`: môn nhập môn C
- `JAVA_OOP`: Java OOP
- `DSA_JAVA`: cấu trúc dữ liệu và giải thuật trên Java

## Index đề nghị

```js
{ rubric_id: 1, version: 1 }
{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }
{ rubric_name: 1 }
```

## Runtime mapping trong prototype

Trong `.NET prototype`, collection này tương ứng với:

- `AiRubricRecord`

Runtime hiện đã có:

- load rubric theo `subject + questionType`
- nối rubric vào `Question Review` flow
- export rubric trong route `generation-review/export`

## Khuyến nghị khi import vào DB thật

1. mỗi rubric nên có `rubric_id` cố định
2. không sửa trực tiếp version cũ nếu đã dùng benchmark
3. nếu prompt đổi theo rubric, cần lưu liên kết giữa `AI_Prompt_Versions` và `AI_Rubrics`
