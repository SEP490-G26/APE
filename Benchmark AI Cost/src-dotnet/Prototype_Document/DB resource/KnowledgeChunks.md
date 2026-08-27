{
  _id: ObjectId,                   // ID duy nhất của bản ghi chunk

  course_id: ObjectId,             // ID môn học mà chunk này thuộc về; có thể null nếu là tài liệu BYOS chưa gắn môn
  document_id: ObjectId,           // ID tài liệu gốc sinh ra chunk này
  user_id: ObjectId,               // ID người sở hữu tài liệu/chunk; dùng để phân biệt dữ liệu hệ thống và dữ liệu BYOS

  chunk_index: Number,             // Thứ tự của chunk trong cùng một document, dùng để truy vết và lấy chunk lân cận
  chunking_strategy: String,       // Chiến lược chia chunk: logical_block | page_block | fixed_words
  chunk_type: String,              // Loại chunk: concept | paragraph | example | code | table | image_desc | mixed
  section_title: String,           // Tên section hoặc heading chính mà chunk này thuộc về

  source_type: String,             // Loại file nguồn: pdf | docx | pptx | txt | image
  source_name: String,             // Tên file nguồn gốc
  source_page_from: Number,        // Số trang bắt đầu mà chunk này lấy nội dung từ đó
  source_page_to: Number,          // Số trang kết thúc mà chunk này lấy nội dung từ đó

  language: String,                // Ngôn ngữ chính của nội dung chunk: vi | en | mixed
  subject_code: String,            // Mã môn học/ngữ cảnh học tập: C_BASIC | JAVA_OOP | DSA_OOP

  raw_text: String,                // Nội dung text gốc sau khi parser/vision extract ra
  normalized_text: String,         // Nội dung đã được làm sạch để phục vụ embedding và retrieval tốt hơn
  markdown_text: String,           // Nội dung ở dạng markdown để giữ cấu trúc heading/list/code block nếu cần hiển thị hoặc debug

  topic_tags: [String],            // Danh sách tag chủ đề chính gắn cho chunk, ví dụ: ["array", "oop"]

  embedding: [Number],             // Vector embedding của chunk để phục vụ semantic search / retrieval
  embedding_provider: String,      // Provider dùng để tạo embedding, ví dụ: cohere | openai
  embedding_model: String,         // Tên model embedding đã dùng
  embedding_dim: Number,           // Số chiều của vector embedding, ví dụ 1024 hoặc 1536

  tagging_provider: String,        // Provider dùng để auto tagging
  tagging_model: String,           // Tên model dùng để sinh topic tags

  word_count: Number,              // Số lượng từ của chunk, phục vụ benchmark và kiểm soát kích thước chunk
  token_count: Number,             // Số token ước lượng/thực tế của chunk, phục vụ tính cost AI
  char_count: Number,              // Số ký tự của chunk, hỗ trợ thống kê và fallback estimate token

  retrieval_enabled: Boolean,      // Chunk này có được phép tham gia vector search / retrieval hay không
  status: String,                  // Trạng thái chunk: active | pending | archived | failed

  created_at: DateTime,            // Thời điểm tạo bản ghi chunk
  updated_at: DateTime             // Thời điểm cập nhật gần nhất của bản ghi chunk
}