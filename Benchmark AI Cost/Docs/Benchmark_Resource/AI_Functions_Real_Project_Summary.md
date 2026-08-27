# Tổng hợp chức năng AI cho dự án thật

## 1. Mục đích tài liệu

Tài liệu này tổng hợp lại toàn bộ các chức năng AI đã được mô hình hóa trong phase benchmark để chuẩn bị chuyển sang giai đoạn code thật của dự án.

Mục tiêu của tài liệu:
- Nhìn lại đầy đủ các chức năng AI trong hệ thống.
- Mô tả rõ mỗi chức năng dùng để làm gì, đóng vai trò gì.
- Chốt lại cách thức hoạt động của từng luồng.
- Làm tài liệu khởi động nhanh cho session chat/code mới.

---

## 2. Bức tranh tổng thể

Hệ thống là một nền tảng hỗ trợ học tập và luyện thi lập trình, tập trung vào 3 nhóm môn/chủ đề chính:
- C nhập môn
- Java OOP
- Cấu trúc dữ liệu và giải thuật

Hệ thống dùng AI ở 6 chức năng cốt lõi:
1. `AI Gatekeeper`
2. `AI Extracted Content`
3. `AI Embedding + Auto Tagging`
4. `AI Question Generation`
5. `AI Review result of AI Question Generation`
6. `AI Code Mentor`

Ngoài ra còn có 2 luồng kỹ thuật quan trọng:
- `Chunking / Ingestion Logic`
- `BYOS (Bring Your Own Syllabus)`

---

## 3. Danh sách chức năng AI

### 3.1 AI Gatekeeper

#### Vai trò
- Đọc tài liệu đầu vào của người dùng.
- Kiểm tra xem tài liệu đó có nằm trong phạm vi hệ thống hỗ trợ hay không.
- Chức năng này là lớp chặn đầu tiên trước khi ingest dữ liệu hoặc tạo câu hỏi.

#### Mục tiêu
- Loại bỏ tài liệu sai phạm vi.
- Giảm rác dữ liệu đổ vào hệ thống.
- Giảm chi phí cho các bước xử lý phía sau.

#### Đầu vào
- File tài liệu người dùng upload.
- Có thể là `pdf`, `docx`, `pptx`, `xlsx`, `txt`, `md`, ảnh và các dạng office phổ biến.

#### Đầu ra
- JSON verdict dạng:
```json
{
  "is_supported": true,
  "primary_domain": "C_Intro | Java_OOP | DSA_OOP",
  "reason": "..."
}
```

#### Cách hoạt động
1. File được parser/vision đọc ra nội dung text cơ bản.
2. Prompt Gatekeeper nhận phần nội dung đó.
3. Model trả về verdict có hỗ trợ hay không.

#### Giá trị trong hệ thống thật
- Là checkpoint trước ingest.
- Có thể dùng cho cả dữ liệu kho chung và BYOS.

#### Model benchmark đã xét
- GPT-4o
- Gemini 3.1 Flash Lite
- Gemini 3.5 Flash

---

### 3.2 AI Extracted Content

#### Vai trò
- Đọc nội dung từ ảnh hoặc tài liệu có thành phần hình ảnh.
- Trả ra text/markdown phục vụ các bước downstream.

#### Mục tiêu
- Biến tài liệu thô thành nội dung text có thể xử lý tiếp.
- Hỗ trợ các case parser thông thường không đủ tốt, nhất là tài liệu có hình, diagram, slide.

#### Đầu vào
- Ảnh đơn lẻ.
- Hoặc ảnh được trích từ `pdf/docx/pptx`.

#### Đầu ra
- Markdown hoặc text đã parse.
- Có thể kèm log token/cost/latency.

#### Cách hoạt động
1. Nếu là ảnh trực tiếp, gửi ảnh vào vision model.
2. Nếu là tài liệu có ảnh xen kẽ, vision được dùng ở bước enrich ảnh sau khi parser lấy text.
3. Nội dung parse trả về dùng cho embedding/chunking hoặc cho Gatekeeper.

#### Giá trị trong hệ thống thật
- Là lớp `text extraction` cho dữ liệu visual-heavy.
- Có thể chạy độc lập hoặc nằm trong ingestion pipeline.

#### Model benchmark đã xét
- GPT-4o
- Gemini 2.5 Flash
- Gemini 3.1 Flash Lite
- Gemini 3.5 Flash

---

### 3.3 AI Embedding + Auto Tagging

#### Vai trò
- Chuyển dữ liệu text thành vector để truy vấn ngữ nghĩa.
- Đồng thời gắn `topic_tags` cho từng chunk dữ liệu.

#### Mục tiêu
- Tạo nền tảng cho retrieval.
- Chuẩn bị dữ liệu cho tạo đề, truy vấn kiến thức, BYOS.

#### Kiến trúc đã chốt
- `Embedding`: `Cohere embed-multilingual-v3.0`
- `Auto Tagging`: `Cohere command-r7b-12-2024`

#### Đầu vào
- Text đã được chuẩn hóa sau bước parse/extract.
- Có thể là markdown, text block, hoặc text đã enrich từ ảnh.

#### Đầu ra
- Danh sách `knowledge chunks`
- Mỗi chunk có:
  - `chunk_id`
  - `content_text`
  - `vector_embedding`
  - `topic_tags`
  - metadata liên quan

#### Cách hoạt động
1. Tài liệu được parse ra text.
2. Bộ lọc rác loại:
- metadata thừa
- page/date lẻ
- note ngắn
- html/style/anchor rác
- block quá ngắn hoặc nhiều ký hiệu
3. Text được chia thành chunk.
4. Mỗi chunk:
- gọi embedding model để lấy vector
- gọi tagging model để gắn topic tags

#### Ghi chú quan trọng
- Có 2 mode chunking trong benchmark:
  - `chunking 500 words` cho benchmark embedding cơ bản
  - `logical block chunking` cho benchmark logic/ingestion thật hơn
- Với hệ thống thật, nên ưu tiên `logical chunking` thay vì cắt cứng 500 từ.

#### Giá trị trong hệ thống thật
- Đây là lõi của knowledge base.
- Là nền cho retrieval và generation.

---

### 3.4 AI Question Generation

#### Vai trò
- Tạo câu hỏi từ dữ liệu đã retrieval/embedding.
- Hỗ trợ 2 nhóm câu hỏi:
  - `FE` (multiple choice)
  - `PE` (practice/code question)

#### Mục tiêu
- Sinh đề luyện tập từ dữ liệu học tập.
- Phù hợp cho cả dữ liệu kho chung lẫn dữ liệu BYOS của người dùng.

#### Đầu vào
- `context chunks` đã được lấy từ embedding/vector retrieval
- `domain`
- `difficulty`
- `questionType`
- `count`

#### Đầu ra
- JSON array theo schema gần với bảng `QuestionBank`

#### Schema logic đã chốt
- `type`: `FE` hoặc `PE`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `skeleton_code`
- `solution_code`
- `test_cases`
- `options`
- `explanation`

#### Quy tắc
- `FE`:
  - có `options`
  - `skeleton_code = null`
  - `solution_code = null`
  - `test_cases = null`
- `PE`:
  - `options = null`
  - bắt buộc có `skeleton_code`
  - bắt buộc có `solution_code`
  - bắt buộc có `test_cases`

#### Giá trị trong hệ thống thật
- Là agent tạo nội dung học tập chính.
- Chạy sau retrieval.

#### Model benchmark đã xét
- GPT-4o
- GPT 5.4
- GPT 5.4 mini
- GPT 5.5
- Gemini 2.5 Flash
- Gemini 3.1 Flash
- Gemini 3.5 Flash

---

### 3.5 AI Review Result of Question Generation

#### Vai trò
- Đóng vai reviewer cho output của Question Generation.
- Kiểm tra tính hợp lệ, tính đúng, độ phù hợp độ khó, và schema.

#### Mục tiêu
- Tránh đưa thẳng output generation ra cho user hoặc lưu DB mà chưa qua kiểm tra.
- Tạo vòng lặp 2-agent: `Generator -> Reviewer`.

#### Đầu vào
- JSON output của Generator
- Có thể kèm ground truth/rule/schema expectation

#### Đầu ra
- JSON review dạng:
```json
{
  "review_status": "approved | needs_regenerate",
  "issues": [],
  "suggestions": []
}
```

#### Cách hoạt động
1. Generator tạo câu hỏi từ context chunks.
2. Reviewer nhận output đó.
3. Reviewer kiểm:
- schema
- difficulty alignment
- logic/factual correctness cơ bản
- chất lượng đề
4. Nếu không đạt, hệ thống có thể trigger regenerate.

#### Giá trị trong hệ thống thật
- Tăng độ ổn định cho tính năng tạo đề.
- Hữu ích đặc biệt với PE/code question.

---

### 3.6 AI Code Mentor

#### Vai trò
- Đọc đề bài + đoạn code sinh viên submit.
- Phân tích đúng/sai, bug, edge case, style, complexity.
- Trả ra góp ý để sinh viên sửa bài.

#### Mục tiêu
- Là trợ giảng AI cho phần thực hành lập trình.
- Tập trung vào phản hồi mang tính học tập, không chỉ chấm đúng/sai.

#### Đầu vào
- `problem`
- `code`
- `language` (`c` hoặc `java`)

#### Đầu ra
- Nhận xét, lỗi, gợi ý fix, có thể kèm complexity và ví dụ test fail.

#### Các nhóm case benchmark đã hướng tới
- correct code
- syntax error
- logic error
- edge case miss
- complexity issue
- runtime risk
- style/readability issue
- partial solution
- wrong algorithm
- testcase fail

#### Giá trị trong hệ thống thật
- Là một trong các tính năng giá trị nhất cho sinh viên luyện code.
- Có thể dùng chung với kho đề PE.

#### Model benchmark đã xét
- GPT-4o
- GPT 5.4
- GPT 5.4 mini
- GPT 5.5
- Gemini 2.5 Flash
- Gemini 3.1 Flash
- Gemini 3.5 Flash

---

## 4. Luồng Chunking / Ingestion Logic

Đây không phải là một “AI function” độc lập từ góc nhìn sản phẩm, nhưng là luồng kỹ thuật cực kỳ quan trọng.

### 4.1 Mode 1: Xử lý thuần text

Luồng:
1. Input file
2. Library/parser extract text
3. Làm sạch text
4. Logical chunking
5. Cohere embedding
6. Cohere command autotagging

Phù hợp khi:
- Tài liệu chủ yếu là text
- Không cần hiểu sâu phần ảnh
- Muốn chi phí thấp hơn

### 4.2 Mode 2: Full multimodal theo page/image

Luồng:
1. Input file có text + image
2. Tách page/image
3. Vision đọc từng page/image
4. Trả về text có marker `[PAGE n]`
5. Embedding
6. Auto tagging

Phù hợp khi:
- Slide nhiều hình
- Diagram/tree/array/image đóng vai trò quan trọng
- Cần độ chính xác cao hơn cho nội dung visual

### 4.3 Hướng semi-multimodal đã thảo luận

Một hướng thực tế cho dự án thật:
1. Parser lấy text từ `docx/pptx/pdf`
2. Parser hoặc thư viện để lộ placeholder ảnh như `image1.jpg`
3. Trích toàn bộ ảnh thật từ file gốc
4. Nếu trong text có placeholder ảnh nào thì gửi đúng ảnh đó sang vision
5. Thay placeholder bằng mô tả vision
6. Tiếp tục chunking -> embedding -> tagging

Hướng này có lợi vì:
- tiết kiệm hơn full-page vision
- giữ ngữ cảnh ảnh đúng vị trí
- dễ benchmark theo từng bước

---

## 5. Luồng BYOS (Bring Your Own Syllabus)

### Vai trò
- Cho phép sinh viên upload tài liệu riêng để tự tạo câu hỏi từ chính dữ liệu của họ.

### Ý nghĩa
- Đây là tính năng cá nhân hóa mạnh.
- Nhưng cũng là phần có nguy cơ chi phí cao nhất nếu không kiểm soát.

### Luồng đề xuất
1. Upload tài liệu
2. Gatekeeper
3. Extract/OCR/vision nếu cần
4. Chuẩn hóa markdown
5. Chunking
6. Embedding
7. Tagging
8. Lưu DB/vector store theo `user_id`

### Nguyên tắc quan trọng
- Dữ liệu BYOS phải tách theo `user_id`
- Không trộn với kho chung
- Nên có preflight estimate cost trước khi ingest
- Nên có quota / credit / file limit

---

## 6. Schema dữ liệu chunk đã hướng tới

Benchmark đã đi đến định hình mỗi chunk kiến thức nên có tối thiểu:
- `document_id`
- `user_id`
- `chunk_id`
- `content_text`
- `vector_embedding`
- `topic_tags`

Metadata nên có thêm:
- `embedding_model`
- `embedding_provider`
- `embedding_dim`
- `tagging_model`
- `tagging_provider`
- `tag_confidence`
- `language`
- `source_page_or_slide`
- `source_type`
- `chunk_order`

Trong benchmark, một phần metadata này được giữ ở output/log hoặc object `metadata` của chunk.

---

## 7. Full flow end-to-end

Luồng hệ thống đầy đủ đã benchmark:
1. `AI Gatekeeper`
2. `AI Extracted Content` hoặc parser text
3. `AI Embedding + Auto Tagging`
4. Retrieval context chunks
5. `AI Question Generation`
6. `AI Review`
7. `AI Code Mentor` (ở use case review lời giải code)

Không phải use case nào cũng chạy full flow.

Ví dụ:
- Ingest tài liệu: dừng ở bước 3
- Tạo đề: dùng bước 4 -> 5 -> 6
- Mentor code: có thể chạy độc lập

---

## 8. Những quyết định kỹ thuật đã chốt trong benchmark

### 8.1 Với embedding/tagging
- Chốt hướng `Cohere-only` cho benchmark ingestion:
  - Embedding: `embed-multilingual-v3.0`
  - Tagging: `command-r7b-12-2024`

Lý do:
- Dễ quản lý cost
- Dễ giải thích trong báo cáo
- Một provider cho một khối chức năng khép kín

### 8.2 Với generation/review và mentor
- Cho phép benchmark nhiều model để chọn tuyến tốt nhất sau cùng.
- UI benchmark hỗ trợ chọn model động, không hard-code.

### 8.3 Với parsing tài liệu
- Có hỗ trợ `pdf`, `docx`, `pptx`, `xlsx`, `txt`, `md`, image.
- Đã thảo luận thêm về semi-multimodal và OCR open-source cho phase sau.

### 8.4 Với noise filtering
- Đã thêm bước lọc rác trước embedding/chunking:
  - bỏ page/date rác
  - bỏ html/style/anchor
  - bỏ markdown image token
  - bỏ note prefix
  - bỏ block quá ngắn hoặc chủ yếu là ký hiệu

---

## 9. Trạng thái mô hình hóa hiện tại

Những gì đã rõ:
- Chức năng AI nào tồn tại
- Mỗi chức năng làm gì
- Các input/output chính
- Các flow ingestion / generation / mentor
- Cách benchmark token/cost/log

Những gì có thể tiếp tục làm ở dự án thật:
- Chốt kiến trúc DB/vector store thật
- Chốt API contract thật giữa BE và FE
- Tối ưu OCR/vision pipeline cho `docx/pptx/pdf`
- Triển khai retrieval thật
- Triển khai regenerate loop giữa generation và review
- Thiết kế credit/quota thật cho BYOS

---

## 10. Gợi ý dùng tài liệu này cho session mới

Khi mở session chat/code mới, có thể dùng tài liệu này như “context bootstrap”:
- Đầu tiên xác định đang code chức năng nào trong 6 chức năng AI.
- Nếu đang làm ingestion, đọc thêm phần `Chunking / Ingestion Logic` và `BYOS`.
- Nếu đang làm đề thi, đọc phần `Generation + Review`.
- Nếu đang làm hỗ trợ code, đọc phần `AI Code Mentor`.

---

## 11. Kết luận ngắn

Từ benchmark, hệ thống AI đã được mô hình hóa thành 3 lớp:

1. `Lớp kiểm tra đầu vào`
- Gatekeeper
- Extracted Content

2. `Lớp tri thức`
- Embedding
- Auto Tagging
- Chunking / Ingestion

3. `Lớp tác vụ học tập`
- Question Generation
- Review
- Code Mentor

Đây là bộ khung đủ rõ để chuyển từ benchmark sang code thật của dự án.

