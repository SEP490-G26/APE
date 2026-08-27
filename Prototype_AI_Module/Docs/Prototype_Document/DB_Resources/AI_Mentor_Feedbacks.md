# AI_Mentor_Feedbacks

## Mục đích

Collection này lưu feedback có cấu trúc của `AI Code Mentor` cho từng lần review một bài nộp `PE`.

`AI Mentor` trong prototype và định hướng production hiện tại:

- chỉ áp dụng cho `PE`
- review theo kiểu `static code review`
- chưa được quyền khẳng định compile/run pass nếu chưa nối `Judge0`
- phải gắn với `submission_id` để truy vết lại lịch sử nhận xét

## Cấu trúc đề xuất

```json
{
  "_id": "ObjectId",
  "agent_id": "ObjectId",
  "submission_id": "ObjectId",

  "question_type": "PE",
  "verdict": "correct|acceptable_with_minor_notes|needs_fix|incorrect",

  "feedback_text": "String",
  "suggested_complexity": "String",

  "quality_score": {
    "overall": "Number",
    "correctness": "Number",
    "robustness": "Number",
    "code_quality": "Number",
    "efficiency": "Number",
    "confidence": "Number"
  },

  "performance_summary": {
    "summary": "String",
    "time_complexity": "String",
    "space_complexity": "String",
    "notes": ["String"]
  },

  "error_analysis": [
    {
      "category": "String",
      "severity": "String",
      "title": "String",
      "detail": "String",
      "failing_scenarios": ["String"]
    }
  ],

  "improvement_suggestions": [
    {
      "priority": "String",
      "title": "String",
      "detail": "String",
      "expected_impact": "String"
    }
  ],

  "issue_categories": ["String"],

  "provider": "String",
  "model_name": "String",

  "prompt_version": "String",
  "rubric_version": "String",
  "policy_version": "String",

  "review_mode": "String",
  "usage_source": "String",
  "tokens_used": "Number",
  "cost_usd": "Number",

  "date": "DateTime",
  "created_at": "DateTime",
  "updated_at": "DateTime"
}
```

## Ghi chú field

- `agent_id`: AI agent thực hiện mentor review.
- `submission_id`: id của đúng một bài nộp cần review.
- `question_type`: cố định là `PE`.
- `verdict`: kết luận tổng quát của mentor.
- `feedback_text`: bản tóm tắt ngắn, dễ đọc, dùng cho lịch sử nhanh hoặc export.
- `suggested_complexity`: độ phức tạp mà AI suy luận được một cách thận trọng.

### quality_score

- `overall`: điểm tổng hợp toàn cục, thang `0 -> 10`.
- `correctness`: mức độ đúng logic theo suy luận từ code và đề bài.
- `robustness`: khả năng handle edge case và input xấu.
- `code_quality`: mức độ rõ ràng, tổ chức, maintainability.
- `efficiency`: đánh giá về độ phức tạp và lựa chọn giải pháp.
- `confidence`: độ tự tin của AI, thang `0 -> 1`.

### performance_summary

- `summary`: nhận xét ngắn về hiệu năng/tính phù hợp tổng quát.
- `time_complexity`: độ phức tạp thời gian nếu suy luận được.
- `space_complexity`: độ phức tạp bộ nhớ nếu suy luận được.
- `notes`: ghi chú phụ về performance, giới hạn suy luận, scalability.

### error_analysis

Mỗi item là một lỗi hoặc rủi ro đáng chú ý:

- `category`: ví dụ `logic`, `syntax`, `edge_case`, `runtime_risk`, `complexity`, `style`, `partial_solution`, `algorithm_choice`.
- `severity`: ví dụ `low`, `medium`, `high`.
- `title`: tiêu đề ngắn của vấn đề.
- `detail`: mô tả chi tiết vấn đề.
- `failing_scenarios`: các tình huống fail mà AI có thể suy luận được từ code và đề bài.

### improvement_suggestions

Mỗi item là một hướng cải thiện:

- `priority`: ví dụ `low`, `medium`, `high`.
- `title`: tiêu đề ngắn của đề xuất.
- `detail`: mô tả cụ thể cách cải thiện.
- `expected_impact`: lợi ích dự kiến nếu áp dụng.

## Quyết định thiết kế

### 1. `submission_id` nên là `ObjectId`, không phải `Array[ObjectId]`

Lý do:

- mỗi feedback nên gắn với đúng một lần nộp bài
- một submission có thể có nhiều lần mentor review thì tạo nhiều document riêng
- cách này dễ query lịch sử hơn và đúng với nghiệp vụ hơn

### 2. Giữ cả `feedback_text` lẫn structured fields

Lý do:

- `feedback_text` tiện cho list view, export nhanh, log ngắn
- structured fields phục vụ UI chi tiết, benchmark, report, so sánh model, audit

### 3. Cần lưu version cấu hình AI

- `prompt_version`
- `rubric_version`
- `policy_version`

Lý do:

- phục vụ benchmark
- giải thích vì sao feedback thay đổi theo từng version
- hỗ trợ audit khi đưa vào production

### 4. Cần chừa chỗ cho production enrichment

Các field như:

- `review_mode`
- `usage_source`
- `tokens_used`
- `cost_usd`

giúp phân biệt:

- review AI-only
- review AI + Judge0 sau này
- nguồn usage là estimate hay provider-raw

## Mapping với prototype hiện tại

Trong prototype hiện tại, `MentorFeedback` đã được đổi theo hướng gần với schema trên:

- `question_type`
- `verdict`
- `quality_score`
- `performance_summary`
- `error_analysis`
- `improvement_suggestions`
- `issue_categories`
- `feedback_text`
- `suggested_complexity`
- `model_name`

Các field DB khác như:

- `agent_id`
- `submission_id`
- `prompt_version`
- `rubric_version`
- `policy_version`
- `tokens_used`
- `cost_usd`
- `date`

sẽ được map đầy đủ hơn khi ghép với BE thật và Mongo repository thật.
