# AI Run History File Storage

## Muc tieu

Prototype luu lai moi lan chay AI function thanh file JSON de:

- giu bang chung benchmark
- xem lai input/output sau khi test
- copy so lieu cho report, sheet Excel, tai lieu do an

## Vi tri luu

Tat ca run duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/run-history/`

Moi function co 1 folder rieng:

- `gatekeeper`
- `extracted-content`
- `embedding-tagging`
- `question-generation`
- `question-review`
- `generation-review`
- `code-mentor`
- `ingestion`
- `full-pipeline`

## Dinh dang file

Moi lan chay se tao 1 file:

- `<function>-<timestamp>-<guid>.json`

Vi du:

- `gatekeeper-20260716033003761-2e1917739d59401895989d42c541058.json`

## Cau truc JSON

Moi file co 3 khoi:

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

## Metadata moi trong summary

`summary` hien tai khong chi co `status/modelSummary/totals`, ma con co:

- `modelFields`
  - chuan hoa cac field model theo tung function
- `usageSource`
  - `raw` | `estimated` | `mixed`
- `error`
  - loi chuan hoa neu provider/runtime co van de

## Y nghia nghiep vu

### `usageSource`

Giup phan biet:

- so token/cost nay lay tu provider usage that
- hay la estimate
- hay la tong hop tu nhieu stage khac nhau

### `error`

Neu co loi provider/runtime, summary co the chua:

- `category`
- `code`
- `providerStatus`
- `retryable`
- `stage`
- `rawMessage`

Dieu nay quan trong khi benchmark:

- quota
- model not found
- auth fail
- request invalid
- provider overload

## API lien quan

| Method | Route | Muc dich |
|---|---|---|
| `GET` | `/api/ai-module/history?functionName=&take=` | Lay lich su tong hoac loc theo function |
| `GET` | `/api/ai-module/history/{functionName}?take=` | Lay lich su cua 1 function |
| `GET` | `/api/ai-module/history/{functionName}/{runId}` | Lay chi tiet 1 run |

## Luu y

- Lich su hien tai la `file-backed`, chua dung Mongo.
- Day la co che phu hop cho prototype/test/benchmark.
- Khi ghep vao BE that, co the giu co che export file nay song song voi log DB de lam artifact benchmark.
