# AI Generation Review Pipeline

## Muc tieu

Luong nay dung de tao cau hoi FE/PE tu `KnowledgeChunks` va kiem soat chat luong bang mot AI reviewer truoc khi tra ket qua cho he thong.

Day la luong quan trong nhat cua do an vi no quyet dinh:

- cau hoi co bam sat tri thuc da embedding hay khong
- chi phi AI cua chuc nang tao de
- kha nang tai su dung thang vao BE tong

## Schema output da duoc siet lai

Module hien tai da bat dau ep output generation bam sat collection that hon.

### FE

Moi cau hoi FE can co:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `options`
- `correct_answer`
- `explanation`

Va cac field sau phai la `null`:

- `skeleton_code`
- `solution_code`
- `test_cases`

### PE

Moi cau hoi PE can co:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `skeleton_code`
- `solution_code`
- `test_cases`

Va cac field sau phai la `null`:

- `options`
- `correct_answer`
- `explanation`

### Normalize sau generation

Sau khi AI1 tra ket qua:

1. module parse JSON array
2. co gang map ca `snake_case` va `camelCase`
3. loai cau hoi sai `question_type`
4. bo sung `source_chunk_ids` neu model khong tra ve
5. FE:
   - phai co `options`
   - phai co `correct_answer`
   - neu `correct_answer` tra ve full text thi module quy doi ve label nhu `A`, `B`
6. PE:
   - phai co `skeleton_code`
   - phai co `solution_code`
   - phai co `test_cases`
7. neu thieu field bat buoc thi cau hoi bi loai ngay

## Hai agent

### AI1 - Generator

Vai tro:

- nhan `KnowledgeChunk` da duoc trich, chunk, tag
- tao ra cau hoi theo dung schema JSON cua:
  - `FE_Questions`
  - `PE_Questions`

Input chinh:

- `subject`
- `difficulty`
- `question_type`
- `count`
- `chunks_json`
- `revision_feedback` neu day la lan tao lai
- `previous_questions_json` neu day la lan tao lai

Output:

- mang JSON cua cac cau hoi

### AI2 - Reviewer

Vai tro:

- doc ket qua cua AI1
- so sanh voi chunk context
- so sanh voi rubric tung loai cau hoi
- ket luan co chap nhan hay khong

Input chinh:

- `subject`
- `question_type`
- `chunks_json`
- `questions_json`
- `rubric_json`

Output:

```json
{
  "reviewStatus": "accepted|needs_revision",
  "issues": ["..."],
  "suggestions": ["..."],
  "score": 0.0,
  "schemaValid": true,
  "contentGrounded": true,
  "needsRevision": false
}
```

## Rubric danh gia

### FE

AI reviewer can check:

- output co dung schema `FE_Questions` khong
- cau hoi co bam sat chunk nguon khong
- `topic_tags` co hop ly khong
- `difficulty` co dung muc yeu cau khong
- `options` co day du va khong mo ho khong
- `correct_answer` co hop le khong
- `explanation` co giai thich hop ly khong

### PE

AI reviewer can check:

- output co dung schema `PE_Questions` khong
- de bai co bam sat chunk nguon khong
- `topic_tags` co hop ly khong
- `difficulty` co dung muc yeu cau khong
- `skeleton_code` co dung ngon ngu khong
- `solution_code` co giai duoc de khong
- `test_cases` co hop ly khong

### Rubric theo mon hoc

Reviewer khong con review generic theo FE/PE thuần.

Rubric hien tai da chia theo:

- `C + FE`
- `C + PE`
- `Java OOP + FE`
- `Java OOP + PE`
- `DSA Java + FE`
- `DSA Java + PE`

Y nghia:

- C: tap trung bien, vong lap, mang, con tro, ham
- Java OOP: tap trung class, object, constructor, encapsulation, inheritance, polymorphism
- DSA Java: tap trung array, list, stack, queue, tree, graph, search, sort

## Vong lap

De tranh ton token, vong lap chi chay toi da `2` lan.

## Lop validation va mapping noi bo

Ngoai AI reviewer, module hien tai da co them 1 lop `rule-based validation` trong application layer:

- `QuestionSchemaService`

Vai tro:

1. validate output cua AI1 theo schema FE/PE
2. map sang object gan voi collection that:
   - `FeQuestionDocument`
   - `PeQuestionDocument`
3. tra ve `QuestionSchemaMappingResult`

No giup:

- khong phu thuoc hoan toan vao reviewer AI
- reject som cac output sai schema
- chuan bi duong dan `validate -> map -> save`

### Tieu chi validate FE

- dung `type = FE`
- co `topic_tags`
- co `title`
- co `description`
- co `source_chunk_ids`
- co `options`
- co `correct_answer`
- `correct_answer` phai khop label cua option
- khong duoc co field PE

### Tieu chi validate PE

- dung `type = PE`
- co `topic_tags`
- co `title`
- co `description`
- co `source_chunk_ids`
- co `skeleton_code`
- co `solution_code`
- co `test_cases`
- khong duoc co field FE

### Tac dong len review

Neu `QuestionSchemaService` ket luan output sai schema:

- reviewer result se bi ep ve `needs_revision`
- `schemaValid = false`
- score bi ha xuong
- issue schema duoc gop vao issue cua AI reviewer

### Attempt 1

1. AI1 tao cau hoi
2. AI2 review
3. Neu `accepted` -> dung
4. Neu `needs_revision` -> chuyen sang Attempt 2

### Attempt 2

1. AI1 nhan them:
   - `revision_feedback`
   - `previous_questions_json`
2. AI1 tao lai cau hoi
3. AI2 review lai
4. Du accepted hay fail thi deu dung tai day

## Ly do gioi han 2 lan

- tranh vong lap vo han
- kiem soat chi phi benchmark
- van du de do kha nang self-correction cua he thong

## API hien tai

- `POST /api/ai-module/question-generation`
- `POST /api/ai-module/question-review`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/generation-review/export`

## Export package JSON chuan hoa

Route:

- `POST /api/ai-module/generation-review/export`

Muc dich:

- tra ve mot goi JSON hoan chinh de debug, benchmark, luu file, hoac dua sang BE tong
- giu cung mot payload chuan cho ca:
  - test tay
  - regression
  - so sanh model
  - luu tam lam bang chung bao cao

Cau truc goi export gom:

- `metadata`
- `request`
- `rubric`
- `questions`
- `schemaMapping`
- `difficultyAlignment`
- `mappedFeQuestions`
- `mappedPeQuestions`
- `review`
- `attemptsUsed`
- `stageLogs`
- `usageLogs`
- `totals`

Y nghia:

- `metadata`: thong tin package, model, rubric, prompt key, dataset id neu co
- `request`: snapshot request goc
- `rubric`: rubric runtime dang dung cho review
- `questions`: output raw sau generation
- `schemaMapping`: ket qua validate/map noi bo
- `mappedFeQuestions` / `mappedPeQuestions`: ban ghi save-ready gan voi collection
- `review`: ket qua AI reviewer sau cung
- `stageLogs`: log tung stage/tung attempt
- `usageLogs`: log usage/cost phuc vu benchmark va audit
- `totals`: tong token/cost/latency cua ca flow

## Prompt hien tai

Trong prompt registry tam thoi dang co:

- `question_generation`
- `question_review`

Prompt khong bi hard-code co dinh trong flow nghiep vu ma duoc lay tu prompt store file-backed.

## Prompt storage tam thoi

Prompt hien tai duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

Tam thoi day la bo nho dem/prompt registry de BE va UI test co the:

- xem prompt
- sua prompt
- reload prompt

Sau nay khi merge vao BE tong, nguon nay se duoc thay bang prompt version trong DB.

## Trang thai hien tai

Luong da duoc trien khai o muc:

- co 2 agent
- co rubric review theo FE/PE
- co loop toi da 2 lan
- co ghi log tung attempt
- co su dung provider that neu config phu hop

Nhung van can siet them:

- them mapper save thang vao `FE_Questions` va `PE_Questions`
- them usage log that neu provider tra usage
