# AI Module .NET Architecture

## Muc tieu

Module `.NET` nay la phan core co the dua vao he thong chinh sau khi benchmark xong. No dong vai tro:

- chay pipeline AI theo dung flow nghiep vu
- ghi nhan ket qua de test, so sanh va tinh chi phi
- giu cau truc sach de sau nay noi MongoDB va provider that

## Cau truc project

- `Ape.AiModule.Domain`
  - model va contract nghiep vu cot loi
- `Ape.AiModule.Application`
  - request model, interface va orchestrator use-case
- `Ape.AiModule.Infrastructure`
  - parser, chunking, AI gateway, repository implementation
- `Ape.AiModule.Api`
  - HTTP host va FE test don gian

## Pipeline nghiep vu

### 1. Ingestion

Flow:

1. nhan tai lieu dau vao
2. gatekeeper xac dinh tai lieu co nam trong pham vi ho tro khong
3. parser dua tai lieu ve text/markdown chuan hoa
4. chunking chia noi dung thanh khoi logic
5. embedding tao vector
6. auto tagging gan topic tag cho chunk
7. luu document va chunk vao repository

Output:

- `StoredDocument`
- `KnowledgeChunk[]`
- stage logs va token/cost summary

### 2. Generation + Review

Flow:

1. tai document da ingestion
2. lay mot tap chunk lam context
3. generation tao cau hoi FE hoac PE
4. review kiem tra ket qua generation
5. luu ket qua

Output:

- `GeneratedQuestion[]`
- `ReviewDecision`
- stage logs va cost

### 3. Code Mentor

Flow:

1. nhan de bai + bai nop
2. mentor agent phan tich loi, goi y sua, uoc luong do phuc tap
3. luu feedback

## Nguyen tac de dua vao he thong that

- khong hard-code provider trong application layer
- repository phai thay duoc bang Mongo adapter
- moi stage phai giu `token`, `cost`, `latency`, `metadata`
- FE chi la cong cu test, khong rang buoc core module

## Buoc tiep theo de dua vao production

1. thay `InMemoryModuleRepository` bang Mongo repository
2. thay `DemoAiProviderGateway` bang adapter that cho OpenAI, Gemini, Cohere
3. thay `DemoDocumentParser` bang parser tai lieu that
4. map `KnowledgeChunk` va `StoredDocument` vao schema chinh thuc
5. them prompt registry va model registry trong persistence

## API hien tai

- `POST /api/ai-module/ingestion`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/code-mentor`
- `POST /api/ai-module/full-pipeline`

## Luu y

Ban hien tai la vertical slice demo chay duoc end-to-end. No duoc tao de:

- khoa kien truc
- test ket noi pipeline
- lam moc de noi Mongo va provider that

Chua phai ban production provider.
