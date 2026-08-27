# Tong Ket Question Generation + Review FE/PE

## Muc tieu

Tai lieu nay tong hop toan bo nhung gi da lam cho chuc nang:

- tao cau hoi `FE`
- tao cau hoi `PE`
- review ket qua tao cau hoi
- chuan hoa output de dua vao BE tong sau nay

Day la phan AI quan trong nhat cua he thong vi no dung de bien `KnowledgeChunks` thanh cau hoi hoc tap co the dua vao question bank.

## Pham vi da lam

Da hoan thanh o muc prototype DB-ready:

1. generation flow
2. review flow
3. generation + review pipeline 1-2 vong
4. schema validation
5. persistence mapping gan voi collection FE/PE
6. duplicate detection muc co ban
7. difficulty alignment service doc lap
8. rubric runtime theo mon + loai cau hoi
9. ground truth dataset runtime cho regression
10. export package JSON chuan hoa
11. log stage + usage + token/cost tong

## Cac thanh phan chinh

### AI1 - Generator

Vai tro:

- nhan `KnowledgeChunks`
- tao `FE` hoac `PE`
- output JSON theo schema da chot

Input chinh:

- `subject`
- `difficulty`
- `questionType`
- `count`
- `chunks`
- `revisionFeedback` neu regenerate
- `previousQuestions` neu regenerate

### AI2 - Reviewer

Vai tro:

- doc output cua AI1
- doi chieu voi chunk nguon
- doi chieu voi rubric
- ket luan `accepted` hoac `needs_revision`

### Rule-based layer

Ngoai reviewer AI, module da co them lop rule-based:

- `QuestionSchemaService`
- `DifficultyAlignmentService`

Muc dich:

- khong phu thuoc 100% vao AI reviewer
- bat som schema sai
- bat som sai do kho
- tra ve output map-ready cho collection that

## Schema output da chot

### FE

Field bat buoc:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `options`
- `correct_answer`
- `explanation`

Field phai la `null`:

- `skeleton_code`
- `solution_code`
- `test_cases`

### PE

Field bat buoc:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `skeleton_code`
- `solution_code`
- `test_cases`

Field phai la `null`:

- `options`
- `correct_answer`
- `explanation`

## Pipeline runtime

### `question-generation`

Flow:

1. AI generator tao danh sach cau hoi
2. module normalize output
3. module validate schema
4. module map sang FE/PE save-ready record
5. module tinh token/cost

Luu y runtime:

- route `POST /api/ai-module/question-generation/debug` hien ton tai
- nhung hien dang tra cung payload runtime nhu route thuong
- chua co debug envelope rieng cho prompt/rubric/ground-truth

### `generation-review`

Flow:

1. chay `question-generation`
2. chay AI reviewer
3. neu schema sai -> ep `needs_revision`
4. neu difficulty lech -> ep `needs_revision`
5. neu reviewer reject -> tao lai toi da 1 lan nua
6. sau vong cuoi -> map va save tam

Luu y runtime:

- route `POST /api/ai-module/generation-review/debug` hien ton tai
- nhung hien dang tra cung payload runtime nhu route thuong
- route `POST /api/ai-module/generation-review/export` moi la route day du nhat de audit package

Mode da ho tro:

- `SingleAgent`
- `SameModelDualRole`
- `DualAgent`

## Duplicate detection da co

Da co 2 lop:

1. duplicate trong cung 1 batch generation
2. duplicate heuristic voi repository tam

Tac dung:

- neu trung cau hoi da ton tai hoac trung noi bo
- schema mapping se bi danh dau issue
- review flow co the bi ep ve `needs_revision`

## Difficulty alignment da co

Da them service doc lap:

- `DifficultyAlignmentService`

Vai tro:

- tu danh gia `Easy/Medium/Hard`
- doi chieu voi difficulty duoc yeu cau
- neu lech muc -> them issue va co the reject output

## Rubric da co

Da tao 6 rubric JSON cho generation/review:

- `C_FE`
- `C_PE`
- `JAVA_OOP_FE`
- `JAVA_OOP_PE`
- `DSA_JAVA_FE`
- `DSA_JAVA_PE`

Rubric da noi runtime vao review flow.

## Ground truth da co

Da tao bo starter ground truth cho:

- `C_FE`
- `C_PE`
- `JAVA_OOP_FE`
- `JAVA_OOP_PE`
- `DSA_JAVA_FE`
- `DSA_JAVA_PE`

Ground truth da noi vao:

- route regression

## API da co

- `POST /api/ai-module/question-generation`
- `POST /api/ai-module/question-generation/debug`
- `GET /api/ai-module/question-generation/rubric`
- `GET /api/ai-module/question-generation/ground-truth`
- `POST /api/ai-module/question-generation/regression`
- `POST /api/ai-module/question-review`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/generation-review/debug`
- `POST /api/ai-module/generation-review/export`

## Export package da co

Route:

- `POST /api/ai-module/generation-review/export`

Payload export gom:

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

Muc dich:

- debug
- benchmark
- so sanh model
- dua cho FE render
- chuyen tiep vao BE tong sau nay

## Collection / config lien quan da duoc dinh hinh

Da chot huong cho 3 collection cau hinh:

- `AI_Prompt_Versions`
- `AI_Rubrics`
- `AI_GroundTruth_Sets`

## Nhung gi chua lam

Nhung phan con lai hop ly de lam khi merge vao BE that:

1. Mongo repository that
2. vector-based duplicate detection
3. compile/run verification cho `PE` bang Judge0
4. publish workflow cho question bank
5. prompt/rubric/ground truth doc that tu DB
6. luu export package vao DB neu can audit lau dai

## Ket luan

Phan `Question Generation + Review FE/PE` hien da du muc de:

- test model
- benchmark cost
- regression co doi chieu
- ep JSON gan voi schema DB
- lam prototype cho core module AI cua do an

Nghia la khi chuyen sang AI function tiep theo, phan nay da co the xem la khoi core on dinh nhat cua module.
