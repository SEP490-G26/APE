# AI Module Operator UI

## Muc tieu

UI nay la trang van hanh don gian cho prototype `Ape.AiModule.Api`.

No phuc vu 4 viec:

- chay nhanh 6 AI function va 2 composite flow
- xem ket qua co cau truc thay vi chi nhin JSON tho
- tai source of truth runtime: prompt, policy, rubric, ground truth
- chon provider/model truc tiep tu catalog backend

## Thanh phan chinh

### 1. Overview

- reload provider state
- ping tung provider
- xem danh sach model provider tra ve
- xem prompt registry hien tai
- xem provider config sau khi reload `.env`

### 2. Gatekeeper

- form input dung contract `GatekeeperRequest`
- chon provider va model
- co nut `Run Debug`
- co nut load `prompt/policy/rubric/ground truth`

### 3. Extracted Content

- form input dung contract `ExtractedContentRequest`
- support `TextOnly` va `FullMultimodalPage`
- chon vision provider/model
- xem output markdown preview va JSON
- co the dung file ingest route de nap `docx/pptx/pdf/image` truoc

### 4. Embedding + Auto Tagging

- form input dung contract `EmbeddingTaggingRequest`
- chon embedding provider/model
- chon tagging provider/model
- xem summary chunk, cost/token va JSON raw
- export chunk JSON de feed sang generation/review

### 5. Generation + Review

- form input dung contract `GenerationReviewRequest`
- chon generator provider/model va reviewer provider/model
- nhap `chunks` JSON truc tiep
- support `DualAgent`, `SameModelDualRole`, `SingleAgent`
- co nut `Run Export Package`
- luu y: route `generation-review/debug` hien dang la debug-placeholder, con `Run Export Package` moi la output audit day du hon

### 6. Code Mentor

- form input dung contract `CodeMentorRequest`
- chon provider/model
- xem structured preview va JSON raw

### 7. Ingestion

- form input dung contract `IngestionRequest`
- hop flow `gatekeeper -> extracted-content -> embedding-tagging`

### 8. Full Pipeline

- gui dong thoi `IngestionRequest` va `GenerationReviewRequest`
- dung de test full flow prototype
- luu y: full pipeline hien gom `ingestion + generation/review`, chua bao gom `code mentor`

### 9. History + Sessions

- xem run history theo function
- filter theo model/status/date
- xem parsed result + raw JSON
- copy/export TSV va JSON
- save/open benchmark sessions

### 10. Prompt Studio

- load prompt tu `/api/ai-module/prompts`
- edit va save prompt qua `PUT /api/ai-module/prompts/{key}`
- reload prompt file qua `/api/ai-module/prompts/reload`

## File lien quan

- UI HTML: `src-dotnet/Ape.AiModule.Api/wwwroot/index.html`
- UI JS: `src-dotnet/Ape.AiModule.Api/wwwroot/app.js`
- UI CSS: `src-dotnet/Ape.AiModule.Api/wwwroot/styles.css`

## Luu y

- UI hien tai la operator console cho prototype, khong phai FE production.
- Cac form van la `JSON-first` theo DTO backend.
- Doi voi tai lieu binary that (`pdf/docx/pptx/image`), da co route `POST /api/ai-module/uploads/ingest` de luu file va tra ve `suggestedRawContent`.
- `pdf` hien tai da dung parser that qua `PdfPig`.
- `docx/pptx` dang doc text tu OpenXML va boc media tu package zip, phu hop cho production test harness muc prototype.
- UI hien thi duoc token/cost, `usage_source`, va thong tin loi chuan hoa trong history/export.
- Khong phai moi nut `Run Debug` deu map toi mot debug payload rieng o backend; hien tai QGen/GenReview van con placeholder.
