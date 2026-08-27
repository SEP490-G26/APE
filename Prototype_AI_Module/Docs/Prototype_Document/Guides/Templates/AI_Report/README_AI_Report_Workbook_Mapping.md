# README AI Report Workbook Mapping

## Mục tiêu

File này giúp map nhanh giữa các file CSV template và các sheet trong workbook Excel.

## Vị trí sử dụng thực tế

- `Docs/Prototype_Document/Guides/Templates/AI_Report/`
  - giữ template, guide, và seed file phục vụ tài liệu
- `Test/`
  - là nơi script runtime ghi workbook test thực tế, CSV export, và evidence `.md`
  - đây là folder chính để nhóm dùng khi chạy test production prototype

## Mapping đề xuất

| Workbook Sheet | CSV Template | Mục đích |
|---|---|---|
| `Input Quality Benchmark` | `input_quality_benchmark_template.csv` | Benchmark khối đầu vào: extracted, embedding, chunking, autotagging; hiện đã được script runtime auto-fill các cột metadata/runtime/config |
| `Question Generation Benchmark` | `question_generation_benchmark_template.csv` | Benchmark khối core tạo câu hỏi và review; hiện đã được script runtime auto-fill các cột metadata/runtime/config |
| `Code Mentor Benchmark` | `code_mentor_benchmark_template.csv` | Benchmark chất lượng AI mentor; hiện đã được script runtime auto-fill các cột metadata/runtime/config |
| `AI Config Version Tracking` | `ai_config_version_tracking_template.csv` | Theo dõi prompt, rubric, policy, ground truth version |
| `AI Config Catalog` | dữ liệu tự tổng hợp từ App_Data current files | Danh mục config hiện đang active/current của prototype |
| `AI Config Snapshots` | dữ liệu tự tổng hợp từ `ai-config-history/` | Snapshot history đầy đủ để audit và compare |
| `Prompt Version Comparison` | dữ liệu tự tổng hợp từ `ai-config-history/prompts/` | So sánh prompt version hiện tại với version liền trước |
| `Scoring Dictionary` | `scoring_dictionary_template.csv` | Chuẩn hóa định nghĩa score, trọng số, pass threshold |
| `Run History Index` | `run_history_index_export.csv` | Chỉ mục toàn bộ run đã ghi nhận để lọc nhanh theo function/model/status |
| `Run Config References` | `run_config_references.csv` | Flatten `run -> config refs` để nối benchmark run với prompt/rubric/policy/ground truth |
| `Input Quality Evidence` | `input_quality_evidence.csv` | Bản parsed dễ đọc cho khối extracted + embedding + chunking + autotagging |
| `Question Generation Evidence` | `question_generation_evidence.csv` | Bản parsed dễ đọc cho output FE/PE generation + review |
| `Code Mentor Evidence` | `code_mentor_evidence.csv` | Bản parsed dễ đọc cho input/output AI mentor |

## Thứ tự import khuyến nghị

1. import `scoring_dictionary_template.csv`
2. import `ai_config_version_tracking_template.csv`
3. import `input_quality_benchmark_template.csv`
4. import `question_generation_benchmark_template.csv`
5. import `code_mentor_benchmark_template.csv`
6. refresh `ai_config_version_changelog.csv`, `run_history_index_export.csv`, `run_config_references.csv` bằng script workbook updater

## Ghi chú khi dùng

- mỗi sheet benchmark nên giữ `run_id` là khóa tra cứu chính
- luôn điền `prompt_version`, `rubric_version`, `policy_version`, `ground_truth_set_id`
- nếu 1 run không dùng rubric hoặc ground truth thì ghi `N/A`, không để trống
- nên khóa hàng header và bật filter cho toàn bộ sheet
- `Run Config References` sẽ chỉ có dữ liệu khi run history JSON đã chứa `Summary.ConfigReferences`
- với các run cũ chưa có `ConfigReferences`, sheet `Run History Index` vẫn dùng được nhưng chưa join sâu tới config version
- mỗi run evidence còn được xuất thêm ra file `.md` trong `Templates/AI_Report/report_evidence/` để làm bằng chứng đọc tay
- benchmark sheet dùng để score/filter; evidence sheet dùng để nhìn input/output parsed; evidence `.md` dùng để trích dẫn đầy đủ hơn khi viết báo cáo

## Script cập nhật workbook

Chạy:

```powershell
py -3 scripts/update_ai_report_workbook.py
```

Script này sẽ:

- tạo hoặc refresh workbook `Test/AI_Benchmark_Report.xlsx`
- cập nhật cả 3 sheet benchmark:
  - `Input Quality Benchmark`
  - `Question Generation Benchmark`
  - `Code Mentor Benchmark`
- xuất `Test/run_history_index_export.csv`
- xuất `Test/run_config_references.csv`
- xuất `Test/input_quality_benchmark.csv`
- xuất `Test/question_generation_benchmark.csv`
- xuất `Test/code_mentor_benchmark.csv`
- xuất `Test/input_quality_evidence.csv`
- xuất `Test/question_generation_evidence.csv`
- xuất `Test/code_mentor_evidence.csv`
- sinh file `.md` parsed evidence cho từng run trong `Test/report_evidence/`
- đồng bộ các sheet config/report từ source-of-truth trong repo

## Lưu ý về benchmark sheet

- script chỉ auto-fill các cột có thể lấy chắc chắn từ runtime:
  - run id, ngày test, model, token, cost, latency, config version, status
  - `tester` sẽ tự lấy từ biến môi trường `AI_BENCHMARK_TESTER`, nếu không có thì fallback về user của máy đang chạy
- các cột chấm tay như:
  - các quality score học thuật cần người chấm
  - `expected_feedback_match`
  - các nhận xét so sánh sâu
  vẫn để trống để nhóm tự nhập sau khi review
- với một số run cũ chưa có `ConfigReferences`, các cột `prompt_version`, `rubric_version`, `policy_version`, `ground_truth_set_id` có thể là `N/A`

## Gợi ý naming workbook

Ví dụ:

```text
AI_Benchmark_Report_v1.xlsx
AI_Benchmark_Report_2026_Q3.xlsx
AI_Benchmark_Report_Final_Defense.xlsx
```
