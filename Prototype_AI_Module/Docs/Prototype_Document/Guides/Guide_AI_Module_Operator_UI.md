# Hướng dẫn giao diện vận hành AI Module

## Mục tiêu

UI này là operator console để chạy thử, benchmark và kiểm tra pipeline của AI module.

## Các khu vực chính

### 1. Overview

- reload provider state
- ping provider
- xem model catalog
- xem prompt registry
- xem cấu hình provider sau khi reload `.env`

### 2. Gatekeeper

- nhập file hoặc nội dung thô
- chọn môn học
- chọn provider/model
- xem raw JSON và parsed result

### 3. Extracted Content

- chạy `TextOnly` hoặc `FullMultimodalPage`
- nạp file qua route ingest
- xem markdown preview, token, cost và JSON raw

### 4. Embedding + Auto Tagging

- chọn embedding provider/model
- chọn tagging provider/model
- xem chunk, tag, token, cost
- export chunk JSON để feed sang generation/review

### 5. Generation + Review

- hỗ trợ 3 input strategy:
  - `Chunks JSON`
  - `Context Pack ID`
  - `Retrieval Planner`
- có thể bấm `Plan Retrieval Pack` để build trước `context pack`
- có thể dùng lại `Last Context Pack` để chạy lại cùng ngữ cảnh
- chọn mode `SingleAgent`, `SameModelDualRole`, `DualAgent`
- chọn loại câu hỏi `FE` hoặc `PE`
- xem `source token`, `packed token`, `compression ratio`
- xem `selected chunks`, `packed summary`, `packed context`
- xem output AI1, output AI2, kết quả cuối cùng
- xem raw JSON từng bước để đưa vào báo cáo

### 6. Code Mentor

- nhập nhiều file code
- chọn provider/model
- xem feedback dạng đọc được và JSON raw

### 7. History + Sessions

- xem lại lịch sử test theo function
- filter theo model, status, date
- mở chi tiết từng run
- export JSON hoặc TSV
- lưu và mở benchmark session

## File UI liên quan

- `src-dotnet/Ape.AiModule.Api/wwwroot/index.html`
- `src-dotnet/Ape.AiModule.Api/wwwroot/app.js`
- `src-dotnet/Ape.AiModule.Api/wwwroot/styles.css`

## Ghi chú vận hành

- UI này là console cho prototype, không phải FE production.
- Dữ liệu hiển thị ưu tiên kiểu `JSON-first` theo DTO backend.
- Các tab đều nên được dùng kèm history để lưu bằng chứng test.
- Với flow production-test-harness cho tạo đề, thứ tự nên là:
  1. `Extraction -> Embedding`
  2. `Plan Retrieval Pack`
  3. `Generation + Review` bằng `Context Pack ID`
