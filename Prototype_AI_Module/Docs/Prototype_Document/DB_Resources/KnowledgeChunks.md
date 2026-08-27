# KnowledgeChunks

```javascript
{
  _id: ObjectId,                          // ID duy nhất của bản ghi chunk

  course_id: ObjectId,                    // ID môn học mà chunk này thuộc về; có thể null nếu là tài liệu BYOS chưa gắn môn
  document_id: ObjectId,                  // ID tài liệu gốc sinh ra chunk này
  user_id: ObjectId,                      // ID người sở hữu tài liệu/chunk; dùng để phân biệt dữ liệu hệ thống và dữ liệu BYOS
  extraction_draft_id: ObjectId,          // ID bản nháp extraction cấp tài liệu mà chunk này sinh ra từ đó
  extraction_content_id: ObjectId,        // ID segment nội dung đã approved mà chunk này được cắt ra từ đó

  chunk_index: Number,                    // Thứ tự của chunk trong cùng một document, dùng để truy vết và lấy chunk lân cận
  chunking_strategy: String,              // Chiến lược chia chunk: logical_block | page_block | semantic_section | hybrid_contextual
  chunk_type: String,                     // Loại chunk: heading | concept | paragraph | example | code | table | image_desc | mixed
  section_title: String,                  // Tên section hoặc heading chính mà chunk này thuộc về
  chapter_id: String,                     // ID logic của chương/phần để nhóm các chunk cùng chapter
  chapter_title: String,                  // Tên chương/phần lớn mà chunk này thuộc về
  chunk_path: [String],                   // Đường dẫn phân cấp heading, ví dụ ["Chapter 1", "Variables", "Naming Rules"]

  source_type: String,                    // Loại file nguồn: pdf | docx | pptx | txt | image | md
  source_name: String,                    // Tên file nguồn gốc
  source_page_from: Number,               // Số trang bắt đầu mà chunk này lấy nội dung từ đó
  source_page_to: Number,                 // Số trang kết thúc mà chunk này lấy nội dung từ đó
  source_block_refs: [String],            // Danh sách block/page/shape/image ref từ extraction layer để truy vết đúng vị trí nguồn

  language: String,                       // Ngôn ngữ chính của nội dung chunk: vi | en | mixed
  subject_code: String,                   // Mã môn học/ngữ cảnh học tập: C_BASIC | JAVA_OOP | DSA_JAVA
  syllabus_scope: String,                 // SYSTEM_CORE | BYOS | HYBRID

  raw_text: String,                       // Nội dung text gốc sau khi parser/vision extract ra
  normalized_text: String,                // Nội dung đã được làm sạch để phục vụ embedding và retrieval tốt hơn
  markdown_text: String,                  // Nội dung ở dạng markdown để giữ cấu trúc heading/list/code block nếu cần hiển thị hoặc debug
  chunk_summary: String,                  // Tóm tắt ngắn 1-3 câu của chunk để phục vụ retrieval packing và giảm token đầu vào
  concept_keywords: [String],             // Từ khoá khái niệm cốt lõi đã chuẩn hoá của chunk

  topic_tags: [String],                   // Danh sách tag chủ đề chính gắn cho chunk, ví dụ ["array", "oop"]
  topic_primary: String,                  // Tag chủ đạo nhất của chunk
  topic_secondary: [String],              // Tag phụ để hỗ trợ re-rank và group theo chủ đề
  prerequisite_tags: [String],            // Tag tiên quyết; dùng khi context packing cần lấy thêm chunk bổ trợ

  embedding: [Number],                    // Vector embedding của chunk để phục vụ semantic search / retrieval
  embedding_provider: String,             // Provider dùng để tạo embedding, ví dụ: cohere | openai
  embedding_model: String,                // Tên model embedding đã dùng
  embedding_dim: Number,                  // Số chiều của vector embedding, ví dụ 1024 hoặc 1536

  tagging_provider: String,               // Provider dùng để auto tagging
  tagging_model: String,                  // Tên model dùng để sinh topic tags

  word_count: Number,                     // Số lượng từ của chunk, phục vụ benchmark và kiểm soát kích thước chunk
  token_count: Number,                    // Số token ước lượng/thực tế của chunk, phục vụ tính cost AI
  char_count: Number,                     // Số ký tự của chunk, hỗ trợ thống kê và fallback estimate token

  estimated_difficulty: String,           // Ước lượng độ khó tri thức của chunk: Easy | Medium | Hard
  assessment_value_score: Number,         // Điểm đánh giá chunk có hữu ích để tạo câu hỏi hay không (0-1)
  content_quality_score: Number,          // Điểm chất lượng nội dung sau extraction/filtering (0-1)
  retrieval_score_boost: Number,          // Hệ số boost tĩnh để ưu tiên chunk quan trọng trong search/re-rank
  duplicate_group_id: String,             // ID nhóm chunk gần trùng nhau để retrieval tránh lấy lặp

  retrieval_enabled: Boolean,             // Chunk này có được phép tham gia vector search / retrieval hay không
  status: String,                         // Trạng thái chunk: active | pending | archived | failed | filtered

  created_at: DateTime,                   // Thời điểm tạo bản ghi chunk
  updated_at: DateTime                    // Thời điểm cập nhật gần nhất của bản ghi chunk
}
```

## Các nhóm trường quan trọng

### 1. Truy vết nguồn

- `document_id`, `extraction_draft_id`, `extraction_content_id`
- `source_page_from`, `source_page_to`, `source_block_refs`
- `chunk_index`, `chunk_path`

Nhóm này giúp truy vết chính xác chunk sinh ra từ đâu, rất quan trọng cho:

- human-in-the-loop
- debug extraction/chunking
- audit kết quả AI

### 2. Tổ chức tri thức

- `section_title`
- `chapter_id`, `chapter_title`
- `topic_primary`, `topic_secondary`, `topic_tags`
- `concept_keywords`, `prerequisite_tags`

Nhóm này giúp retrieval không phải chỉ dựa vào vector. Hệ thống có thể filter theo môn, chapter, topic rồi mới semantic search.

### 3. Tối ưu token và context packing

- `chunk_summary`
- `token_count`
- `assessment_value_score`
- `content_quality_score`
- `duplicate_group_id`

Đây là các trường mới quan trọng cho production. Nếu không có chúng, việc tạo đề từ tài liệu lớn sẽ rất dễ đưa quá nhiều chunk vào LLM và làm chi phí tăng mạnh.

## Phân lớp field để chốt DB

### 1. Core required fields

Đây là nhóm nên có ngay từ vòng đầu khi dựng collection production:

- identity và ownership:
  - `_id`
  - `course_id`
  - `document_id`
  - `user_id`
- extraction trace:
  - `extraction_draft_id`
  - `extraction_content_id`
  - `chunk_index`
  - `chunking_strategy`
  - `chunk_type`
  - `section_title`
  - `source_type`
  - `source_name`
  - `source_page_from`
  - `source_page_to`
- learning context:
  - `language`
  - `subject_code`
  - `syllabus_scope`
- content payload:
  - `raw_text`
  - `normalized_text`
  - `markdown_text`
- retrieval payload:
  - `topic_tags`
  - `topic_primary`
  - `embedding`
  - `embedding_provider`
  - `embedding_model`
  - `embedding_dim`
  - `tagging_provider`
  - `tagging_model`
- stats and lifecycle:
  - `word_count`
  - `token_count`
  - `char_count`
  - `retrieval_enabled`
  - `status`
  - `created_at`
  - `updated_at`

### 2. Recommended production fields

Đây là nhóm rất nên có nếu hệ thống sẽ chạy retrieval + context packing thật:

- structure and navigation:
  - `chapter_id`
  - `chapter_title`
  - `chunk_path`
  - `source_block_refs`
- retrieval quality:
  - `chunk_summary`
  - `concept_keywords`
  - `topic_secondary`
  - `prerequisite_tags`
  - `estimated_difficulty`
  - `assessment_value_score`
  - `content_quality_score`
  - `retrieval_score_boost`
  - `duplicate_group_id`
- invalidation and provenance:
  - `content_version`
  - `source_checksum`
  - `is_human_edited`
  - `approved_from_draft_version`
  - `approval_status`
- model/pipeline trace:
  - `embedding_version`
  - `tagging_version`

### 3. Optional benchmark and audit fields

Đây là nhóm nên thêm khi bắt đầu tối ưu benchmark, cache và audit sâu:

- dedupe and change detection:
  - `normalized_text_hash`
  - `markdown_text_hash`
- retrieval tuning:
  - `neighbor_chunk_ids`
  - `retrieval_notes`
- usage/cost trace:
  - `token_count_source`
  - `last_embedding_at`

## Khuyến nghị bổ sung thêm

Các field dưới đây không bắt buộc để chạy prototype hiện tại, nhưng rất đáng cân nhắc khi chốt DB production:

### 1. Phục vụ dedupe và audit

- `normalized_text_hash: String`
  - hash của `normalized_text`
  - dùng để phát hiện chunk trùng rất nhanh mà không cần so text full

- `markdown_text_hash: String`
  - hash của `markdown_text`
  - hữu ích nếu muốn phát hiện thay đổi cấu trúc hiển thị/debug

### 2. Phục vụ retrieval tốt hơn

- `neighbor_chunk_ids: [ObjectId]`
  - ID chunk trước/sau trong cùng document
  - dùng khi planner cần mở rộng ngữ cảnh lân cận thay vì query lại bằng index

- `retrieval_notes: [String]`
  - note ngắn kiểu `short_chunk_passthrough`, `summary_ready`, `low_signal`
  - giúp debug vì sao chunk được giữ, boost hay loại

### 3. Phục vụ cost / benchmark chính xác hơn

- `token_count_source: String`
  - `estimated | provider_raw | cached_estimate`
  - phân biệt token count là ước lượng hay lấy từ usage thật

- `last_embedding_at: DateTime`
  - thời điểm gần nhất embedding của chunk được tạo/cập nhật
  - hữu ích khi kiểm soát re-embed

### 4. Phục vụ invalidation

- `content_version: Number`
  - tăng khi chunk bị regenerate từ extraction draft hoặc bị chỉnh sửa
  - rất hữu ích để đánh stale các `AI_Context_Packs` dùng chunk cũ

- `source_checksum: String`
  - hash ổn định của block nguồn hoặc segment nguồn trước khi chunking
  - dùng để phát hiện thay đổi nội dung thật sự, tránh re-embed vô ích khi chỉ đổi metadata

### 5. Phục vụ provenance và human-in-the-loop

- `is_human_edited: Boolean`
  - cho biết chunk hiện tại có bị chỉnh tay sau extraction/cleanup hay không
  - quan trọng để audit chất lượng vì dữ liệu benchmark và dữ liệu production có thể khác nhau ở bước review tay

- `approved_from_draft_version: Number`
  - version của extraction draft tại thời điểm chunk này được sinh ra
  - giúp biết chunk đang bám vào approval snapshot nào

- `approval_status: String`
  - `approved | system_generated | superseded`
  - nên có nếu sau này muốn tách rõ chunk chỉ mới sinh tự động và chunk đã đi qua human review

### 6. Phục vụ trace model/config

- `embedding_version: String`
  - version logic của pipeline embedding, ví dụ `cohere-embed-v1`
  - khác với `embedding_model`; field này dùng để trace thay đổi prompt/preprocess/chunk-cleaning

- `tagging_version: String`
  - version logic của autotagging pipeline
  - hữu ích khi chất lượng tag thay đổi nhưng model name vẫn giữ nguyên

## Ghi chú thực tế với runtime hiện tại

- Prototype runtime hiện đã dùng trực tiếp các field:
  - `chunk_summary`
  - `concept_keywords`
  - `topic_primary`
  - `assessment_value_score`
  - `content_quality_score`
  - `retrieval_score_boost`
  - `duplicate_group_id`
- Vì vậy các field trên nên được xem là nhóm `production-critical`, không còn là metadata phụ.
- Các field như `embedding_version`, `tagging_version`, `approved_from_draft_version`, `is_human_edited` chưa bắt buộc cho runtime hiện tại, nhưng rất nên chốt từ sớm trong DB design để tránh phải migration lại khi bắt đầu audit chất lượng hoặc rebuild retrieval cache.

## Gợi ý index Mongo

### Index bắt buộc

- `{ course_id: 1, subject_code: 1, status: 1, retrieval_enabled: 1 }`
- `{ document_id: 1, chunk_index: 1 }`
- `{ extraction_draft_id: 1, chunk_index: 1 }`
- `{ topic_primary: 1, subject_code: 1 }`
- `{ topic_tags: 1, subject_code: 1 }`
- `{ chapter_id: 1, subject_code: 1 }`

### Index khuyến nghị

- `{ estimated_difficulty: 1, subject_code: 1 }`
- `{ duplicate_group_id: 1 }`
- `{ assessment_value_score: -1 }`

### Vector index

- vector index trên `embedding`
- nên kết hợp với metadata filter theo `subject_code`, `course_id`, `retrieval_enabled`, `status`

## Ghi chú quan hệ

- `extraction_draft_id` -> `DocumentExtractionDrafts._id`
- `extraction_content_id` -> `DocumentExtractionDraftContents._id`
- `document_id` -> collection tài liệu gốc của hệ thống
- `course_id` -> collection course
- `user_id` -> collection user

## Vai trò trong pipeline production

`KnowledgeChunks` không chỉ là nơi lưu embedding. Đây là tầng tri thức trung gian để:

1. retrieval candidate search
2. topic-based filtering
3. context packing
4. question generation
5. mentor grounding / evidence trace

Nếu thiết kế collection này quá tối giản, hệ thống sẽ phải đẩy quá nhiều raw chunk vào LLM, dẫn tới:

- token input cao
- latency cao
- cost tăng mạnh
- khó kiểm soát chất lượng grounding
