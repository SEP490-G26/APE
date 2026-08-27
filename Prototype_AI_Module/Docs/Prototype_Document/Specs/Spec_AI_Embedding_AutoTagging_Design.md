# AI Embedding + Auto Tagging Design

## Mục tiêu

`AI Embedding + Auto Tagging` có vai trò:

- nhận nội dung đã được parser/extract
- chia thành các chunk có cấu trúc
- tạo vector embedding cho từng chunk
- gắn topic tags cho từng chunk để phục vụ retrieval, generation và debug
- chuẩn bị metadata đầy đủ để retrieval planner và context packer có thể hoạt động hiệu quả ở production

## Contract chunk đã chốt

`KnowledgeChunk` trong prototype cần được hiểu là một bản ghi tri thức phục vụ cả 3 mục tiêu:

- trace nguồn tài liệu
- retrieval/search
- generation/review grounding

Các nhóm field quan trọng:

### 1. Identity và trace

- `id`
- `courseId`
- `documentId`
- `userId`
- `extractionDraftId`
- `extractionContentId`
- `chunkIndex`

### 2. Cấu trúc tri thức

- `chunkingStrategy`
- `chunkType`
- `sectionTitle`
- `chapterId`
- `chapterTitle`
- `chunkPath`

### 3. Source metadata

- `sourceType`
- `sourceName`
- `sourcePageFrom`
- `sourcePageTo`
- `sourceBlockRefs`

### 4. Nội dung đã chuẩn hoá

- `rawText`
- `normalizedText`
- `markdownText`
- `chunkSummary`
- `conceptKeywords`

### 5. Tag / retrieval semantics

- `topicTags`
- `topicPrimary`
- `topicSecondary`
- `prerequisiteTags`
- `estimatedDifficulty`
- `assessmentValueScore`
- `contentQualityScore`
- `retrievalScoreBoost`
- `duplicateGroupId`

### 6. Embedding / tagging metadata

- `embedding`
- `embeddingProvider`
- `embeddingModel`
- `embeddingDim`
- `taggingProvider`
- `taggingModel`

### 7. Kích thước / cost

- `wordCount`
- `tokenCount`
- `charCount`

### 8. Runtime state

- `retrievalEnabled`
- `status`
- `createdAt`
- `updatedAt`

Chi tiết DB-ready đầy đủ xem tại:

- `Docs/Prototype_Document/DB_Resources/KnowledgeChunks.md`

## Tại sao phải mở rộng KnowledgeChunks

Nếu `KnowledgeChunks` chỉ lưu:

- text
- embedding
- topic tags

thì hệ thống sẽ thiếu dữ liệu để tối ưu retrieval production.

Khi đó question generation rất dễ phải đọc quá nhiều raw chunks, làm:

- tăng input token
- tăng latency
- tăng chi phí mỗi request
- giảm chất lượng grounding do context loãng

Vì vậy, các field như `chunkSummary`, `topicPrimary`, `assessmentValueScore`, `duplicateGroupId`, `chapterId` là cần thiết ở production.

## Handoff sang retrieval planner

Sau `Embedding + Auto Tagging`, output không nên được hiểu chỉ là "xong embedding".

Thực tế đây là tầng chuẩn bị knowledge base cho bước sau:

`Extracted Content -> Embedding + Auto Tagging -> Retrieval Planner -> Context Packing -> Question Generation`

### Handoff tối thiểu cần có

Mỗi chunk cần đủ dữ liệu để planner có thể:

- lọc theo môn/chapter/topic
- đánh giá chunk nào đáng dùng để tạo đề
- loại chunk trùng hoặc chunk rác
- pack context theo token budget

## Policy runtime

Policy hiện tại được lưu tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/embedding-tagging-policy.json`

Policy nên tiếp tục chứa:

- chunking defaults
- retrieval/status defaults
- tagging rules
- subject taxonomy

Về sau nên mở rộng thêm nhóm policy cho retrieval-oriented metadata, ví dụ:

- ngưỡng token tối đa mỗi chunk
- rule đánh dấu `retrieval_enabled = false`
- rule đánh dấu `assessment_value_score` thấp cho chunk rác
- heuristic nhận diện duplicate chunk

## Rubric và ground truth

Rubric nhẹ:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EMBEDDING_TAGGING.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EMBEDDING_TAGGING_CORE.json`

Về sau nên bổ sung thêm ground truth để đo:

- tag precision
- retrieval usefulness
- chunk quality usefulness cho generation

## Prompt

Auto tagging prompt hiện tại được lưu tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

Đã nâng cấp để:

- nhận taxonomy json
- chỉ được dùng allowed tags
- ưu tiên ít tag nhưng đúng
- không invent tag

Về sau có thể thêm một prompt hoặc rule stage phụ để sinh:

- `chunk_summary`
- `concept_keywords`
- `topic_primary`

nếu muốn AI hỗ trợ nhiều hơn cho retrieval planner.

## Route đã có

- `POST /api/ai-module/embedding-tagging`
- `POST /api/ai-module/embedding-tagging/debug`
- `GET /api/ai-module/embedding-tagging/policy`
- `GET /api/ai-module/embedding-tagging/rubric`
- `GET /api/ai-module/embedding-tagging/ground-truth`

## Output kết quả

Result hiện tại đã tách rõ:

- `chunks`
- `embeddingTotals`
- `taggingTotals`
- `stageLogs`
- `usageLogs`
- `totals`

Debug result bổ sung:

- `policy`
- `rubric`
- `groundTruth`

## Hướng production tiếp theo

Sau khi đã có `KnowledgeChunks` tốt, hệ thống nên thêm tầng mới:

- retrieval planner
- re-ranker
- context pack builder
- `AI_Context_Packs`

Tài liệu đặc tả phần này xem tại:

- `Docs/Prototype_Document/Specs/Spec_AI_Retrieval_Context_Packing_Architecture.md`
- `Docs/Prototype_Document/DB_Resources/AI_Context_Packs.md`

## Ghi chú nghiệp vụ

Phần embedding và tagging hiện đã hỗ trợ:

- embedding model linh hoạt
- tagging model linh hoạt

Nghĩa là pipeline có thể dùng:

- 1 model embedding
- 1 model tagging khác

để benchmark chất lượng tagging giữa nhiều model khác nhau trên cùng 1 bộ chunk.

Nhưng ở production, giá trị lớn nhất của stage này là chuẩn bị một `KnowledgeChunks` đủ giàu metadata để downstream retrieval không bị đốt token vô ích.
