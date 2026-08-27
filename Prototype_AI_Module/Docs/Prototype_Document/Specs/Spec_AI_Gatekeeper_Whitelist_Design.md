# AI Gatekeeper Whitelist Design

## Mục tiêu

`AI Gatekeeper` có vai trò:

- đọc nhanh nội dung tài liệu đã extract
- kiểm tra tài liệu có thuộc whitelist môn học được hệ thống hỗ trợ hay không
- trả về kết quả phân loại chuẩn hoá để hệ thống quyết định có cho đi tiếp sang pipeline sau hay không

## Whitelist hiện tại

- `C`
- `JAVA_OOP`
- `DSA_JAVA`

## Kiểu kết quả

Gatekeeper trả về:

- `supported`
- `unsupported`
- `ambiguous`

## Policy runtime

Policy hiện tại được lưu tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/gatekeeper-policy.json`

Nó chứa:

- danh sách môn được hỗ trợ
- topic example của từng môn
- quy tắc verdict
- precheck rule

## Rubric và ground truth

Rubric nhẹ:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/GATEKEEPER.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/GATEKEEPER_CORE.json`

## Route đã có

- `POST /api/ai-module/gatekeeper`
- `POST /api/ai-module/gatekeeper/debug`
- `GET /api/ai-module/gatekeeper/policy`
- `GET /api/ai-module/gatekeeper/rubric`
- `GET /api/ai-module/gatekeeper/ground-truth`

## Output chuẩn hoá

Gatekeeper verdict hiện tại có các field chính:

- `isSupported`
- `verdict`
- `primaryDomain`
- `matchedSubjects`
- `confidence`
- `reason`
- `rejectionReasonCode`
- `detectedTopics`
- `modelName`

## Ghi chú nghiệp vụ

Gatekeeper chỉ nên làm đúng 1 việc:

- xác định có thuộc whitelist hay không

Nó không nên:

- OCR sâu
- chunking
- embedding
- sinh metadata quá sâu

Nếu content quá ngắn, rỗng hoặc có quá nhiều loại môn học trộn lẫn:

- nên trả `ambiguous`

để tránh classify sai.
