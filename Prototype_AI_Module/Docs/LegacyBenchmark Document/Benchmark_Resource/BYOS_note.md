# Ghi chú BYOS (Bring Your Own Syllabus)

## 1) Mục tiêu
- Cho phép sinh viên tự upload tài liệu cá nhân để hệ thống tạo câu hỏi dựa trên **đúng dữ liệu của riêng họ**.
- Dữ liệu BYOS phải tách biệt theo `user_id`, không lẫn với kho dữ liệu chung của hệ thống.

## 2) Luồng xử lý đề xuất
1. Người dùng upload tài liệu.
2. `AI Gatekeeper` kiểm tra phạm vi hỗ trợ.
3. Trích xuất nội dung:
- Nếu là ảnh/tài liệu chứa ảnh: OCR/vision.
- Nếu là file văn bản (pdf/docx/xlsx/pptx/txt...): parser/library.
4. Chuẩn hóa ra Markdown.
5. Chunking theo kích thước chuẩn (ví dụ 500 từ/chunk).
6. Embedding cho từng chunk.
7. Sub-agent tagging (nhanh/rẻ) gắn `topic tags` theo taxonomy môn học.
8. Lưu DB/vector store:
- `user_id`
- `chunk_id`
- `content`
- `vector`
- `metadata` (môn học, tags, nguồn tài liệu, thời gian...)

## 3) Kiểm soát chi phí BYOS
- BYOS có thể tốn chi phí cao nếu file lớn (ví dụ giáo trình vài trăm trang).
- Nên áp dụng các lớp kiểm soát:

### 3.1 Giới hạn theo user
- `max_files_per_user`
- `max_total_storage_mb`
- `max_ingest_jobs_per_day`
- `max_tokens_per_month`

### 3.2 Giới hạn theo file
- `max_file_size_mb`
- `max_pages`
- `max_words`
- `max_chunks`

### 3.3 Tối ưu kỹ thuật
- Dùng `file_hash/chunk_hash` để tránh xử lý lại dữ liệu trùng.
- Re-ingest incremental (chỉ xử lý phần thay đổi).
- Chỉ bật vision cho phần thực sự cần OCR.
- Dùng model rẻ cho bước tagging/gatekeeper.

## 4) Cơ chế credit/kiếm tiền
Hệ thống có 2 loại credit:
- `free_credit`: 50 credit/ngày (reset mỗi ngày).
- `paid_credit`: người dùng mua thêm (tạm quy đổi: `1.000 VND = 10 credit`).

### 4.1 Nguyên tắc trừ credit
- Ưu tiên trừ `free_credit` trước.
- Nếu thiếu thì trừ sang `paid_credit`.

### 4.2 Thu phí ingest BYOS
Nên tính phí theo từng bước pipeline:
- Gatekeeper
- Extract/OCR
- Embedding
- Tagging
- (Tùy chọn) phí lưu trữ/vector

## 5) Preflight cost (khuyến nghị bắt buộc)
Trước khi chạy ingestion:
1. Ước tính chi phí/token/credit dự kiến.
2. Hiển thị cho user: "Lần xử lý này dự kiến tốn X credit".
3. User xác nhận mới chạy.
4. Nếu không đủ credit thì từ chối hoặc yêu cầu nạp thêm.

## 6) Truy vấn dữ liệu BYOS
Khi sinh câu hỏi từ dữ liệu người dùng:
- Luôn filter theo `user_id`.
- Có thể kết hợp filter metadata (môn học/tag) trước khi similarity search.
- Khuyến nghị dùng hybrid retrieval (metadata filter + vector search).

## 7) Nhật ký phục vụ báo cáo đồ án
Mỗi lần chạy BYOS nên lưu:
- `credits_estimated`
- `credits_actual`
- `credits_deducted_from_free`
- `credits_deducted_from_paid`
- `token/cost breakdown theo từng step`
- `run_id`, `user_id`, `file_id`, thời gian xử lý

---

Ghi chú này dùng làm nền cho:
- Thiết kế API ingest BYOS
- Thiết kế quota/credit policy
- Viết phần phân tích chi phí trong báo cáo đồ án
