# AI_Rubrics

## Muc tieu

Collection `AI_Rubrics` luu bo tieu chi danh gia co cau truc cho cac AI function can review theo quy chuan ro rang.

Trong giai doan hien tai, rubric dang duoc luu tam o:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/`

Khi dua vao BE that, cac file nay co the import vao collection `AI_Rubrics`.

## Schema de nghi

```js
{
  _id: ObjectId,

  rubric_id: String,               // vd: C_FE, JAVA_OOP_PE, DSA_JAVA_FE
  rubric_name: String,             // ten de doc tren UI / admin
  function_name: String,           // QuestionGenerationReview | CodeMentor

  subject_code: String,            // C | JAVA_OOP | DSA_JAVA
  question_type: String,           // FE | PE | null neu rubric khong theo FE/PE
  language: String,                // en

  version: String,                 // vd: v1
  institution_context: String,     // FPT University, PRF / PRO / CSD style
  description: String,             // mo ta ngan muc dich rubric

  content_json: Object,            // noi dung rubric chi tiet

  is_active: Boolean,
  created_at: Date,
  updated_at: Date
}
```

## Giai thich field

- `rubric_id`: ma rubric on dinh cho runtime
- `rubric_name`: ten hien thi
- `function_name`: rubric thuoc AI function nao
- `subject_code`: rubric gan voi mon hoc nao
- `question_type`: FE hay PE; neu rubric cho mentor co the de `null`
- `language`: ngon ngu viet rubric, hien tai nen de `en`
- `version`: version cua rubric
- `institution_context`: boi canh hoc thuat cua FPT University
- `description`: mo ta muc dich rubric
- `content_json`: noi dung chi tiet dung cho runtime
- `is_active`: rubric dang duoc su dung hay da ngung
- `created_at`, `updated_at`: thoi gian quan tri du lieu

## Cau truc `content_json`

### Doi voi Question Generation / Review

`content_json` nen co toi thieu:

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

Y nghia:

- `difficulty_definitions`: mo ta `Easy`, `Medium`, `Hard` theo tung mon
- `review_checks`: danh sach tieu chi can reviewer check
- `acceptance_rules`: dieu kien de accept
- `rejection_rules`: dieu kien bat buoc reject / needs_revision
- `difficulty_mismatch_signals`: dau hieu sai do kho
- `regeneration_hints`: huong dan de AI generator sua lai

### Doi voi Code Mentor

`content_json` nen co:

```js
{
  verdict_levels: [String],
  issue_categories: [String],
  feedback_style_rules: [String],
  suggestion_quality_rules: [String],
  severity_guidelines: [Object]
}
```

## Rubric da co trong prototype hien tai

Phan `Question Generation + Review` da co 6 rubric:

- `C_FE`
- `C_PE`
- `JAVA_OOP_FE`
- `JAVA_OOP_PE`
- `DSA_JAVA_FE`
- `DSA_JAVA_PE`

Y nghia:

- `C`: mon nhap mon C
- `JAVA_OOP`: Java OOP
- `DSA_JAVA`: cau truc du lieu va giai thuat tren Java

## Index de nghi

```js
{ rubric_id: 1, version: 1 }
{ function_name: 1, subject_code: 1, question_type: 1, is_active: 1 }
{ rubric_name: 1 }
```

## Runtime mapping trong prototype

Trong `.NET prototype`, collection nay tuong ung voi:

- `AiRubricRecord`

Runtime hien da co:

- load rubric theo `subject + questionType`
- noi rubric vao `Question Review` flow
- export rubric trong route `generation-review/export`

## Khuyen nghi khi import vao DB that

1. moi rubric nen co `rubric_id` co dinh
2. khong sua truc tiep version cu neu da dung benchmark
3. neu prompt doi theo rubric, can luu lien ket giua `AI_Prompt_Versions` va `AI_Rubrics`
