# Tổng Kết Question Generation + Review FE/PE

## Mục tiêu

Tài liệu này tổng hợp toàn bộ những gì đã làm cho chức năng:

- tạo câu hỏi `FE`
- tạo câu hỏi `PE`
- review kết quả tạo câu hỏi
- chuẩn hoá output để đưa vào BE tổng sau này

Đây là phần AI quan trọng nhất của hệ thống vì nó dùng để biến tri thức đã ingest thành câu hỏi học tập có thể đưa vào question bank.

## Phạm vi đã làm

Đã hoàn thành ở mức prototype DB-ready:

1. generation flow
2. review flow
3. generation + review pipeline 1-2 vòng
4. schema validation
5. persistence mapping gần với collection FE/PE
6. duplicate detection mức cơ bản
7. difficulty alignment service độc lập
8. rubric runtime theo môn + loại câu hỏi
9. ground truth dataset runtime cho regression
10. export package JSON chuẩn hoá
11. log stage + usage + token/cost tổng

## Phạm vi còn thiếu cho production

Production không thể chỉ nối thẳng `KnowledgeChunks` vào AI1 như prototype nhỏ hiện nay.

Cần thêm 1 tầng bắt buộc ở giữa:

`KnowledgeChunks -> Retrieval Planner -> Context Packing -> Question Generation -> Question Review`

Mục tiêu của tầng này là:

- giảm token input
- chọn đúng chunk theo topic/difficulty
- tránh đẩy quá nhiều raw chunks vào model
- tối ưu cost khi tạo 1 câu đơn hoặc một batch câu hỏi

## Các thành phần chính

### Retrieval Planner

Vai trò:

- nhận yêu cầu tạo đề theo môn, topic, difficulty, question type
- lọc candidate chunk từ `KnowledgeChunks`
- re-rank theo độ phù hợp
- build hoặc load `AI_Context_Packs`

Đây là lớp sẽ quyết định phần lớn chi phí production của flow tạo đề.

### AI1 - Generator

Vai trò:

- nhận `AI_Context_Packs` hoặc tập chunk đã được pack gọn
- tạo `FE` hoặc `PE`
- output JSON theo schema đã chốt

Input chính:

- `subject`
- `difficulty`
- `questionType`
- `count`
- `contextPack` hoặc `chunks`
- `revisionFeedback` nếu regenerate
- `previousQuestions` nếu regenerate

### AI2 - Reviewer

Vai trò:

- đọc output của AI1
- đối chiếu với context pack và chunk nguồn
- đối chiếu với rubric
- kết luận `accepted` hoặc `needs_revision`

### Rule-based layer

Ngoài reviewer AI, module đã có thêm lớp rule-based:

- `QuestionSchemaService`
- `DifficultyAlignmentService`

Mục đích:

- không phụ thuộc 100% vào AI reviewer
- bắt sớm schema sai
- bắt sớm sai độ khó
- trả về output map-ready cho collection thật

## Schema output đã chốt

### FE

Field bắt buộc:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `options`
- `correct_answer`
- `explanation`

Field phải là `null`:

- `skeleton_code`
- `solution_code`
- `test_cases`

### PE

Field bắt buộc:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `skeleton_code`
- `solution_code`
- `test_cases`

Field phải là `null`:

- `options`
- `correct_answer`
- `explanation`

## Pipeline runtime hiện tại

### `question-generation`

Flow hiện tại trong prototype:

1. nhận `chunks`
2. AI generator tạo danh sách câu hỏi
3. module normalize output
4. module validate schema
5. module map sang FE/PE save-ready record
6. module tính token/cost

Điểm còn thiếu:

- chưa có DB/vector retrieval thật
- chưa có reuse policy / cache invalidation đủ chặt cho context pack

### `generation-review`

Flow hiện tại:

1. chạy `question-generation`
2. chạy AI reviewer
3. nếu schema sai -> ép `needs_revision`
4. nếu difficulty lệch -> ép `needs_revision`
5. nếu reviewer reject -> tạo lại tối đa 1 lần nữa
6. sau vòng cuối -> map và save tạm

Mode đã hỗ trợ:

- `SingleAgent`
- `SameModelDualRole`
- `DualAgent`

## Pipeline production-test-harness hiện tại

### `generation-request`

Flow đang hỗ trợ trong prototype:

1. nhận request tạo đề
2. retrieval planner lọc candidate từ `KnowledgeChunks`
3. re-rank candidate
4. build/load `AI_Context_Packs`
5. generator tạo câu hỏi từ context pack
6. reviewer đối chiếu lại với context pack + source chunks
7. save logs + config refs + context pack refs

## Tại sao cần context packing

Nếu đưa quá nhiều chunk raw vào AI1, hệ thống sẽ gặp:

- token input cao
- latency cao
- chi phí cao cho một câu hỏi đơn
- ngữ cảnh loãng, dễ sinh câu hỏi generic

Do đó, trước generation phải có tầng:

- lọc đúng chunk
- bỏ chunk trùng
- ưu tiên chunk có giá trị assessment cao
- pack theo token budget

## Mode downstream nên có

### `single_question_precise`

Dùng khi:

- chỉ tạo 1 câu
- cần tối ưu chi phí

Chiến lược:

- 1 primary chunk
- 0-2 supporting chunks
- token budget chặt

### `small_batch`

Dùng khi:

- tạo 3-5 câu cùng topic/chương

Chiến lược:

- 1 pack dùng chung
- amortize chi phí tốt hơn

### `coverage_exam`

Dùng khi:

- cần phủ nhiều topic
- tạo đề nhiều phần

Chiến lược:

- nhiều context pack nhỏ
- mỗi pack tạo 1-2 câu

## Duplicate detection đã có

Đã có 2 lớp:

1. duplicate trong cùng 1 batch generation
2. duplicate heuristic với repository tạm

Tác dụng:

- nếu trùng câu hỏi đã tồn tại hoặc trùng nội bộ
- schema mapping sẽ bị đánh dấu issue
- review flow có thể bị ép về `needs_revision`

Về sau nên kết hợp thêm duplicate theo retrieval/context pack:

- cùng `topic`
- cùng `source_chunk_ids`
- cùng `task pattern`

để tránh sinh lặp theo pack đã dùng nhiều lần.

## Difficulty alignment đã có

Đã thêm service độc lập:

- `DifficultyAlignmentService`

Vai trò:

- tự đánh giá `Easy/Medium/Hard`
- đối chiếu với difficulty được yêu cầu
- nếu lệch mức -> thêm issue và có thể reject output

Về sau retrieval planner cũng nên tận dụng field:

- `estimated_difficulty`
- `assessment_value_score`

để chọn chunk phù hợp difficulty trước khi gửi vào AI.

## Rubric đã có

Đã tạo 6 rubric JSON cho generation/review:

- `C_FE`
- `C_PE`
- `JAVA_OOP_FE`
- `JAVA_OOP_PE`
- `DSA_JAVA_FE`
- `DSA_JAVA_PE`

Rubric đã nối runtime vào review flow.

## Ground truth đã có

Đã tạo bộ starter ground truth cho:

- `C_FE`
- `C_PE`
- `JAVA_OOP_FE`
- `JAVA_OOP_PE`
- `DSA_JAVA_FE`
- `DSA_JAVA_PE`

Ground truth đã nối vào:

- route regression

## API đã có

- `POST /api/ai-module/question-generation`
- `POST /api/ai-module/question-generation/debug`
- `GET /api/ai-module/question-generation/rubric`
- `GET /api/ai-module/question-generation/ground-truth`
- `POST /api/ai-module/question-generation/regression`
- `POST /api/ai-module/question-review`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/generation-review/debug`
- `POST /api/ai-module/generation-review/export`
- `POST /api/ai-module/retrieval/plan`
- `POST /api/ai-module/retrieval/plan/debug`
- `POST /api/ai-module/context-packs/build`
- `GET /api/ai-module/context-packs/{packId}`
- `GET /api/ai-module/context-packs`
- `POST /api/ai-module/context-packs/{packId}/mark-stale`

## Export package đã có

Route:

- `POST /api/ai-module/generation-review/export`

Payload export gồm:

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

Về sau nên thêm:

- `contextPackSummary`
- `contextPackId`
- `contextSourceChunkIds`
- `retrievalScores`

## Tài liệu liên quan nên đọc cùng

- `Docs/Prototype_Document/DB_Resources/KnowledgeChunks.md`
- `Docs/Prototype_Document/DB_Resources/AI_Context_Packs.md`
- `Docs/Prototype_Document/Specs/Spec_AI_Retrieval_Context_Packing_Architecture.md`\n- `Docs/Prototype_Document/Specs/Spec_AI_Retrieval_Planner_Contracts.md`

## Kết luận

Generation + Review hiện đã có retrieval + context packing ở mức prototype test harness.

Điểm cần tối ưu tiếp không còn là “có hay không có retrieval planner”, mà là:

- retrieval quality
- packed token ratio
- cache/reuse strategy
- khả năng nối DB/vector search thật để phản ánh production đầy đủ hơn

