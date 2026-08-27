# Guide Scoring Dictionary Manual

## Mục tiêu

Tài liệu này hướng dẫn cách chấm điểm thủ công cho các sheet benchmark trong workbook:

- `Input Quality Benchmark`
- `Question Generation Benchmark`
- `Code Mentor Benchmark`

Mục tiêu là giúp nhiều người test chấm theo cùng một chuẩn, giảm lệch cảm tính khi so sánh output giữa các AI model.

## Nguyên tắc chung

- Mỗi metric được chấm theo thang `0 -> 1`.
- Khuyến nghị dùng 3 mức nhanh:
  - `0.0` = fail rõ ràng
  - `0.5` = tạm chấp nhận, còn lỗi đáng kể
  - `1.0` = tốt, đạt kỳ vọng
- Nếu cần chi tiết hơn, có thể dùng các mức `0.25`, `0.75`.
- Không chấm theo cảm giác chung chung. Mỗi điểm phải bám vào output thực tế, context đầu vào, và rubric của sheet tương ứng.
- Khi phân vân giữa 2 mức điểm, ưu tiên mức thấp hơn nếu lỗi có ảnh hưởng tới khả năng dùng output downstream.

## 1. Input Quality Benchmark

Nhóm này dùng để chấm chất lượng đầu vào sau các bước:

`Extracted Content -> Human Review -> Embedding -> Chunking -> Auto Tagging`

### 1.1 `ocr_text_accuracy_score`

Ý nghĩa:
- đo độ chính xác của text sau OCR hoặc parsing

Cách chấm:
- `0.0`: sai nhiều, mất dòng, sai ký tự, thiếu nội dung quan trọng
- `0.5`: đọc được ý chính nhưng còn lỗi OCR/parsing đáng kể
- `1.0`: text đúng gần như hoàn toàn, ít hoặc không có lỗi ảnh hưởng nghĩa

Gợi ý nhìn nhanh:
- tên mục, heading, code block, bảng, công thức, ký tự đặc biệt

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 1.2 `content_cleanliness_score`

Ý nghĩa:
- đo mức độ làm sạch noise, ký tự rác, khoảng trắng thừa, markdown bẩn

Cách chấm:
- `0.0`: output rất bẩn, khó đọc, nhiều đoạn rác
- `0.5`: có làm sạch nhưng còn nhiều chỗ lộn xộn
- `1.0`: sạch, dễ đọc, định dạng nhất quán

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 1.3 `chunk_boundary_score`

Ý nghĩa:
- đo việc chia chunk có đúng cụm nội dung và không cắt vỡ nghĩa

Cách chấm:
- `0.0`: chunk cắt sai nặng, mất ngữ cảnh, cắt giữa câu/ý quan trọng
- `0.5`: phần lớn tạm ổn nhưng vẫn có nhiều chunk cắt chưa hợp lý
- `1.0`: chunk chia hợp logic, giữ được ngữ nghĩa và khả năng retrieval

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 1.4 `tag_relevance_score`

Ý nghĩa:
- đo tag có bám đúng nội dung chunk hay không

Cách chấm:
- `0.0`: tag sai topic hoặc quá chung chung
- `0.5`: có tag đúng nhưng thiếu chính xác hoặc còn tag nhiễu
- `1.0`: tag đúng chủ đề, có ích cho retrieval và downstream

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 1.5 `tag_coverage_score`

Ý nghĩa:
- đo mức độ bao phủ topic quan trọng của tài liệu

Cách chấm:
- `0.0`: bỏ sót nhiều topic chính
- `0.5`: bao phủ được phần chính nhưng còn thiếu topic quan trọng
- `1.0`: bao phủ tốt các topic quan trọng, ít bỏ sót

Ngưỡng pass khuyến nghị:
- `>= 0.70`

### 1.6 `image_description_quality_score`

Ý nghĩa:
- đo chất lượng mô tả ảnh nếu pipeline có vision

Cách chấm:
- `0.0`: mô tả sai hoặc vô dụng
- `0.5`: mô tả đúng một phần nhưng thiếu chi tiết quan trọng
- `1.0`: mô tả đúng, rõ, hỗ trợ hiểu nội dung tài liệu

Ngưỡng pass khuyến nghị:
- `>= 0.60`

### 1.7 `grounding_traceability_score`

Ý nghĩa:
- đo khả năng truy vết từ output về chunk, page, segment, hoặc nguồn gốc nội dung

Cách chấm:
- `0.0`: khó hoặc không truy được nguồn
- `0.5`: truy được một phần nhưng chưa ổn định
- `1.0`: truy vết rõ ràng, đủ để audit và review tay

Ngưỡng pass khuyến nghị:
- `>= 0.80`

### 1.8 Công thức tổng điểm

```text
overall_quality_score =
ocr_text_accuracy_score * 0.20 +
content_cleanliness_score * 0.20 +
chunk_boundary_score * 0.20 +
tag_relevance_score * 0.15 +
tag_coverage_score * 0.10 +
image_description_quality_score * 0.05 +
grounding_traceability_score * 0.10
```

### 1.9 Kết luận pass/fail

Khuyến nghị `Pass` khi:

- `overall_quality_score >= 0.80`
- `ocr_text_accuracy_score >= 0.75`
- `chunk_boundary_score >= 0.75`
- `tag_relevance_score >= 0.75`

## 2. Question Generation Benchmark

Nhóm này dùng để chấm chất lượng:

`Retrieval / Context Packing -> Generation -> Review`

Lưu ý:

- `distractor_quality_score` chỉ áp dụng cho `FE`
- `testcase_quality_score` chỉ áp dụng cho `PE`

### 2.1 `schema_valid_rate`

Ý nghĩa:
- tỷ lệ câu hỏi đúng schema đích

Cách chấm:
- `0.0`: sai schema nhiều, thiếu field bắt buộc, field sai loại
- `0.5`: có câu đúng schema, có câu lỗi
- `1.0`: toàn bộ output đúng schema

Ngưỡng pass khuyến nghị:
- `= 1.00`

### 2.2 `grounded_rate`

Ý nghĩa:
- tỷ lệ câu hỏi bám sát context nguồn

Cách chấm:
- `0.0`: nhiều chi tiết bịa, không truy được về context
- `0.5`: phần lớn có liên quan nhưng còn vài chỗ suy diễn quá mức
- `1.0`: bám sát context, không thấy hallucination đáng kể

Ngưỡng pass khuyến nghị:
- `>= 0.80`

### 2.3 `difficulty_alignment_score`

Ý nghĩa:
- độ khớp giữa độ khó output và target yêu cầu

Cách chấm:
- `0.0`: lệch rõ ràng
- `0.5`: có xu hướng đúng nhưng chưa ổn định
- `1.0`: khớp tốt với target easy/medium/hard

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 2.4 `topic_relevance_score`

Ý nghĩa:
- mức độ bám sát topic được yêu cầu

Cách chấm:
- `0.0`: lệch topic
- `0.5`: đúng topic chính nhưng còn loãng hoặc drift
- `1.0`: bám sát topic, ít nội dung thừa

Ngưỡng pass khuyến nghị:
- `>= 0.80`

### 2.5 `question_clarity_score`

Ý nghĩa:
- câu hỏi có rõ ràng, tự đủ nghĩa, ít mơ hồ không

Cách chấm:
- `0.0`: câu hỏi mơ hồ, khó hiểu
- `0.5`: hiểu được nhưng còn diễn đạt lủng củng
- `1.0`: rõ ràng, tự nhiên, dễ hiểu

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 2.6 `question_usefulness_score`

Ý nghĩa:
- giá trị học thuật và sư phạm của câu hỏi

Cách chấm:
- `0.0`: ít giá trị học tập, quá dễ hoặc quá vô nghĩa
- `0.5`: dùng được nhưng chưa thật sự tốt
- `1.0`: có giá trị kiểm tra kiến thức rõ ràng

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 2.7 `distractor_quality_score` (`FE only`)

Ý nghĩa:
- chất lượng các phương án nhiễu trong câu hỏi FE

Cách chấm:
- `0.0`: phương án nhiễu vô lý, lộ đáp án
- `0.5`: có vài phương án dùng được nhưng chưa đều
- `1.0`: distractor hợp lý, đủ phân hóa

Ngưỡng pass khuyến nghị:
- `>= 0.70`

### 2.8 `testcase_quality_score` (`PE only`)

Ý nghĩa:
- chất lượng bộ test case dùng để đánh giá câu PE

Cách chấm:
- `0.0`: test case nghèo nàn, không bắt được lỗi
- `0.5`: có test cơ bản nhưng chưa phủ edge case
- `1.0`: test case tốt, đủ để phân biệt lời giải đúng/sai

Ngưỡng pass khuyến nghị:
- `>= 0.70`

### 2.9 `rule_based_pass_rate`

Ý nghĩa:
- tỷ lệ vượt qua các kiểm tra rule-based

Cách chấm:
- `0.0`: fail nhiều rule quan trọng
- `0.5`: pass một phần
- `1.0`: pass đầy đủ các rule cần thiết

Ngưỡng pass khuyến nghị:
- `>= 0.80`

### 2.10 Công thức tổng điểm

Công thức lõi:

```text
final_quality_score =
schema_valid_rate * 0.15 +
grounded_rate * 0.25 +
difficulty_alignment_score * 0.15 +
topic_relevance_score * 0.15 +
question_clarity_score * 0.10 +
question_usefulness_score * 0.10 +
rule_based_pass_rate * 0.10
```

Biến thể chi tiết hơn cho `FE`:

```text
final_quality_score_fe =
schema_valid_rate * 0.15 +
grounded_rate * 0.23 +
difficulty_alignment_score * 0.15 +
topic_relevance_score * 0.15 +
question_clarity_score * 0.10 +
question_usefulness_score * 0.10 +
distractor_quality_score * 0.07 +
rule_based_pass_rate * 0.05
```

Biến thể chi tiết hơn cho `PE`:

```text
final_quality_score_pe =
schema_valid_rate * 0.15 +
grounded_rate * 0.23 +
difficulty_alignment_score * 0.15 +
topic_relevance_score * 0.15 +
question_clarity_score * 0.08 +
question_usefulness_score * 0.10 +
testcase_quality_score * 0.09 +
rule_based_pass_rate * 0.05
```

### 2.11 Kết luận pass/fail

Khuyến nghị `Pass` khi:

- `final_quality_score >= 0.80`
- `grounded_rate >= 0.80`
- `schema_valid_rate = 1.00`
- `rule_based_pass_rate >= 0.80`

## 3. Code Mentor Benchmark

Nhóm này dùng để chấm chất lượng feedback của AI mentor trên bài nộp code.

### 3.1 `bug_detection_score`

Ý nghĩa:
- độ đúng khi phát hiện bug hoặc lỗi sai

Cách chấm:
- `0.0`: bỏ sót lỗi chính hoặc kết luận sai nặng
- `0.5`: phát hiện được một phần lỗi
- `1.0`: phát hiện đúng phần lớn lỗi quan trọng

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 3.2 `correctness_feedback_score`

Ý nghĩa:
- độ đúng của nhận xét về tính đúng/sai của code

Cách chấm:
- `0.0`: nhận xét sai bản chất
- `0.5`: đúng một phần
- `1.0`: nhận xét đúng, bám bài và bám code

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 3.3 `code_quality_feedback_score`

Ý nghĩa:
- chất lượng nhận xét về style, cấu trúc, maintainability, code smell

Cách chấm:
- `0.0`: nhận xét nghèo nàn hoặc vô ích
- `0.5`: có nhận xét đúng nhưng chưa sâu
- `1.0`: nhận xét rõ, đúng và hữu ích

Ngưỡng pass khuyến nghị:
- `>= 0.70`

### 3.4 `actionability_score`

Ý nghĩa:
- mức độ đề xuất sửa lỗi có thể làm theo ngay

Cách chấm:
- `0.0`: feedback chung chung, khó hành động
- `0.5`: có hướng sửa nhưng còn mơ hồ
- `1.0`: hướng sửa rõ, làm theo được

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 3.5 `specificity_score`

Ý nghĩa:
- mức độ cụ thể thay vì nhận xét mơ hồ

Cách chấm:
- `0.0`: vague, chung chung
- `0.5`: có chỉ ra điểm cụ thể nhưng chưa đều
- `1.0`: chỉ rõ vấn đề, vị trí, lý do

Ngưỡng pass khuyến nghị:
- `>= 0.70`

### 3.6 `hallucination_penalty`

Ý nghĩa:
- điểm phạt khi AI nhận xét sai, bịa lỗi, hoặc không bám vào code thực

Cách chấm:
- `0.0`: không thấy hallucination đáng kể
- `0.5`: có một số nhận xét suy diễn
- `1.0`: bịa nhiều, gây hại cho người học

Lưu ý:
- metric này là **điểm trừ**
- không cộng vào tổng như các metric khác

### 3.7 `grounded_to_code_score`

Ý nghĩa:
- mức độ nhận xét bám vào source code và context đề bài

Cách chấm:
- `0.0`: nhận xét rời khỏi code
- `0.5`: có bám code nhưng chưa chắc
- `1.0`: bám sát code, ví dụ, flow và yêu cầu đề bài

Ngưỡng pass khuyến nghị:
- `>= 0.80`

### 3.8 `pedagogical_value_score`

Ý nghĩa:
- giá trị hướng dẫn học tập cho sinh viên

Cách chấm:
- `0.0`: feedback không giúp người học tiến bộ
- `0.5`: có ích ở mức cơ bản
- `1.0`: giúp người học hiểu lỗi và học cách sửa

Ngưỡng pass khuyến nghị:
- `>= 0.75`

### 3.9 Công thức tổng điểm

Điểm nền:

```text
overall_quality_score =
bug_detection_score * 0.20 +
correctness_feedback_score * 0.20 +
code_quality_feedback_score * 0.15 +
actionability_score * 0.15 +
specificity_score * 0.10 +
grounded_to_code_score * 0.10 +
pedagogical_value_score * 0.10
```

Nếu dùng điểm phạt hallucination:

```text
overall_quality_score_adjusted =
MAX(0, overall_quality_score - hallucination_penalty)
```

### 3.10 Kết luận pass/fail

Khuyến nghị `Pass` khi:

- `overall_quality_score_adjusted >= 0.80`
- `grounded_to_code_score >= 0.80`
- `actionability_score >= 0.75`

## 4. Cách chấm nhanh trong thực tế

Nếu thời gian gấp, mỗi run có thể chấm theo thứ tự:

1. đọc input và output
2. chấm các metric cứng trước:
   - Input Quality: OCR, chunking, tag relevance
   - Generation: schema, grounded, rule-based
   - Code Mentor: bug detection, grounded to code, actionability
3. nếu fail các metric cứng thì đánh dấu fail ngay
4. sau đó mới chấm các metric mềm hơn để tính tổng điểm

## 5. Quy ước để nhiều người chấm giống nhau

- Luôn dùng cùng một bộ case test khi so model.
- Luôn giữ cùng input/context pack khi so model generation-review.
- Không nhìn tên model khi chấm nếu có thể, để giảm bias.
- Nếu 2 người chấm lệch quá `0.25` ở cùng một metric, cần review lại output cùng nhau.
- Với metric có tính cảm nhận như `question_clarity_score` hay `pedagogical_value_score`, nên đọc ít nhất 2 output mẫu chuẩn trước khi bắt đầu batch chấm.

## 6. Kết luận

`Scoring Dictionary` không chỉ là bảng ghi chú metric, mà là chuẩn chấm điểm chung để:

- so sánh model
- đánh giá chất lượng output
- kết luận pass/fail
- theo dõi chất lượng qua các version prompt, rubric, policy, ground truth

Khi cần ra quyết định nhanh, nên ưu tiên nhìn:

1. các metric cứng có đạt ngưỡng hay không
2. tổng điểm weighted score
3. sau đó mới so `quality_cost_index` và `quality_latency_index`
