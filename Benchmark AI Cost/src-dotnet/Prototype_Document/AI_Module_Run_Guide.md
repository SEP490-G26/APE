# AI Module Run Guide

## Muc tieu

Tai lieu nay huong dan chay prototype `.NET` cua AI module trong workspace nay.

Muc tieu:

- build va run nhanh
- cau hinh provider bang `.env`
- test ping ket noi
- mo UI operator de chay 6 function AI
- biet ro log va source of truth dang nam o dau

## 1. Thu muc chinh

Solution:

- `src-dotnet/Ape.AiModule.slnx`

API/UI project:

- `src-dotnet/Ape.AiModule.Api`

Backend contracts / services:

- `src-dotnet/Ape.AiModule.Application`
- `src-dotnet/Ape.AiModule.Infrastructure`
- `src-dotnet/Ape.AiModule.Domain`

## 2. Yeu cau moi truong

Can co:

- `.NET SDK 10`
- quyen goi internet neu can test provider that
- API key hop le cho `OpenAI`, `Gemini`, `Cohere` neu chay real provider

Kiem tra:

```powershell
dotnet --version
```

## 3. Cach cau hinh API key va endpoint

Project nay ho tro kieu `ENV` giong ben NodeJS thong qua file:

- `src-dotnet/Ape.AiModule.Api/.env`

`Program.cs` se tu dong load file `.env` nay neu ton tai, sau do nap vao `Configuration`.

### Mau `.env`

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
```

### Luu y

- Neu dung endpoint trung gian cua ben thu 3, thay `BaseUrl` tuong ung.
- `OpenAI` va `Gemini` co the can `BaseUrl`/`ApiVersion` khac neu provider trung gian khong giong endpoint mac dinh.
- Cac gia tri mac dinh co san trong:
  - `src-dotnet/Ape.AiModule.Api/appsettings.json`
  - `src-dotnet/Ape.AiModule.Api/appsettings.Development.json`

## 4. Build project

Tu root workspace:

```powershell
dotnet build .\src-dotnet\Ape.AiModule.slnx
```

Neu build thanh cong, API/UI da san sang de chay.

## 5. Run API + UI

Tu root workspace:

```powershell
dotnet run --project .\src-dotnet\Ape.AiModule.Api
```

Mac dinh ASP.NET Core se in ra URL local, thuong se la mot trong cac dang:

- `http://localhost:5xxx`
- `https://localhost:7xxx`

Mo trinh duyet vao root URL do.  
UI operator console duoc phuc vu truc tiep tu `wwwroot`.

## 6. Kiem tra provider sau khi run

### Lay danh sach provider

```http
GET /api/ai-module/providers
```

### Ping tung provider

```http
GET /api/ai-module/providers/openai/ping
GET /api/ai-module/providers/gemini/ping
GET /api/ai-module/providers/cohere/ping
```

### Reload cau hinh provider

Neu vua sua `.env`, co the reload bang route:

```http
POST /api/ai-module/providers/reload
```

UI Overview cung da co nut reload/ping/provider models.

## 7. Cac route quan trong de van hanh

### Upload file de ingest prototype

```http
POST /api/ai-module/uploads/ingest
```

Route nay:

- luu file vao `App_Data/uploads`
- co gang extract text cho `txt/docx/pptx/pdf`
- boc media cho `docx/pptx`
- tra ve `suggestedRawContent`, `parserName`, `parserVersion`, `extractedImagePaths`

### Chay cac function chinh

- `POST /api/ai-module/gatekeeper`
- `POST /api/ai-module/extracted-content`
- `POST /api/ai-module/embedding-tagging`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/code-mentor`
- `POST /api/ai-module/ingestion`
- `POST /api/ai-module/full-pipeline`

Luu y:

- `full-pipeline` hien gom `ingestion + generation/review`
- chua bao gom `code mentor`

### Debug + source of truth

Moi function chinh deu co:

- route `debug`
- route `policy`
- route `rubric`
- route `ground-truth`

Ngoai le hien tai:

- `question-generation/debug` va `generation-review/debug` dang la debug-placeholder
- 2 route nay chua tra debug envelope rieng nhu Gatekeeper / Extracted / Embedding / Mentor

## 8. App_Data dang luu gi

Thu muc:

- `src-dotnet/Ape.AiModule.Api/App_Data`

Dang luu:

- `ai-prompts.json`
- `gatekeeper-policy.json`
- `extracted-content-policy.json`
- `embedding-tagging-policy.json`
- `code-mentor-policy.json`
- `ai-rubrics/*.json`
- `ai-groundtruth/*.json`
- `run-history/...`
- `uploads/...`

## 9. Run history va benchmark session

### Run history

Moi lan chay function se luu vao:

- `src-dotnet/Ape.AiModule.Api/App_Data/run-history/{function-name}`

Moi file run la 1 JSON record co:

- request
- response
- summary
- totals
- model fields
- usage source
- normalized error

### Benchmark sessions

Session save/open qua route:

- `POST /api/ai-module/benchmark-sessions`
- `GET /api/ai-module/benchmark-sessions`
- `GET /api/ai-module/benchmark-sessions/{sessionId}`

Dung de gom nhieu run cua cung mot dot test.

## 10. Cac diem moi quan trong trong phien ban hien tai

### Provider raw usage capture

Neu provider tra `usage` that, he thong se co gang bat:

- `inputTokens`
- `outputTokens`
- `cacheInputTokens`
- `cacheReadTokens`
- `reasoningTokens`
- `rawUsageJson`

Va danh dau:

- `usage_source = raw`

Neu provider khong tra usage, he thong se estimate va danh dau:

- `usage_source = estimated`

Neu tong hop tu nhieu stage co ca `raw` va `estimated`, tong co the hien:

- `usage_source = mixed`

### Normalized error

Neu provider loi, he thong se chuan hoa sang:

- `category`
- `code`
- `providerStatus`
- `retryable`
- `stage`
- `rawMessage`

Muc dich:

- de xem lich su ro hon
- de so sanh model/provider khi chay production test harness

### PDF parser that

PDF hien tai da dung:

- `PdfPig`

Nen parser PDF da tot hon cach regex byte prototype truoc day.

`docx` va `pptx` dang di theo huong:

- doc XML text
- extract media tu zip package
- chen image markers de noi vao luong vision neu can

## 11. UI operator dung de lam gi

UI hien tai phuc vu 3 nhom viec:

1. test runtime
2. benchmark model/cost
3. test production harness truoc khi merge vao BE tong

UI da co:

- provider overview + ping + load models
- 6 tab function AI
- compare lab
- batch preset runner
- matrix runner
- history browser
- session save/open
- export JSON/TSV

## 12. Luong chay de xac minh nhanh

De test nhanh sau khi run:

1. vao UI root
2. vao `Overview`
3. bam `Reload Providers`
4. ping `OpenAI/Gemini/Cohere`
5. vao `Gatekeeper`, chay 1 case text ngan
6. vao `Extracted Content`, thu 1 file `txt` hoac `docx`
7. vao `Embedding + Auto Tagging`, xac nhan chunks tra ve
8. vao `Generation + Review`, nap chunk JSON va chay `FE` hoac `PE`
9. vao `History`, mo lai run de xem `usage_source`, `error`, `cost`

## 13. Khi nao can restart app

Nen restart app khi:

- thay package / code backend
- thay doi lon lien quan `appsettings`

Khong bat buoc restart neu chi:

- sua `.env` va da goi `POST /api/ai-module/providers/reload`
- sua prompt va da goi `POST /api/ai-module/prompts/reload`

## 14. Gioi han hien tai

Prototype nay da du de test production harness, nhung chua phai ban production cuoi cung.

Van con cac khoang trong:

- chua noi Mongo repository that
- chua compile/run PE qua Judge0
- chua co OCR production-final cho tai lieu kho
- `docx/pptx` parser dang tot cho prototype, chua phai parser semantic layout-final
- cost van phu thuoc vao muc do provider co tra usage that hay khong
- `question-generation/debug` va `generation-review/debug` chua co payload debug rieng

## 15. Lenh nhanh

Build:

```powershell
dotnet build .\src-dotnet\Ape.AiModule.slnx
```

Run:

```powershell
dotnet run --project .\src-dotnet\Ape.AiModule.Api
```

Kiem tra JS operator:

```powershell
node --check .\src-dotnet\Ape.AiModule.Api\wwwroot\app.js
```

## Ket luan

Tinh den hien tai, module nay da o muc:

- chay duoc local
- provider-configurable
- UI operator-ready
- benchmark-ready
- production-test-harness-ready o muc prototype
