{
  _id: ObjectId,                              // ID duy nhất của bản nháp extraction cấp tài liệu

  course_id: ObjectId,                        // ID môn học nếu tài liệu thuộc học phần hệ thống; có thể null với BYOS
  document_id: ObjectId,                      // ID tài liệu gốc; dùng để nối với bảng Document hoặc metadata file gốc
  user_id: ObjectId,                          // ID người tải lên / sở hữu bản nháp extraction

  source_type: String,                        // Loại file nguồn: pdf | docx | pptx | txt | image
  source_name: String,                        // Tên file gốc do người dùng tải lên
  source_storage_path: String,                // Đường dẫn hoặc object key tới file gốc đã lưu
  source_size_bytes: Number,                  // Kích thước file gốc theo byte
  source_checksum: String,                    // Hash của file gốc để chống upload trùng và hỗ trợ cache OCR/extraction

  language: String,                           // Ngôn ngữ chính của tài liệu: vi | en | mixed
  subject_code: String,                       // Mã môn hoặc ngữ cảnh học tập: C_BASIC | JAVA_OOP | DSA_OOP | UNKNOWN
  ownership_type: String,                     // system_seeded | byos

  ingest_parser: String,                      // Tên parser/lib đã bóc nội dung thô: openxml-docx-parser | pdfpig | ...
  ingest_parser_version: String,              // Version parser/lib ở thời điểm chạy
  extraction_mode: String,                    // Chế độ extraction: text_only | full_multimodal_page | hybrid
  vision_provider: String,                    // Provider vision nếu có dùng mô tả ảnh; null nếu không dùng
  vision_model: String,                       // Model vision nếu có dùng

  total_pages: Number,                        // Tổng số trang/slide ước lượng của tài liệu
  total_segments: Number,                     // Tổng số segment con trong DocumentExtractionDraftContents
  approved_segments: Number,                  // Số segment đã được human approve
  rejected_segments: Number,                  // Số segment bị reject hoặc cần sửa lại

  detected_image_refs: [String],              // Danh sách ảnh được detect trong file nguồn
  embedded_image_count: Number,               // Số ảnh/marker ảnh được detect trong tài liệu
  cleanup_warnings: [String],                 // Các cảnh báo tổng quan khi normalize: page noise, unreadable blocks, ...
  human_notes: String,                        // Ghi chú ngắn của người review ở mức tài liệu nếu cần

  raw_text_preview: String,                   // Đoạn preview ngắn của raw extracted text để xem nhanh trên UI/admin
  clean_markdown_preview: String,             // Đoạn preview ngắn của clean markdown
  approved_markdown_preview: String,          // Đoạn preview ngắn của approved markdown

  review_status: String,                      // draft | extracted | needs_review | partially_approved | approved | rejected | embedded
  reviewed_by: ObjectId,                      // ID người review/approve; có thể chính là user upload
  reviewed_at: DateTime,                      // Thời điểm human review gần nhất
  approval_version: Number,                   // Số lần duyệt/chỉnh sửa đã chốt ở cấp tài liệu

  extraction_input_tokens: Number,            // Tổng input token cho bước extraction/vision ở cấp tài liệu
  extraction_output_tokens: Number,           // Tổng output token cho bước extraction/vision ở cấp tài liệu
  extraction_cost_usd: Number,                // Tổng chi phí USD của bước extraction/vision
  extraction_latency_ms: Number,              // Tổng thời gian chạy extraction/vision

  credit_charge_status: String,               // pending | charged | waived | refunded
  credit_charge_amount: Number,               // Số credit đã trừ cho bước xử lý tài liệu này
  pricing_plan_code: String,                  // Mã gói giá đang áp dụng cho BYOS ingestion
  pricing_basis_snapshot: {                   // Snapshot để audit cách tính credit tại thời điểm xử lý
    source_size_bytes: Number,                // Kích thước file tại thời điểm tính giá
    total_pages: Number,                      // Số trang tại thời điểm tính giá
    total_words_estimate: Number,             // Ước lượng số từ toàn tài liệu
    embedded_image_count: Number,             // Số ảnh nhúng trong tài liệu
    extraction_input_tokens: Number,          // Token input extraction dùng làm căn cứ tính credit
    extraction_output_tokens: Number,         // Token output extraction dùng làm căn cứ tính credit
    extraction_cost_usd: Number               // USD cost thực tế hoặc estimate dùng để map sang credit
  },

  chunking_ready: Boolean,                    // Đã đủ điều kiện đưa sang embedding chưa
  chunk_count: Number,                        // Số chunk sinh ra sau khi approved và embedding; 0 nếu chưa chạy
  last_embedding_run_id: String,              // ID run embedding gần nhất để trace pipeline

  created_at: DateTime,                       // Thời điểm tạo bản nháp extraction
  updated_at: DateTime                        // Thời điểm cập nhật gần nhất
}

## Vai trò

Đây là collection cha ở cấp tài liệu cho pipeline:

`Upload tài liệu -> Ingest parser/lib -> Extracted Content -> Human review -> Approved markdown -> Embedding`

Collection này không giữ toàn bộ full text dài của tài liệu. Nó giữ:

- metadata file nguồn
- trạng thái pipeline
- tổng hợp token/cost/credit
- preview để xem nhanh
- thống kê số segment con

Toàn bộ nội dung dài sẽ nằm ở collection con:

- `DocumentExtractionDraftContents`

## Tại sao phải tách collection con

Nếu một giáo trình có `500k từ` hoặc hơn, việc nhét toàn bộ:

- `raw_extracted_text`
- `clean_markdown`
- `approved_markdown`

vào một document Mongo là thiết kế không bền.

Tách `master + content segments` giúp:

- tránh document Mongo quá lớn
- human review theo từng phần thực tế hơn
- sửa đâu lưu đó, không phải update lại cả cuốn sách
- retry OCR/vision theo segment nếu có lỗi
- tính giá credit chi tiết hơn theo độ lớn thật của tài liệu

## Quan hệ đề nghị

- `document_id` -> bảng `Document` hoặc metadata file gốc của hệ thống
- `user_id` -> bảng `User`
- `course_id` -> bảng `Course`
- `DocumentExtractionDraftContents.extraction_draft_id` -> `DocumentExtractionDrafts._id`
- `KnowledgeChunks.document_id` nên trỏ ngược về cùng `document_id`
- nên bổ sung:
  - `KnowledgeChunks.extraction_draft_id`
  - `KnowledgeChunks.extraction_content_id`

## Cách dùng với BYOS và credit

Collection này là nơi phù hợp để chốt việc tính credit cho ingestion tài liệu cá nhân.

Lý do:

- 1 file BYOS có kích thước và độ phức tạp khác nhau
- chi phí AI extraction/vision sẽ khác nhau rõ rệt
- cần lưu snapshot để giải thích tại sao lần xử lý đó bị trừ từng ấy credit

Các trường nên dùng để làm căn cứ set giá:

- `source_size_bytes`
- `total_pages`
- `pricing_basis_snapshot.total_words_estimate`
- `embedded_image_count`
- `extraction_input_tokens`
- `extraction_output_tokens`
- `extraction_cost_usd`
- `credit_charge_amount`

## Index đề nghị

- `{ user_id: 1, review_status: 1, updated_at: -1 }`
- `{ document_id: 1, approval_version: -1 }`
- `{ source_checksum: 1 }`
- `{ course_id: 1, subject_code: 1, review_status: 1 }`
- `{ ownership_type: 1, credit_charge_status: 1, created_at: -1 }`

