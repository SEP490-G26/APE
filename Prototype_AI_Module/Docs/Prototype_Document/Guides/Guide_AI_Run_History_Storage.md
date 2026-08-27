# Hướng dẫn lưu run history

## Mục tiêu

Prototype lưu lại mỗi lần chạy AI function thành file JSON để:

- giữ bằng chứng benchmark
- xem lại input/output sau khi test
- copy số liệu cho report, sheet Excel và tài liệu đồ án

## Vị trí lưu

Tất cả run được lưu tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/run-history/`

Mỗi function có một folder riêng:

- `gatekeeper`
- `extracted-content`
- `embedding-tagging`
- `question-generation`
- `question-review`
- `generation-review`
- `code-mentor`
- `ingestion`
- `full-pipeline`

## Định dạng file

Mỗi lần chạy sẽ tạo 1 file:

- `<function>-<timestamp>-<guid>.json`

Ví dụ:

- `gatekeeper-20260716033003761-2e1917739d59401895989d42c541058.json`

## Cấu trúc JSON

Mỗi file có 3 khối:

```json
{
  "summary": {
    "runId": "...",
    "functionName": "gatekeeper",
    "routeKey": "gatekeeper",
    "recordedAt": "...",
    "requestType": "GatekeeperRequest",
    "responseType": "GatekeeperResult",
    "status": "supported",
    "modelSummary": "Model=gpt-4o",
    "modelFields": {
      "provider": "openai",
      "primaryModel": "gpt-4o"
    },
    "usageSource": "raw",
    "error": null,
    "totals": {
      "inputTokens": 25,
      "outputTokens": 40,
      "inputCostUsd": 0.000004,
      "outputCostUsd": 0.000024,
      "totalCostUsd": 0.000028,
      "latencyMs": 120
    },
    "fileName": "..."
  },
  "request": { ... },
  "response": { ... }
}
```

## Metadata mới trong `summary`

`summary` hiện tại không chỉ có `status`, `modelSummary`, `totals`, mà còn có:

- `modelFields`
  - chuẩn hoá các field model theo từng function
- `usageSource`
  - `raw | estimated | mixed`
- `error`
  - lỗi chuẩn hoá nếu provider/runtime có vấn đề

## Ý nghĩa nghiệp vụ

### `usageSource`

Giúp phân biệt:

- số token/cost này lấy từ provider usage thật
- hay là estimate
- hay là tổng hợp từ nhiều stage khác nhau

### `error`

Nếu có lỗi provider/runtime, summary có thể chứa:

- `category`
- `code`
- `providerStatus`
- `retryable`
- `stage`
- `rawMessage`

Điều này quan trọng khi benchmark:

- quota
- model not found
- auth fail
- request invalid
- provider overload

## API liên quan

| Method | Route | Mục đích |
|---|---|---|
| `GET` | `/api/ai-module/history?functionName=&take=` | Lấy lịch sử tổng hoặc lọc theo function |
| `GET` | `/api/ai-module/history/{functionName}?take=` | Lấy lịch sử của một function |
| `GET` | `/api/ai-module/history/{functionName}/{runId}` | Lấy chi tiết một run |

## Lưu ý

- lịch sử hiện tại là `file-backed`, chưa dùng Mongo
- đây là cơ chế phù hợp cho prototype, test và benchmark
- khi ghép vào BE thật, có thể giữ cơ chế export file này song song với log DB để làm artifact benchmark
