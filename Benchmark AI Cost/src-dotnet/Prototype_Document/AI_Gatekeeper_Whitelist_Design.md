# AI Gatekeeper Whitelist Design

## Muc tieu

`AI Gatekeeper` co vai tro:

- doc nhanh noi dung tai lieu da extract
- kiem tra tai lieu co thuoc whitelist mon hoc duoc he thong ho tro hay khong
- tra ve ket qua phan loai chuan hoa de he thong quyet dinh co cho di tiep sang pipeline sau hay khong

## Whitelist hien tai

- `C`
- `JAVA_OOP`
- `DSA_JAVA`

## Kieu ket qua

Gatekeeper tra ve:

- `supported`
- `unsupported`
- `ambiguous`

## Policy runtime

Policy hien tai duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/gatekeeper-policy.json`

No chua:

- danh sach mon duoc ho tro
- topic example cua tung mon
- quy tac verdict
- precheck rule

## Rubric va ground truth

Rubric nhe:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/GATEKEEPER.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/GATEKEEPER_CORE.json`

## Route da co

- `POST /api/ai-module/gatekeeper`
- `POST /api/ai-module/gatekeeper/debug`
- `GET /api/ai-module/gatekeeper/policy`
- `GET /api/ai-module/gatekeeper/rubric`
- `GET /api/ai-module/gatekeeper/ground-truth`

## Output chuan hoa

Gatekeeper verdict hien tai co cac field chinh:

- `isSupported`
- `verdict`
- `primaryDomain`
- `matchedSubjects`
- `confidence`
- `reason`
- `rejectionReasonCode`
- `detectedTopics`
- `modelName`

## Ghi chu nghiep vu

Gatekeeper chi nen lam dung 1 viec:

- xac dinh co thuoc whitelist hay khong

No khong nen:

- OCR sau
- chunking
- embedding
- sinh metadata qua sau

Neu content qua ngan, rong, hoac qua nhieu nhieu loai mon hoc tron lan:

- nen tra `ambiguous`

de tranh classify sai.
