# Benchmark AI Cost

Workspace nay hien co 2 huong code:

1. prototype NodeJS cu
2. prototype `.NET` moi cho `AI core module`

Trang thai hien tai:

- huong `.NET` la huong chinh dang tiep tuc phat trien
- huong nay dung de benchmark, production test harness, va lam base ghep vao BE that sau nay

## Cau truc chinh

- `src-dotnet/`
  - solution `.NET` hien tai
  - chua API, operator UI, contracts, services
- `Docs/`
  - tai lieu thiet ke, route, run guide, DB-ready collections
- `backend/`
  - benchmark NodeJS cu
- `frontend/`
  - frontend benchmark NodeJS cu

## Entry point hien tai

Neu ban can chay module AI dang dung hien tai, di theo huong `.NET`:

- solution: `src-dotnet/Ape.AiModule.slnx`
- web app: `src-dotnet/Ape.AiModule.Api`

Tai lieu can doc truoc:

1. [Run Guide](D:/1_Tailieuhoctap%20REAL%20Reborn/Ky9%20%20-%20Final/Benchmark%20AI%20Cost/Docs/AI_Module_Run_Guide.md)
2. [6 Functions Summary](D:/1_Tailieuhoctap%20REAL%20Reborn/Ky9%20%20-%20Final/Benchmark%20AI%20Cost/Docs/AI_Module_6_Functions_Summary.md)
3. [API Routes Summary](D:/1_Tailieuhoctap%20REAL%20Reborn/Ky9%20%20-%20Final/Benchmark%20AI%20Cost/Docs/AI_Module_API_Routes_Summary.md)
4. [Operator UI](D:/1_Tailieuhoctap%20REAL%20Reborn/Ky9%20%20-%20Final/Benchmark%20AI%20Cost/Docs/AI_Module_Operator_UI.md)

## Chay nhanh module `.NET`

Yeu cau:

- `.NET SDK 10`
- file `.env` trong `src-dotnet/Ape.AiModule.Api`

Build:

```powershell
dotnet build .\src-dotnet\Ape.AiModule.slnx
```

Run:

```powershell
dotnet run --project .\src-dotnet\Ape.AiModule.Api
```

Sau khi app chay:

- mo URL local do ASP.NET Core in ra
- UI operator se duoc serve truc tiep tu `wwwroot`

## Provider config `.NET`

File:

- `src-dotnet/Ape.AiModule.Api/.env`

Mau key:

```env
AIProviders__OpenAI__ApiKey=...
AIProviders__OpenAI__BaseUrl=...

AIProviders__Gemini__ApiKey=...
AIProviders__Gemini__BaseUrl=...

AIProviders__Cohere__ApiKey=...
AIProviders__Cohere__BaseUrl=...
```

Co the reload config sau khi sua `.env` bang route:

```http
POST /api/ai-module/providers/reload
```

## Cac tinh nang chinh cua module `.NET`

6 function AI:

1. `AI Gatekeeper`
2. `AI Extracted Content`
3. `AI Embedding + Auto Tagging`
4. `AI Question Generation`
5. `AI Review Result of AI Question Generation`
6. `AI Code Mentor`

Them cac flow tong hop:

- `Ingestion`
- `Full Pipeline`
- `Generation + Review`

## Diem ky thuat hien tai

Module `.NET` hien tai da co:

- provider auto load models
- prompt/policy/rubric/ground truth file-backed
- run history file-backed
- benchmark sessions
- raw usage capture neu provider tra usage that
- `usage_source = raw | estimated | mixed`
- normalized error catalog o muc runtime
- parser `pdf` that qua `PdfPig`
- parser `docx/pptx` bang OpenXML text + media extraction muc prototype

## Noi luu du lieu runtime

Thu muc:

- `src-dotnet/Ape.AiModule.Api/App_Data`

Trong do co:

- `ai-prompts.json`
- `*-policy.json`
- `ai-rubrics/*.json`
- `ai-groundtruth/*.json`
- `run-history/...`
- `uploads/...`

## API chinh

Provider:

- `GET /api/ai-module/providers`
- `GET /api/ai-module/providers/{provider}/models`
- `GET /api/ai-module/providers/{provider}/ping`
- `POST /api/ai-module/providers/reload`

Main runtime:

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

Upload:

- `POST /api/ai-module/uploads/ingest`

History:

- `GET /api/ai-module/history`
- `GET /api/ai-module/history/{functionName}`
- `GET /api/ai-module/history/{functionName}/{runId}`

Sessions:

- `POST /api/ai-module/benchmark-sessions`
- `GET /api/ai-module/benchmark-sessions`
- `GET /api/ai-module/benchmark-sessions/{sessionId}`

## Legacy NodeJS benchmark

Huong `backend/` + `frontend/` van con trong repo de tham chieu benchmark cu.

Neu can chay ban cu:

```bash
npm install
npm run dev:backend
npm run dev:frontend
```

Nhung hien tai, huong duoc uu tien de tiep tuc phat trien la `src-dotnet/`.

## Ghi chu

- Prototype `.NET` nay la base de merge vao BE tong sau nay.
- Hien tai no van la file-backed thay vi Mongo-backed.
- Khi chuyen vao production BE, se can noi repository that, Judge0, va storage that.
