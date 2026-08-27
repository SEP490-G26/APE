# Hướng dẫn chạy AI Module

## Mục tiêu

Tài liệu này hướng dẫn cách build, cấu hình và chạy prototype `.NET` của AI module.

## Thư mục chính

- solution: `src-dotnet/Ape.AiModule.slnx`
- web app: `src-dotnet/Ape.AiModule.Api`
- tài liệu: `Docs/Prototype_Document/`

## Yêu cầu môi trường

- `.NET SDK 10`
- kết nối internet nếu cần gọi provider thật
- API key hợp lệ cho `OpenAI`, `Gemini`, `Cohere`

Kiểm tra nhanh:

```powershell
dotnet --version
```

## Cấu hình API key và endpoint

File cấu hình kiểu `ENV` nằm tại:

- `src-dotnet/Ape.AiModule.Api/.env`

Mẫu cấu hình:

```env
AIProviders__OpenAI__ApiKey=your_openai_key
AIProviders__OpenAI__BaseUrl=https://api.openai.com/v1/
AIProviders__OpenAI__ApiVersion=v1

AIProviders__Gemini__ApiKey=your_gemini_key
AIProviders__Gemini__BaseUrl=https://generativelanguage.googleapis.com/
AIProviders__Gemini__ApiVersion=v1beta

AIProviders__Cohere__ApiKey=your_cohere_key
AIProviders__Cohere__BaseUrl=https://api.cohere.com/
AIProviders__Cohere__ApiVersion=v1

AI_BENCHMARK_TESTER=member_name
```

Gợi ý:

- `AI_BENCHMARK_TESTER` nên đặt theo tên ngắn thống nhất của từng thành viên, ví dụ:
  - `anhtuan`
  - `linhpt`
  - `hungnq`
- giá trị này sẽ được tự động ghi vào các sheet `Benchmark` trong report để tránh phải điền tay lại người test.

Sau khi sửa `.env`, có thể reload mà không cần restart app:

```http
POST /api/ai-module/providers/reload
```

## Build

```powershell
dotnet build .\src-dotnet\Ape.AiModule.slnx
```

## Run

```powershell
dotnet run --project .\src-dotnet\Ape.AiModule.Api
```

UI operator được serve trực tiếp từ `wwwroot` của API.

Static files của prototype hiện được serve theo chế độ `no-cache`:

- `app.js`
- các file trong `wwwroot/js/`
- các fragment HTML trong `wwwroot/fragments/`

Mục đích là tránh UI bị giữ cache cũ khi đang test và sửa prototype liên tục.

Ngay khi startup, app sẽ:

- tự tạo các thư mục runtime bị `gitignore` như `run-history`, `uploads`, `extraction-drafts`, `context-packs`, `benchmark-sessions`
- kiểm tra các file source/config bắt buộc trong `App_Data`
- dừng ngay với lỗi dễ đọc nếu thiếu `prompt`, `policy`, `rubric`, hoặc `ground truth`

Điều này giúp clone mới chỉ cần:

1. `git pull`
2. tạo `.env`
3. `dotnet run`

## Kiểm tra sau khi chạy

1. Mở UI local do ASP.NET Core in ra.
2. Vào phần `Overview`.
3. Bấm `Reload Providers`.
4. Ping lần lượt `OpenAI`, `Gemini`, `Cohere`.
5. Mở các tab chức năng để chạy thử.

## Các route cần nhớ

- `GET /api/ai-module/providers`
- `GET /api/ai-module/providers/{provider}/models`
- `GET /api/ai-module/providers/{provider}/ping`
- `POST /api/ai-module/uploads/ingest`
- `POST /api/ai-module/gatekeeper`
- `POST /api/ai-module/extracted-content`
- `POST /api/ai-module/embedding-tagging`
- `POST /api/ai-module/retrieval/plan`
- `GET /api/ai-module/context-packs/{packId}`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/code-mentor`
- `GET /api/ai-module/history`

## Nơi lưu dữ liệu runtime

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- `src-dotnet/Ape.AiModule.Api/App_Data/*-policy.json`
- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/`
- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/`
- `src-dotnet/Ape.AiModule.Api/App_Data/run-history/`
- `src-dotnet/Ape.AiModule.Api/App_Data/context-packs/`
- `src-dotnet/Ape.AiModule.Api/App_Data/uploads/`

Phân loại:

- `ai-prompts.json`, `*-policy.json`, `ai-rubrics/`, `ai-groundtruth/`, `ai-config-history/`: dữ liệu nguồn, phải có trong git
- `run-history/`, `context-packs/`, `uploads/`, `extraction-drafts/`, `benchmark-sessions/`, `Test/`: dữ liệu runtime, không commit

## Report sync

Sau mỗi run được lưu vào `run-history`, prototype sẽ sync lại report workbook.

File runtime được cập nhật tại `Test/`:

- `AI_Benchmark_Report.xlsx`
- `input_quality_benchmark.csv`
- `question_generation_benchmark.csv`
- `code_mentor_benchmark.csv`
- `input_quality_evidence.csv`
- `question_generation_evidence.csv`
- `code_mentor_evidence.csv`
- `report_evidence/*.md`

Lưu ý:

- `Benchmark` sheet: dùng để tổng hợp metadata, token, cost, config version và các cột chấm tay.
- `Evidence` sheet: dùng để đọc nhanh input/output đã parse.
- script hiện auto-fill các cột runtime/config chắc chắn; các cột đánh giá thủ công vẫn để nhóm nhập thêm trong Excel.

## Ghi chú vận hành

- `full-pipeline` hiện chỉ gồm `ingestion + generation/review`.
- `AI Code Mentor` hiện là `PE-only`.
- `AI Code Mentor` hiện là `static review`, chưa có compile/run thật và chưa được xem như kết quả verified execution trước khi nối `Judge0`.
- `question-generation/debug` và `generation-review/debug` hiện vẫn là debug placeholder.
- PDF đã dùng parser thật qua `PdfPig`.
- `docx/pptx` đang ở mức parser prototype bằng OpenXML text + media extraction.
- Để test gần production cho tạo đề, thứ tự nên là:
  1. `Extracted Content`
  2. `Embedding + Auto Tagging`
  3. `Retrieval Plan`
  4. `Generation + Review` bằng `contextPackId`
- Nếu cần so model công bằng, nên dùng lại cùng một `contextPackId`.
