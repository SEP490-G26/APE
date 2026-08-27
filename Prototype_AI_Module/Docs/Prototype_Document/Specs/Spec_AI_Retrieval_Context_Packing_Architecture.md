# Retrieval + Context Packing Architecture

## 1. Mục tiêu

Kiến trúc này giải quyết bài toán production khi tài liệu nguồn rất lớn nhưng request downstream chỉ cần một phần tri thức phù hợp.

Ví dụ:

- một giáo trình Java có thể hơn 1000 trang
- sau khi extraction + chunking có thể sinh ra hàng nghìn `KnowledgeChunks`
- nếu generation đọc quá nhiều chunk raw trong một request, input token có thể tăng lên hàng chục nghìn đến hơn 100 nghìn token

Vì vậy, hệ thống phải có một tầng trung gian:

`KnowledgeChunks -> Retrieval -> Re-rank -> Context Packing -> Question Generation / Review / Mentor`

## 2. Vấn đề cần giải quyết

### 2.1 Không thể đưa toàn bộ chunk vào LLM

Nếu request tạo đề nhận toàn bộ chunk liên quan đến một chương hoặc cả tài liệu, hệ thống sẽ gặp:

- token input rất cao
- latency cao
- cost tăng mạnh
- LLM bị loãng ngữ cảnh
- khả năng grounding kém đi vì có quá nhiều tín hiệu nhiễu

### 2.2 Tạo 1 câu đơn rất dễ lỗ

Nếu mỗi lần tạo 1 câu mà phải đọc lại một lượng chunk quá lớn, cost trên mỗi câu sẽ quá cao.

Do đó cần tối ưu theo 2 hướng:

- giảm token đầu vào cho từng request
- hoặc tăng số câu hỏi sinh trên cùng một context pack để amortize chi phí

## 3. Thành phần chính

### 3.1 Candidate Retrieval

Nhiệm vụ:

- lọc ra tập chunk ứng viên từ `KnowledgeChunks`
- giảm hàng nghìn chunk xuống còn vài chục ứng viên có ý nghĩa

Nguồn tín hiệu nên dùng đồng thời:

- metadata filter
- topic filter
- lexical overlap
- vector similarity
- chapter/section proximity

### 3.2 Retrieval Re-ranker

Nhiệm vụ:

- tính điểm cuối cùng cho mỗi chunk ứng viên
- loại chunk yếu, trùng, quá ngắn, quá nhiễu
- chọn một tập chunk nền phù hợp với mục tiêu downstream

### 3.3 Context Packer

Nhiệm vụ:

- gom các chunk tốt nhất thành một context pack cuối cùng
- đảm bảo đúng token budget
- ưu tiên tính đầy đủ về tri thức nhưng vẫn gọn

### 3.4 Context Pack Store

Nhiệm vụ:

- cache lại pack đã được build
- tái sử dụng cho các request tương tự
- phục vụ benchmark cost, audit và định giá credit

Collection phù hợp là `AI_Context_Packs`.

## 4. Input của retrieval planner

Retrieval planner nên nhận một request logic như sau:

```json
{
  "course_id": "...",
  "subject_code": "JAVA_OOP",
  "question_type": "PE",
  "difficulty": "Medium",
  "target_topics": ["class", "constructor"],
  "requested_question_count": 1,
  "generation_mode": "single_question_precise",
  "user_id": "...",
  "source_scope": "SYSTEM_CORE|BYOS|HYBRID"
}
```

## 5. Bước 1: Metadata pre-filter

Đây là bước rẻ nhất và phải làm trước vector search.

Filter cứng nên gồm:

- `subject_code`
- `course_id`
- `retrieval_enabled = true`
- `status = active`
- `syllabus_scope`
- `language` nếu cần

Filter mềm có thể gồm:

- `topic_primary`
- `topic_tags`
- `chapter_id`
- `estimated_difficulty`

Mục tiêu:

- không cho vector search chạy trên toàn bộ knowledge base khi không cần

## 6. Bước 2: Candidate search

Sau pre-filter, planner nên lấy candidate từ nhiều kênh:

### 6.1 Topic-first retrieval

Lấy các chunk có:

- `topic_primary` trùng
- `topic_tags` giao với `target_topics`
- `chapter_title` hoặc `section_title` liên quan

### 6.2 Vector retrieval

Dùng query embedding từ:

- topic phrase
- intent text của request
- hoặc một prompt rút gọn như `Create one Medium PE question about constructor behavior and object initialization`

### 6.3 Lexical retrieval

Dùng BM25/full-text/keyword overlap với:

- `section_title`
- `normalized_text`
- `concept_keywords`
- `chunk_summary`

## 7. Bước 3: Re-rank

Mỗi chunk ứng viên nên có một điểm cuối `final_score`.

Ví dụ các thành phần:

```text
final_score =
  0.35 * vector_score
+ 0.20 * topic_score
+ 0.15 * lexical_score
+ 0.10 * section_priority_score
+ 0.10 * assessment_value_score
+ 0.05 * content_quality_score
+ 0.05 * retrieval_score_boost
- duplicate_penalty
- noise_penalty
```

### 7.1 Ưu tiên chunk nào

Chunk tốt cho generation thường là chunk:

- có 1 khái niệm rõ ràng
- có ví dụ hoặc quy tắc rõ
- đủ ngắn để pack
- có `assessment_value_score` cao
- không phải slide mục lục, số trang, ngày tháng, rác parser

### 7.2 Loại chunk nào

Nên loại hoặc phạt điểm mạnh với chunk:

- quá ngắn
- chỉ là heading rỗng
- gần trùng với chunk khác
- thiên về metadata hơn là tri thức học tập
- chất lượng extraction kém

## 8. Bước 4: Context packing

Đây là bước quan trọng nhất để tối ưu cost.

### 8.1 Nguyên tắc pack

Không lấy đơn giản `top K chunks`.

Phải pack theo vai trò:

- `primary chunk`: chunk cốt lõi nhất
- `supporting chunk`: bổ trợ định nghĩa, ví dụ, edge case
- `prerequisite chunk`: chỉ thêm nếu thiếu ngữ cảnh nền
- `code/example chunk`: thêm nếu PE cần code behavior cụ thể

### 8.2 Token budget

Budget phải phụ thuộc vào mode downstream.

Gợi ý:

- `single_question_precise / FE`: 2000-5000 input tokens
- `single_question_precise / PE`: 4000-8000 input tokens
- `small_batch / FE`: 5000-10000 input tokens
- `small_batch / PE`: 8000-15000 input tokens
- `review_context`: nhỏ hơn generation context nếu có thể dùng summary + cited chunks

### 8.3 Summary-first packing

Thay vì luôn đưa full raw chunk, nên ưu tiên:

- `chunk_summary`
- rồi mới thêm `normalized_text` cho một số chunk lõi

Chiến lược này giúp:

- giảm token mạnh
- vẫn giữ đủ tín hiệu cho LLM

### 8.4 De-dup packing

Nếu nhiều chunk gần như nói cùng một ý:

- chỉ giữ 1 chunk đại diện
- hoặc giữ 1 chunk raw + 1 summary ngắn của chunk phụ

## 9. Các mode đóng gói nên có

### 9.1 `single_question_precise`

Dùng khi:

- chỉ tạo 1 câu
- cần tối ưu cost/token

Chiến lược:

- 1 primary chunk
- 0-2 supporting chunks
- token budget chặt

### 9.2 `small_batch`

Dùng khi:

- tạo 3-5 câu cùng topic/chapter

Chiến lược:

- 1 pack dùng chung
- nhiều chunk hơn một chút
- amortize cost tốt hơn

### 9.3 `coverage_exam`

Dùng khi:

- cần phủ nhiều topic
- tạo mini exam hoặc set câu hỏi đa dạng

Chiến lược:

- planner tạo nhiều pack nhỏ theo từng topic
- mỗi pack sinh 1-2 câu
- tránh 1 request khổng lồ

## 10. Luồng production đề xuất

### 10.1 Tạo câu hỏi

```text
Request create question
-> Retrieval Planner
-> Candidate search
-> Re-rank
-> Context pack build/load
-> Question Generator
-> Question Reviewer
-> Save logs + context pack refs
```

### 10.2 Review câu hỏi

Review không nhất thiết phải đọc lại toàn bộ raw chunks.

Nên ưu tiên:

- `packed_summary_text`
- `packed_context_text`
- và các `source_chunk_ids` đã được trích dẫn

### 10.3 Mentor / giải thích

Mentor cũng có thể dùng cùng kiến trúc này nếu cần giải thích dựa trên tri thức môn học, không chỉ code submission.

## 11. Trường cần có trong KnowledgeChunks để hỗ trợ kiến trúc này

Tối thiểu nên có:

- `chapter_id`
- `chapter_title`
- `chunk_path`
- `chunk_summary`
- `concept_keywords`
- `topic_primary`
- `topic_secondary`
- `prerequisite_tags`
- `estimated_difficulty`
- `assessment_value_score`
- `content_quality_score`
- `retrieval_score_boost`
- `duplicate_group_id`

## 12. Trường cần có trong AI_Context_Packs

Tối thiểu nên có:

- `pack_strategy`
- `target_topics`
- `source_chunk_ids`
- `retrieval_scores`
- `packed_summary_text`
- `packed_context_text`
- `token_count`
- `source_token_count`
- `compression_ratio`
- `recommended_question_count`

## 13. Chiến lược tối ưu cost

### 13.1 Với 1 câu hỏi đơn

Không nên đọc quá nhiều chunk.

Nên:

- dùng `single_question_precise`
- giới hạn token chặt
- ưu tiên pack đã cache
- ưu tiên summary-first

### 13.2 Với nhiều câu hỏi cùng topic

Nên:

- build 1 context pack đủ mạnh
- tạo 3-5 câu trên cùng pack
- cost/câu sẽ thấp hơn rõ rệt

### 13.3 Với tài liệu cực lớn

Không query toàn bộ tài liệu mỗi lần.

Nên:

- precompute chapter/topic summaries
- prebuild pack cho topic hot
- chỉ re-pack động cho case mới hoặc BYOS

## 14. Trạng thái prototype hiện tại

Prototype hiện tại đã có:

- extraction
- chunking
- embedding
- tagging
- retrieval planner
- context pack store
- generation/review

Prototype hiện tại đã hỗ trợ:

- `POST /api/ai-module/retrieval/plan`
- `POST /api/ai-module/retrieval/plan/debug`
- `POST /api/ai-module/context-packs/build`
- `GET /api/ai-module/context-packs/{packId}`
- `GET /api/ai-module/context-packs`
- `POST /api/ai-module/context-packs/{packId}/mark-stale`
- `generation-review` nhận:
  - `chunks`
  - `contextPackId`
  - `retrieval`

Những gì vẫn còn là bước tối ưu tiếp theo:

1. siết token ratio tốt hơn cho chunk trung bình/dài
2. thêm rerank/vector retrieval thật khi nối DB/vector store
3. thêm context-pack reuse policy chặt hơn
4. thêm telemetry so sánh `raw chunk mode` với `context pack mode`

## 15. Kết luận

Kiến trúc retrieval + context packing là tầng bắt buộc nếu hệ thống muốn:

- chạy trên tài liệu lớn
- kiểm soát chi phí tốt
- giữ grounding mạnh
- giải thích được cost theo nghiệp vụ
- scale sang production/BYOS mà không bị bùng token input
