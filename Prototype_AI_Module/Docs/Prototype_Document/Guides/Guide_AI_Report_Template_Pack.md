# Guide AI Report Template Pack

## Mục tiêu

Bộ template này dùng để dựng báo cáo AI theo 4 phần:

1. chất lượng khối đầu vào
2. chất lượng khối tạo câu hỏi
3. chất lượng khối code mentor
4. theo dõi version của prompt, rubric, policy, ground truth

Các file CSV đi kèm được thiết kế để:

- import thẳng vào Excel
- dùng một header chuẩn giữa nhiều người test
- tách rõ `raw runs`, `summary`, `version tracking`

## Danh sách file

- `Templates/AI_Report/input_quality_benchmark_template.csv`
- `Templates/AI_Report/question_generation_benchmark_template.csv`
- `Templates/AI_Report/code_mentor_benchmark_template.csv`
- `Templates/AI_Report/ai_config_version_tracking_template.csv`
- `Templates/AI_Report/scoring_dictionary_template.csv`

## Cách dùng đề xuất

### 1. Sheet Input Quality Benchmark

Phạm vi:

- Extracted Content
- Human Review
- Embedding
- Chunking
- Auto Tagging

Mỗi dòng nên là `1 run hoàn chỉnh` trên `1 tài liệu` hoặc `1 case`.

### 2. Sheet Question Generation Benchmark

Phạm vi:

- retrieval
- context packing
- generator
- reviewer

Mỗi dòng nên là `1 run generation-review` hoàn chỉnh.

### 3. Sheet Code Mentor Benchmark

Phạm vi:

- input problem
- source files
- mentor output
- đối chiếu ground truth/rubric

Mỗi dòng nên là `1 submission test`.

### 4. Sheet AI Config Version Tracking

Phạm vi:

- prompt
- rubric
- policy
- ground truth

Mỗi dòng nên là `1 thay đổi version`.

### 5. Sheet Scoring Dictionary

Phạm vi:

- định nghĩa các cột score
- định nghĩa trọng số
- định nghĩa pass/fail

Sheet này không phải dữ liệu test, mà là sheet chuẩn hoá cách chấm.

## Công thức scoring đề xuất

### 1. Input Quality Benchmark

Mục tiêu:

- đo chất lượng làm sạch tài liệu
- đo chất lượng chunking/tagging
- đo khả năng dùng làm input downstream

Các score đầu vào nên chuẩn hoá về thang `0 -> 1`.

Trọng số:

- `ocr_text_accuracy_score`: `0.20`
- `content_cleanliness_score`: `0.20`
- `chunk_boundary_score`: `0.20`
- `tag_relevance_score`: `0.15`
- `tag_coverage_score`: `0.10`
- `image_description_quality_score`: `0.05`
- `grounding_traceability_score`: `0.10`

Công thức:

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

Khuyến nghị pass/fail:

- `Pass` nếu:
  - `overall_quality_score >= 0.80`
  - `ocr_text_accuracy_score >= 0.75`
  - `chunk_boundary_score >= 0.75`
  - `tag_relevance_score >= 0.75`

### 2. Question Generation Benchmark

Tách FE và PE nhưng vẫn dùng chung một trục tổng.

Trọng số lõi:

- `schema_valid_rate`: `0.15`
- `grounded_rate`: `0.25`
- `difficulty_alignment_score`: `0.15`
- `topic_relevance_score`: `0.15`
- `question_clarity_score`: `0.10`
- `question_usefulness_score`: `0.10`
- `rule_based_pass_rate`: `0.10`

Công thức:

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

Điểm bổ sung theo loại:

- FE dùng thêm `distractor_quality_score`
- PE dùng thêm `testcase_quality_score`

Nếu muốn chi tiết hơn:

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

Khuyến nghị pass/fail:

- `Pass` nếu:
  - `final_quality_score >= 0.80`
  - `grounded_rate >= 0.80`
  - `schema_valid_rate = 1.00`
  - `rule_based_pass_rate >= 0.80`

### 3. Code Mentor Benchmark

Trọng số:

- `bug_detection_score`: `0.20`
- `correctness_feedback_score`: `0.20`
- `code_quality_feedback_score`: `0.15`
- `actionability_score`: `0.15`
- `specificity_score`: `0.10`
- `grounded_to_code_score`: `0.10`
- `pedagogical_value_score`: `0.10`

Công thức:

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

Nếu dùng `hallucination_penalty`, nên trừ sau:

```text
overall_quality_score_adjusted =
MAX(0, overall_quality_score - hallucination_penalty)
```

Khuyến nghị pass/fail:

- `Pass` nếu:
  - `overall_quality_score_adjusted >= 0.80`
  - `grounded_to_code_score >= 0.80`
  - `actionability_score >= 0.75`

## Công thức đánh giá quality/cost balance

Nên có thêm một cột phụ để so sánh model:

```text
quality_cost_index = overall_quality_score / total_cost_usd
```

Nếu cần giảm bias khi cost rất nhỏ:

```text
quality_cost_index = overall_quality_score / (total_cost_usd + 0.0001)
```

## Công thức đánh giá quality/latency balance

```text
quality_latency_index = overall_quality_score / latency_ms
```

Hoặc đổi sang giây để dễ nhìn:

```text
quality_latency_index = overall_quality_score / (latency_ms / 1000)
```

## Công thức Excel mẫu

Giả sử sheet `Input Quality Benchmark` dùng cột:

- `AA`: `tag_coverage_score`
- `AB`: `tag_relevance_score`
- `AC`: `chunk_boundary_score`
- `AD`: `content_cleanliness_score`
- `AE`: `ocr_text_accuracy_score`
- `AF`: `image_description_quality_score`
- `AG`: `grounding_traceability_score`

thì cột `AH` có thể là:

```excel
=ROUND(AE2*0.2+AD2*0.2+AC2*0.2+AB2*0.15+AA2*0.1+AF2*0.05+AG2*0.1,3)
```

Tương tự cho generation:

```excel
=ROUND(AE2*0.15+AF2*0.25+AG2*0.15+AH2*0.15+AI2*0.1+AJ2*0.1+AK2*0.1,3)
```

Tương tự cho mentor:

```excel
=ROUND(P2*0.2+Q2*0.2+R2*0.15+S2*0.15+T2*0.1+V2*0.1+W2*0.1-U2,3)
```

## Cấu trúc workbook đề xuất

1. `Input Quality Benchmark`
2. `Question Generation Benchmark`
3. `Code Mentor Benchmark`
4. `AI Config Version Tracking`
5. `Scoring Dictionary`

## Cách trình bày kết luận trong báo cáo

Mỗi sheet nên trả lời được:

1. model nào có chất lượng tốt nhất
2. model nào có chi phí tốt nhất
3. model nào cân bằng nhất giữa quality và cost
4. version prompt/rubric/policy nào giúp cải thiện rõ ràng

## Ghi chú thực dụng

- giữ `run_id` là khoá chính để đối chiếu với run history JSON
- luôn lưu `prompt_version`, `rubric_version`, `policy_version`, `ground_truth_set_id`
- không dùng note tự do để thay thế cột có cấu trúc
- nếu có nhiều người test, phải dùng chung scoring dictionary
