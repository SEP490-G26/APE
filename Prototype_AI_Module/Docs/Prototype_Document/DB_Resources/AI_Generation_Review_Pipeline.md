# AI Generation Review Pipeline

## Mục tiêu

Luồng này dùng để tạo câu hỏi FE/PE từ tri thức đã ingest và kiểm soát chất lượng bằng một AI reviewer trước khi trả kết quả cho hệ thống.

Đây là luồng quan trọng nhất của đồ án vì nó quyết định:

- câu hỏi có bám sát tri thức đã embedding hay không
- chi phí AI của chức năng tạo đề
- khả năng tái sử dụng thẳng vào BE tổng

## Điều chỉnh tư duy pipeline

Ở mức benchmark nhỏ, có thể vẫn truyền thẳng `KnowledgeChunks` vào generation.

Ở mức production-test-harness hiện tại, luồng khuyến nghị là:

`KnowledgeChunks -> Retrieval Planner -> AI_Context_Packs -> Generator -> Reviewer`

Nghĩa là `KnowledgeChunks` là nơi lưu tri thức gốc, còn `AI_Context_Packs` là context đã được chọn lọc và nén để đưa vào AI downstream.

## Schema output đã được siết lại

Module hiện tại đã bắt đầu ép output generation bám sát collection thật hơn.

### FE

Mỗi câu hỏi FE cần có:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `options`
- `correct_answer`
- `explanation`

Và các field sau phải là `null`:

- `skeleton_code`
- `solution_code`
- `test_cases`

### PE

Mỗi câu hỏi PE cần có:

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `skeleton_code`
- `solution_code`
- `test_cases`

Và các field sau phải là `null`:

- `options`
- `correct_answer`
- `explanation`

## Normalize sau generation

Sau khi AI1 trả kết quả:

1. module parse JSON array
2. cố gắng map cả `snake_case` và `camelCase`
3. loại câu hỏi sai `question_type`
4. bổ sung `source_chunk_ids` nếu model không trả về
5. FE:
   - phải có `options`
   - phải có `correct_answer`
   - nếu `correct_answer` trả về full text thì module quy đổi về label như `A`, `B`
6. PE:
   - phải có `skeleton_code`
   - phải có `solution_code`
   - phải có `test_cases`
7. nếu thiếu field bắt buộc thì câu hỏi bị loại ngay

## Hai agent

### AI1 - Generator

Vai trò:

- nhận context pack hoặc tập chunk đã được pack gọn
- tạo ra câu hỏi theo đúng schema JSON của:
  - `FE_Questions`
  - `PE_Questions`

Input chính:

- `subject`
- `difficulty`
- `question_type`
- `count`
- `chunks_json` hoặc `context_pack`
- `revision_feedback` nếu đây là lần tạo lại
- `previous_questions_json` nếu đây là lần tạo lại

Output:

- mảng JSON của các câu hỏi

### AI2 - Reviewer

Vai trò:

- đọc kết quả của AI1
- so sánh với chunk/context nguồn
- so sánh với rubric từng loại câu hỏi
- kết luận có chấp nhận hay không

Input chính:

- `subject`
- `question_type`
- `chunks_json` hoặc `context_pack`
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

## Retrieval planner nên làm gì trước AI1

Trước khi AI1 chạy, planner nên:

1. filter theo `course_id`, `subject_code`, `retrieval_enabled`, `status`
2. lấy candidate theo `topic_tags`, lexical search, vector search
3. re-rank theo:
   - `topic_primary`
   - `assessment_value_score`
   - `content_quality_score`
   - `estimated_difficulty`
   - duplicate penalty
4. build `AI_Context_Packs` theo token budget

## Runtime hiện tại của prototype

Prototype hiện tại đã nối runtime theo 3 kiểu input cho `generation-review`:

1. `chunks`
2. `contextPackId`
3. `retrieval`

Nếu dùng `retrieval`:

- orchestrator sẽ gọi retrieval planner
- retrieval planner chọn candidate, re-rank và build/load `AI_Context_Packs`
- `generator` và `reviewer` sẽ đọc `pack.Chunks`

Nếu dùng `contextPackId`:

- orchestrator sẽ load pack đã lưu
- phù hợp để chạy nhiều model trên cùng một ngữ cảnh

Nếu dùng `chunks`:

- hệ thống sẽ chạy trực tiếp theo kiểu benchmark nhanh
- phù hợp để test prompt hoặc test quality ở quy mô nhỏ

## Token budget gợi ý

### FE single

- khoảng 2k đến 5k input tokens

### PE single

- khoảng 4k đến 8k input tokens

### Small batch

- khoảng 8k đến 15k input tokens tuỳ loại

Mục tiêu là giảm cost cho từng câu hỏi, nhất là khi tài liệu nguồn rất lớn.

## Rubric đánh giá

### FE

Reviewer cần kiểm tra:

- có grounded vào context pack và source chunks không
- có đúng difficulty không
- có đúng schema không
- có self-contained cho sinh viên không
- có tránh meta-source wording không

### PE

Reviewer cần kiểm tra thêm:

- task có meaningful hay không
- skeleton/solution/tests có align không
- có rơi vào syntax drill quá hời hợt không
- test coverage có đủ để xem là bài PE hợp lệ không

## Quan hệ collection

- `KnowledgeChunks` là kho tri thức gốc
- `AI_Context_Packs` là ngữ cảnh đã tối ưu cho generation/review
- `FE_Questions` / `PE_Questions` là output cuối cùng của pipeline

## Kết luận

Luồng generation-review hiện đã có tầng retrieval + context packing ở mức prototype test harness.

Điểm cần phát triển tiếp không còn là thiếu flow này, mà là:

- tối ưu retrieval quality
- tối ưu token packing ở case trung bình/dài
- nối DB/vector search thật
- siết reuse policy cho `AI_Context_Packs`
