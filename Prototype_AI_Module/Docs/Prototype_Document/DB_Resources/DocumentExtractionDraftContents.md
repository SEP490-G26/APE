{
  _id: ObjectId,                              // ID duy nhất của một segment nội dung thuộc bản nháp extraction

  extraction_draft_id: ObjectId,              // Ref -> DocumentExtractionDrafts._id
  document_id: ObjectId,                      // Ref logic tới tài liệu gốc để query nhanh
  user_id: ObjectId,                          // Ref -> User._id
  course_id: ObjectId,                        // Ref -> Course._id; có thể null với BYOS

  segment_index: Number,                      // Thứ tự segment trong cùng một tài liệu
  segment_type: String,                       // page_block | logical_block | image_block | mixed_block
  section_title: String,                      // Tiêu đề/heading gần nhất mà segment này thuộc về

  source_page_from: Number,                   // Trang/slide bắt đầu của segment
  source_page_to: Number,                     // Trang/slide kết thúc của segment
  source_locator: String,                     // Vị trí nguồn chi tiết hơn nếu cần: page_12 | slide_05 | section_3.2

  raw_text: String,                           // Text thô của riêng segment này sau parser/lib
  clean_markdown: String,                     // Markdown sạch của riêng segment này sau cleanup + vision expansion
  approved_markdown: String,                  // Markdown cuối cùng đã được human duyệt cho segment này

  detected_image_refs: [String],              // Danh sách ảnh xuất hiện trong segment
  vision_expansions: [                         // Mô tả ảnh của riêng segment
    {
      image_ref: String,                      // Định danh ảnh hoặc path/object key của ảnh
      placement_hint: String,                 // inline_after_marker | page_end | section_end
      description_markdown: String,           // Markdown mô tả ảnh do vision trả về
      provider: String,                       // Provider vision
      model: String,                          // Model vision
      input_tokens: Number,                   // Input token cho lần vision call này
      output_tokens: Number,                  // Output token cho lần vision call này
      cost_usd: Number,                       // Cost USD cho lần vision call này
      latency_ms: Number,                     // Latency của lần vision call này
      status: String                          // completed | skipped | failed
    }
  ],

  cleanup_warnings: [String],                 // Warning riêng của segment này
  review_status: String,                      // extracted | needs_review | approved | rejected
  reviewed_by: ObjectId,                      // Người review segment này
  reviewed_at: DateTime,                      // Thời điểm review segment gần nhất
  approval_version: Number,                   // Số lần chốt chỉnh sửa ở segment này

  word_count: Number,                         // Số từ của segment sau clean
  token_count: Number,                        // Số token ước lượng hoặc thực tế của segment
  char_count: Number,                         // Số ký tự của segment

  extraction_input_tokens: Number,            // Token input dùng để xử lý segment này
  extraction_output_tokens: Number,           // Token output dùng để xử lý segment này
  extraction_cost_usd: Number,                // Cost USD của riêng segment này
  extraction_latency_ms: Number,              // Latency của riêng segment này

  created_at: DateTime,                       // Thời điểm tạo segment
  updated_at: DateTime                        // Thời điểm cập nhật gần nhất
}

## Vai trò

Đây là collection con chứa toàn bộ phần text/markdown dài của một tài liệu đã extraction.

Một tài liệu lớn sẽ có nhiều record `DocumentExtractionDraftContents`.

Ví dụ:

- giáo trình `1000` trang
- sau ingest + cleanup có thể chia thành `300` đến `800` segment logic

Khi đó:

- collection cha `DocumentExtractionDrafts` giữ metadata tổng
- collection con `DocumentExtractionDraftContents` giữ nội dung thực

## Tại sao phù hợp hơn cho human in the loop

Human không thể review thực tế trên một chuỗi markdown dài hàng trăm nghìn từ.

Chia segment giúp:

- review theo block/page/section
- approve từng phần
- sửa đâu lưu đó
- retry OCR/vision ở đúng segment lỗi

## Quan hệ với KnowledgeChunks

Sau khi segment đã `approved`, bước embedding sẽ đọc:

- `approved_markdown`

và sinh ra `KnowledgeChunks`.

Khuyến nghị `KnowledgeChunks` nên có thêm:

- `extraction_draft_id`
- `extraction_content_id`

để biết chunk này sinh ra từ segment nào của tài liệu nào.

## Index đề nghị

- `{ extraction_draft_id: 1, segment_index: 1 }`
- `{ document_id: 1, source_page_from: 1, source_page_to: 1 }`
- `{ review_status: 1, updated_at: -1 }`
- `{ extraction_draft_id: 1, review_status: 1 }`

