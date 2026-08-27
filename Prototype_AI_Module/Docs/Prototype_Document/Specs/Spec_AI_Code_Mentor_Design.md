# AI Code Mentor Design

## Mục tiêu

`AI Code Mentor` có vai trò:

- nhận đề bài và đoạn code sinh viên nộp
- phân tích lỗi kỹ thuật có thể suy ra từ code
- trả về feedback có cấu trúc để sinh viên biết sai ở đâu và nên sửa gì

## Scope hiện tại

Prototype hiện tại:

- không compile/run code
- không kết luận dựa trên kết quả judge thật
- chỉ review dựa trên đề bài + code + rubric/policy

Compile/run verification sẽ để cho Judge0 hoặc pipeline execution sau này.

## Policy runtime

Policy hiện tại được lưu tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/code-mentor-policy.json`

Policy chứa:

- supported languages
- issue categories
- verdict set
- feedback rules

## Rubric và ground truth

Rubric nhẹ:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/CODE_MENTOR.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/CODE_MENTOR_CORE.json`

## Prompt

Prompt hiện tại được lưu tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

Đã nâng cấp để:

- nhận policy json
- nhận rubric json
- trả JSON only
- tách rõ verdict, categories, issues, suggestions, failing scenarios, confidence, complexity

## Route đã có

- `POST /api/ai-module/code-mentor`
- `POST /api/ai-module/code-mentor/debug`
- `GET /api/ai-module/code-mentor/policy`
- `GET /api/ai-module/code-mentor/rubric`
- `GET /api/ai-module/code-mentor/ground-truth`

## Output chuẩn hoá

Mentor feedback hiện tại gồm:

- `verdict`
- `issueCategories`
- `issues`
- `suggestions`
- `failingScenarios`
- `confidence`
- `complexity`
- `modelName`

## Ghi chú nghiệp vụ

Mentor nên:

- nói đúng và cụ thể
- tránh phê bình mơ hồ
- đề xuất cách sửa rõ ràng
- không khẳng định đã run code nếu chưa run

Nếu sau này ghép Judge0:

- Mentor có thể nhận thêm compile result / runtime result / failed test cases
- khi đó chất lượng feedback sẽ tăng rõ rệt
