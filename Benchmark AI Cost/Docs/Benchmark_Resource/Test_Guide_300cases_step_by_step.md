# Step-by-Step Guide Chạy Test 100 Cases (Cost Benchmark)

Tài liệu này bám theo `expected_result` và `rubric_key` đã điền trong file Excel test plan, nhưng ưu tiên mục tiêu **benchmark chi phí** và **so sánh model**.

## A) Kế hoạch 100 case (đã chốt)
- AI Gatekeeper: **15**
- AI Extracted Content: **15**
- AI Embedding + Auto Tagging: **10**
- AI Generation + Review: **35**
- AI Code Mentor: **25**
- **Tổng: 100 case**

## 0) Chuẩn bị trước khi chạy
1. Mở tab `Connection / Credentials`.
2. Nhập đầy đủ key/endpoint cần test.
3. Bấm `Load Models From Providers`.
4. Chọn preset môn + ngôn ngữ nếu cần (`C nhập môn / Java OOP / Cấu trúc DL&GT`, `vi/en`).
5. Đảm bảo đã lưu prompt hiện tại (nếu có chỉnh).
6. Với mỗi run, ghi thêm `case_id` vào `notes` hoặc dùng naming convention trong file input.

## 1) AI Gatekeeper (15 cases)
### 1.1 Mục tiêu pass/fail (theo expected_result)
- `supported`: trả `is_supported=true`, `primary_domain` thuộc `{C_Intro, Java_OOP, DSA_OOP}`, có `reason`.
- `unsupported`: trả `is_supported=false`, `primary_domain` null/OutsideScope, có `reason`.
- `noise_injection`: không bị prompt injection, vẫn trả JSON hợp lệ.
- `ambiguous_content`: verdict nhất quán, reason nêu rõ thiếu dữ kiện.

### 1.2 Bộ file cần chuẩn bị (đặt tên cố định để chạy nhanh)
Đặt các file trong thư mục `Docs/TestFiles/Gatekeeper/`:

- `GK_SUP_C_intro_pdf.pdf`
- `GK_SUP_Java_OOP_docx.docx`
- `GK_SUP_DSA_OOP_pptx.pptx`
- `GK_SUP_Java_array_txt.txt`
- `GK_SUP_C_pointer_xlsx.xlsx`
- `GK_SUP_DSA_graph_image.jpg`
- `GK_UNSUP_marketing_docx.docx`
- `GK_UNSUP_medical_pdf.pdf`
- `GK_UNSUP_history_txt.txt`
- `GK_UNSUP_finance_xlsx.xlsx`
- `GK_NOISE_prompt_injection_pdf.pdf`
- `GK_NOISE_hidden_instruction_txt.txt`
- `GK_AMBIG_mixed_topic_png.png`
- `GK_AMBIG_short_note_txt.txt`

### 1.3 Mapping nhanh case -> file input
- `supported`: dùng lần lượt 6 file `GK_SUP_*` (quay vòng nếu case nhiều hơn 6).
- `unsupported`: dùng lần lượt 4 file `GK_UNSUP_*` (quay vòng).
- `noise_injection`: dùng lần lượt 2 file `GK_NOISE_*` (quay vòng).
- `ambiguous_content`: dùng lần lượt 2 file `GK_AMBIG_*` (quay vòng).

Ví dụ áp dụng theo pattern:
- `GAT-001 supported` -> `GK_SUP_C_intro_pdf.pdf`
- `GAT-002 unsupported` -> `GK_UNSUP_marketing_docx.docx`
- `GAT-003 noise_injection` -> `GK_NOISE_prompt_injection_pdf.pdf`
- `GAT-004 ambiguous_content` -> `GK_AMBIG_mixed_topic_png.png`
- `GAT-005 supported` -> `GK_SUP_Java_OOP_docx.docx`
- ...

### 1.4 Step thực thi mỗi case
1. Vào tab `AI Gatekeeper`.
2. Chọn model A (và B nếu test so sánh).
3. Xác định `scenario_group` của case trong sheet.
4. Chọn file đúng theo mapping ở mục `1.3`.
4. Bấm `Run Gatekeeper` hoặc `Run A + B`.
5. Kiểm tra box `Supported` + `Primary domain` + `Reason`.
6. Đối chiếu với `expected_result`.
7. Ghi `pass/fail`, token, cost.
8. Export JSON nếu cần dẫn chứng.

## 2) AI Extracted Content (15 cases)
### 2.1 Mục tiêu pass/fail
- `has_content`: markdown trích xuất đúng nội dung chính (>90% tương đối).
- `image_only`: OCR ra đúng ý chính, chấp nhận lệch format nhẹ.
- `mixed_layout`: giữ được text + list/bảng ở mức dùng được.
- `no_content`: không bịa nội dung, output rỗng/hợp lý.

### 2.2 Bộ file cần chuẩn bị (đặt tên cố định để chạy nhanh)
Đặt các file trong thư mục `Docs/TestFiles/ExtractedContent/`:

- `EXT_CONTENT_pdf_textbook.pdf`
- `EXT_CONTENT_docx_notes.docx`
- `EXT_CONTENT_pptx_lecture.pptx`
- `EXT_IMG_ONLY_scan_page_1.jpg`
- `EXT_IMG_ONLY_scan_page_2.png`
- `EXT_MIXED_pdf_table_image.pdf`
- `EXT_MIXED_pptx_code_chart.pptx`
- `EXT_NOCONTENT_blank_pdf.pdf`
- `EXT_NOCONTENT_low_quality_image.jpg`

### 2.3 Mapping nhanh case -> file input
- `has_content`: dùng lần lượt 3 file `EXT_CONTENT_*` (quay vòng).
- `image_only`: dùng lần lượt 2 file `EXT_IMG_ONLY_*` (quay vòng).
- `mixed_layout`: dùng lần lượt 2 file `EXT_MIXED_*` (quay vòng).
- `no_content`: dùng lần lượt 2 file `EXT_NOCONTENT_*` (quay vòng).

Ví dụ:
- `EXT-001 has_content` -> `EXT_CONTENT_pdf_textbook.pdf`
- `EXT-002 image_only` -> `EXT_IMG_ONLY_scan_page_1.jpg`
- `EXT-003 mixed_layout` -> `EXT_MIXED_pdf_table_image.pdf`
- `EXT-004 no_content` -> `EXT_NOCONTENT_blank_pdf.pdf`
- ...

### 2.4 Step thực thi mỗi case
1. Vào tab `AI Extracted Content`.
2. Chọn model.
3. Xác định `scenario_group` của case trong sheet.
4. Chọn file đúng theo mapping ở mục `2.3`.
4. Bấm `Run Extracted Content`.
5. Kiểm tra `Extracted Markdown Preview` + `Chunk Count`.
6. Đối chiếu expected, chấm pass/fail.
7. Ghi token/cost/latency.
8. Export `.md`/`.json` khi cần.

## 3) AI Embedding + Auto Tagging (10 cases)
### 3.1 Mục tiêu pass/fail
- `cost_baseline`: log token/cost đầy đủ cho embedding + tagging.
- `basic_tagging`: có tag hợp lệ theo taxonomy.
- `cross_topic`: tag đa chủ đề đúng ngữ cảnh.
- `noisy_text`: tagging vẫn ổn định, không over-tag.
- `long_chunk`: chạy thành công, vector dimension hợp lệ.

### 3.2 Step thực thi mỗi case
1. Vào tab `AI Embedding`.
2. Chọn embedding model A/B và tagging model.
3. Điền `subject`, `language`, `allowedTags`.
4. Upload file theo `case_id`.
5. Bấm `Run Embedding` hoặc `Run A + B`.
6. Kiểm tra `KnowledgeChunks preview`:
- `topic_tags`
- `content_text`
- `vector_dim`
7. Đối chiếu expected_result.
8. Export `Chunks JSON` để dùng cho Gen/Review.

## 4) AI Generation + Review (35 cases)
### 4.1 Mục tiêu pass/fail
- FE: output đúng schema QuestionsBank (FE-nullability đúng).
- PE: output đúng schema QuestionsBank (PE-nullability đúng).
- `ambiguous_prompt`: review nêu đúng điểm mơ hồ.
- `insufficient_data`: review phải flag thiếu dữ kiện/needs_regeneration.

### 4.2 Step thực thi mỗi case
1. Vào tab `Generation + Review Flow`.
2. Import chunk JSON master hoặc dán `contextChunks`.
3. Chọn `questionType` (FE/PE), `domain`, `count`.
4. Chọn model generator/reviewer (A/B nếu cần).
5. Bấm `Run Generation + Review` hoặc `Run A + B`.
6. Kiểm tra:
- `Generation Output`
- `Review Output`
- schema JSON của câu hỏi theo QuestionsBank.
7. Đối chiếu expected_result + rubric.
8. Ghi token/cost stage generation + review.

## 5) AI Code Mentor (25 cases)
### 5.1 Mục tiêu pass/fail
- Phát hiện đúng lỗi/case tương ứng.
- Đưa hướng sửa cụ thể, actionable.
- Không bịa lỗi với code đúng.

### 5.2 Step thực thi mỗi case
1. Vào tab `AI Mentor Code`.
2. Chọn model A/B.
3. Nhập `problem` + dán `code` theo case.
4. Bấm `Run Code Mentor` hoặc `Run A + B`.
5. Kiểm tra `Mentor Output`.
6. Đối chiếu expected_result của case.
7. Chấm pass/fail + ghi token/cost.

## 6) Quy trình ghi nhận chuẩn sau mỗi run (phục vụ report chi phí)
1. Lưu `run_id`.
2. Tải JSON run (`/api/reports/runs/:runId.json`).
3. Điền vào sheet tổng hợp cost:
- `cases_run`
- `pass_rate`
- `avg_input_tokens`
- `avg_output_tokens`
- `avg_total_cost_usd`
- `p95_latency_ms`
4. Ghi thêm:
- `avg_input_cost_usd`
- `avg_output_cost_usd`
- `error_rate`
5. Với case fail, ghi ngắn `root cause` (không cần phân tích sâu chất lượng nếu ngoài scope).

## 7) Quy tắc chốt model cho từng chức năng (theo mục tiêu cost)
1. Ưu tiên `cost/request` thấp.
2. Không vi phạm schema/logic trọng yếu.
3. Error rate thấp, latency ổn định.
4. Chỉ dùng quality check ở mức sanity để tránh chọn model quá kém.

## 8) Mẫu cấu trúc báo cáo chi phí (gợi ý)
1. Tổng quan bộ test 100 case.
2. Cost theo từng chức năng (avg/min/max).
3. So sánh model theo từng chức năng.
4. Kết luận model đề xuất cho đồ án:
- Gatekeeper model
- Extracted model
- Embedding + Tagging model
- Gen + Review model pair
- Mentor model
