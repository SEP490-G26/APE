# AI Module Codebase Alignment

## Ket luan

Voi structure hien tai cua `APE_BE`, module AI khong nen ton tai nhu mot solution tach biet khi dua vao codebase that.
No nen duoc to chuc thanh mot khoi feature `AI Module` trai deu qua 4 layer san co:

- `API`
- `Application`
- `Domain`
- `Infrastructure`

## Cach dat module de hop voi codebase

### API

- `API/Controllers/AIModuleController.cs`

Controller nay la diem vao cho cac luong test va orchestration:

- `POST /api/ai-module/ingestion`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/code-mentor`
- `POST /api/ai-module/full-pipeline`

Ly do gom mot controller:

- de nhin ro day la mot feature module
- de de gan middleware, auth, rate limiting, usage logging theo cum AI
- de sau nay tach ra thanh `DocumentController` va `QuestionController` neu can

### Application

- `Application/DTOs/AI/*`
- `Application/Interfaces/AI/*`
- `Application/Services/AI/*`

Day moi la tam diem cua module.

### Domain

- `Domain/Entities/AI/*`

Dat cac model nghiep vu lien quan den orchestration, log cost, chunk, generation, review, mentor vao nhom AI.

### Infrastructure

- `Infrastructure/AI/*`
- `Infrastructure/Files/*`
- `Infrastructure/Persistence/*`

Tach ro:

- provider AI client / adapter
- file parsing
- persistence

## Mapping voi APE_BE that

Module demo `src-dotnet` dang duoc can chinh de khi dua vao `APE_BE` co the map nhu sau:

- `Ape.AiModule.Domain/Entities/AI` -> `Domain/Entities/AI*`
- `Ape.AiModule.Application/DTOs/AI` -> `Application/DTOs/AIDtos.cs` hoac folder `DTOs/AI`
- `Ape.AiModule.Application/Interfaces/AI` -> `Application/Interfaces/*`
- `Ape.AiModule.Application/Services/AI` -> `Application/Services/*`
- `Ape.AiModule.Infrastructure/AI` -> `Infrastructure/AI/*`
- `Ape.AiModule.Infrastructure/Files` -> `Application/Services/FileExtractionService.cs` + infrastructure phu tro
- `Ape.AiModule.Infrastructure/Persistence` -> `Infrastructure/Persistence/*`

## Khuyen nghi tich hop that

Khi bat dau ghep vao `APE_BE`, nen bo solution `Ape.AiModule.*` va chuyen logic theo feature vao cac project san co thay vi giu 4 project moi.

Noi cach khac:

- hien tai `src-dotnet` la sandbox de khoa kien truc
- khi ap dung that, can "flatten into existing layers", khong "mount another mini-clean-architecture inside clean-architecture"

Neu khong lam vay, codebase se bi dup layer va kho maintain.
