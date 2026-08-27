# AI_Context_Packs

## Mục tiêu

`AI_Context_Packs` là collection trung gian dùng để gom một tập `KnowledgeChunks` đã được chọn lọc và nén ngữ cảnh theo đúng mục đích AI downstream.

Collection này giải quyết trực tiếp bài toán production:

- không đẩy hàng chục nghìn token raw chunks vào một request tạo đề
- tái sử dụng context đã được pack tốt cho nhiều lần generate/review
- giảm chi phí khi tạo 1 câu hỏi hoặc 1 batch câu hỏi theo cùng topic

## Schema đề xuất

```javascript
{
  _id: ObjectId,                           // ID duy nhất của context pack

  course_id: ObjectId,                     // Môn học liên quan
  document_id: ObjectId,                   // Tài liệu nguồn chính; có thể null nếu pack ghép từ nhiều tài liệu
  user_id: ObjectId,                       // Chủ sở hữu pack; hỗ trợ BYOS

  pack_id: String,                         // ID logic để trace trong app/log
  pack_type: String,                       // generation_context | review_context | mentor_context | retrieval_debug
  pack_strategy: String,                   // topic_focused | chapter_focused | difficulty_balanced | summary_first | hybrid
  pack_status: String,                     // draft | active | stale | archived

  subject_code: String,                    // C_BASIC | JAVA_OOP | DSA_JAVA
  question_type: String,                   // FE | PE | null nếu dùng cho tác vụ khác
  target_difficulty: String,               // Easy | Medium | Hard | Mixed
  target_topics: [String],                 // Các topic mà pack này nhắm tới
  chapter_scope: [String],                 // Các chapter/section logic được phủ trong pack
  syllabus_scope: String,                  // SYSTEM_CORE | BYOS | HYBRID

  source_chunk_ids: [ObjectId],            // Các chunk thật được dùng để tạo pack
  source_chunk_refs: [                     // Snapshot ref gọn để UI/debug không phải query ngược toàn bộ ngay lập tức
    {
      chunk_id: ObjectId,
      chunk_index: Number,
      section_title: String,
      topic_primary: String,
      token_count: Number,
      retrieval_rank: Number
    }
  ],

  retrieval_query: String,                 // Query hoặc ý định retrieval đã dùng để tạo pack
  retrieval_filters: Object,               // Metadata filter dùng ở bước retrieval
  retrieval_scores: [                      // Điểm score của các chunk được chọn
    {
      chunk_id: ObjectId,
      vector_score: Number,
      lexical_score: Number,
      topic_score: Number,
      boost_score: Number,
      final_score: Number
    }
  ],

  packed_summary_text: String,             // Bản tóm tắt pack để dùng khi cần context ngắn
  packed_context_text: String,             // Context text cuối cùng đã được nén để đưa cho AI
  packed_markdown_text: String,            // Biến thể markdown dễ đọc/debug/export
  context_windows: [                       // Các cụm nhỏ bên trong pack để planner dùng lại
    {
      window_id: String,
      window_type: String,                 // primary | supporting | prerequisite | example | code
      source_chunk_ids: [ObjectId],
      token_count: Number,
      summary_text: String
    }
  ],

  token_count: Number,                     // Tổng token của packed_context_text
  source_token_count: Number,              // Tổng token của raw chunks trước khi pack
  compression_ratio: Number,               // token_count / source_token_count
  recommended_question_count: Number,      // Số câu hỏi nên generate trên pack này để cost tối ưu
  max_question_count: Number,              // Ngưỡng trên nên dùng với pack này

  usage_count: Number,                     // Số lần pack đã được dùng
  last_used_at: DateTime,                  // Lần gần nhất pack được dùng
  expires_at: DateTime,                    // Hạn làm stale/rebuild nếu dữ liệu nguồn đổi

  created_from_run_id: String,             // Pipeline run tạo ra pack này
  created_at: DateTime,
  updated_at: DateTime
}
```

## Lưu ý giữa DB-ready schema và runtime hiện tại

Prototype runtime hiện tại đã có các field lõi sau:

- `pack_id`
- `pack_type`
- `pack_strategy`
- `pack_status`
- `user_id`
- `course_id`
- `document_id`
- `subject_code`
- `question_type`
- `target_difficulty`
- `target_topics`
- `syllabus_scope`
- `retrieval_query`
- `source_chunk_ids`
- `retrieved_chunks`
- `chunks`
- `packed_summary_text`
- `packed_context_text`
- `token_count`
- `source_token_count`
- `compression_ratio`
- `recommended_question_count`
- `max_question_count`
- `usage_count`
- `last_used_at`
- `expires_at`
- `created_at`
- `updated_at`

Các field như:

- `chapter_scope`
- `source_chunk_refs`
- `retrieval_filters`
- `retrieval_scores`
- `packed_markdown_text`
- `context_windows`
- `created_from_run_id`

hiện đang là nhóm `DB-ready target fields`, rất nên có khi nối production thật để tăng khả năng audit, debug và reuse.

## Khi nào cần tạo context pack

### 1. Tạo pack động theo request

Áp dụng khi:

- người dùng chọn topic cụ thể
- tài liệu BYOS mới ingest
- chưa có pack phù hợp trong cache

### 2. Tạo pack sẵn theo syllabus

Áp dụng khi:

- tài liệu core của môn ít thay đổi
- cần tối ưu cost cho production
- có các topic/chapter xuất hiện lặp lại nhiều

Ví dụ:

- `JAVA_OOP / constructor / FE / Easy`
- `C_BASIC / pointer / PE / Medium`
- `DSA_JAVA / linked-list / PE / Medium`

## Quan hệ với collection khác

- `source_chunk_ids` -> `KnowledgeChunks._id`
- `document_id` -> collection tài liệu gốc
- `course_id` -> collection course
- `user_id` -> collection user

## Vai trò trong production

`AI_Context_Packs` là lớp đệm giữa retrieval và generation/review.

Luồng đúng nên là:

`KnowledgeChunks -> Retrieval -> Re-rank -> Context Packing -> AI_Context_Packs -> Generation/Review`

Nếu bỏ qua collection này, hệ thống thường sẽ mắc các vấn đề sau:

- mỗi request phải pack lại từ đầu
- khó tối ưu input token cho trường hợp tạo 1 câu hỏi đơn
- khó benchmark cost thực theo từng topic
- khó tái sử dụng ngữ cảnh đã được xác nhận là tốt

## Gợi ý index Mongo

- `{ subject_code: 1, question_type: 1, target_difficulty: 1, pack_status: 1 }`
- `{ target_topics: 1, subject_code: 1 }`
- `{ chapter_scope: 1, subject_code: 1 }`
- `{ usage_count: -1, last_used_at: -1 }`
- `{ expires_at: 1 }`

## Phân lớp field để chốt DB

### 1. Core required fields

Đây là nhóm nên có ngay nếu muốn dùng `AI_Context_Packs` trong production:

- identity và ownership:
  - `_id`
  - `pack_id`
  - `course_id`
  - `document_id`
  - `user_id`
- routing:
  - `pack_type`
  - `pack_strategy`
  - `pack_status`
  - `subject_code`
  - `question_type`
  - `target_difficulty`
  - `target_topics`
  - `syllabus_scope`
- source linkage:
  - `source_chunk_ids`
  - `retrieval_query`
- packed payload:
  - `packed_summary_text`
  - `packed_context_text`
- cost and lifecycle:
  - `token_count`
  - `source_token_count`
  - `compression_ratio`
  - `recommended_question_count`
  - `max_question_count`
  - `usage_count`
  - `last_used_at`
  - `expires_at`
  - `created_at`
  - `updated_at`

### 2. Recommended production fields

Đây là nhóm rất nên có khi nối retrieval planner, run history và cache reuse thật:

- trace and audit:
  - `chapter_scope`
  - `source_chunk_refs`
  - `retrieval_filters`
  - `retrieval_scores`
  - `packed_markdown_text`
  - `context_windows`
  - `created_from_run_id`
  - `last_used_run_id`
- invalidation and cache:
  - `source_signature_hash`
  - `stale_reason`
  - `generation_mode`
  - `max_packed_tokens`
  - `used_cached_pack`
- cost transparency:
  - `token_count_source`
  - `cost_estimate_usd`
- reproducibility:
  - `planner_version`
  - `packing_policy_version`
  - `cache_key`

### 3. Optional benchmark and audit fields

Đây là nhóm nên thêm khi bắt đầu tối ưu benchmark fairness và quality scoring:

- source-of-truth snapshot:
  - `prompt_version_refs`
  - `rubric_version_refs`
- benchmark metadata:
  - `benchmark_label`
- quality analytics:
  - `pack_quality_score`

## Khuyến nghị bổ sung thêm

Các field dưới đây nên được cân nhắc bổ sung khi chốt collection production:

### 1. Phục vụ cache / invalidation

- `source_signature_hash: String`
  - hash của danh sách `source_chunk_ids` + `content_version`
  - giúp xác định pack còn hợp lệ hay không mà không cần diff sâu

- `stale_reason: String`
  - ví dụ: `chunk_updated`, `prompt_changed`, `policy_changed`, `manual_invalidate`
  - giúp audit lý do rebuild

### 2. Phục vụ routing benchmark

- `generation_mode: String`
  - `single_question_precise | small_batch | coverage_exam`
  - rất nên lưu vì nó ảnh hưởng trực tiếp token budget và cost/câu

- `max_packed_tokens: Number`
  - token budget gốc dùng để build pack
  - hữu ích khi phân tích vì sao 2 pack cùng topic nhưng độ nén khác nhau

- `used_cached_pack: Boolean`
  - nếu pack được lấy lại từ cache trong một plan run
  - giúp tách biệt benchmark `build mới` và `reuse`

### 3. Phục vụ log / trace

- `created_from_run_id: String`
  - route/run nào tạo ra pack này
  - rất quan trọng để nối với `run-history` và `AI_Usage_Logs`

- `last_used_run_id: String`
  - run gần nhất dùng pack này
  - hữu ích khi debug vì sao pack được reuse nhiều lần

### 4. Phục vụ cost minh bạch hơn

- `token_count_source: String`
  - `estimated | request_payload_estimated | provider_raw`
  - cho biết con số token trong pack được tính theo kiểu nào

- `cost_estimate_usd: Number`
  - ước lượng chi phí downstream nếu dùng pack này cho 1 lần generate/review
  - hữu ích cho định giá credit trước khi gọi model

### 5. Phục vụ reproducibility và version snapshot

- `planner_version: String`
  - version của retrieval planner đã chọn chunk cho pack này
  - rất cần khi chất lượng retrieval thay đổi theo từng patch

- `packing_policy_version: String`
  - version của policy/context packing rules dùng để build pack
  - giúp giải thích vì sao cùng một topic nhưng pack text khác nhau giữa hai lần build

- `prompt_version_refs: [String]`
  - danh sách version prompt có liên quan trực tiếp tới pack
  - đặc biệt hữu ích nếu pack được build khác nhau cho `generation_context` và `review_context`

- `rubric_version_refs: [String]`
  - snapshot version rubric ảnh hưởng tới việc chọn context
  - nên có nếu reviewer hoặc planner có rule ưu tiên chunk theo rubric

### 6. Phục vụ cache routing và benchmark fairness

- `cache_key: String`
  - key logic để xác định pack này có thể reuse cho cùng request shape hay không
  - ví dụ ghép từ `subject + question_type + difficulty + topic set + max_packed_tokens + planner_version`

- `benchmark_label: String`
  - nhãn ngắn để đánh dấu pack phục vụ benchmark nào
  - hữu ích khi một pack được tạo riêng để so sánh nhiều model trên cùng context

- `pack_quality_score: Number`
  - điểm đánh giá nhanh chất lượng pack sau build, ví dụ dựa trên grounding coverage / redundancy / topic focus
  - chưa bắt buộc phải tính ngay, nhưng nên chừa note cho DB design vì field này rất hữu ích khi tối ưu retrieval sau này

## Ghi chú nghiệp vụ

### 1. Không thay thế KnowledgeChunks

`AI_Context_Packs` không phải nơi lưu tri thức gốc. Nó chỉ là lớp context đã tối ưu cho AI downstream.

### 2. Có thể rebuild

Khi:

- prompt retrieval thay đổi
- policy packing thay đổi
- chunk nguồn bị cập nhật
- score/rubric mới yêu cầu context khác

thì context pack có thể bị đánh `stale` và build lại.

### 3. Hỗ trợ tính credit/cost

Vì collection này lưu:

- `source_token_count`
- `token_count`
- `compression_ratio`
- `recommended_question_count`

nên hệ thống có thể dùng nó để:

- ước lượng chi phí trước khi generate
- định giá credit theo độ lớn tài liệu và độ khó pack
- giải thích vì sao tạo 1 câu đơn sẽ đắt hơn tạo theo batch

## Ghi chú thực tế với patch hiện tại

- Runtime hiện tại đã có `short-chunk passthrough`, nên không phải pack nào cũng nén xuống nhỏ hơn raw source.
- Với chunk ngắn, `compression_ratio` có thể gần `1.0`; đây là behavior đúng để cost estimate sát production hơn.
- Với tài liệu dài hoặc chunk lớn, `compression_ratio` mới là chỉ số thể hiện rõ hiệu quả context packing.
- Các field như `planner_version`, `packing_policy_version`, `cache_key`, `prompt_version_refs` hiện chưa bắt buộc trong runtime file-store, nhưng rất nên có trong DB production để đảm bảo reproducible benchmark và invalidation đúng khi source of truth AI thay đổi.
