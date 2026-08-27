# AI Embedding + Auto Tagging Design

## Muc tieu

`AI Embedding + Auto Tagging` co vai tro:

- nhan noi dung da duoc parser/extract
- chia thanh cac chunk co cau truc
- tao vector embedding cho tung chunk
- gan topic tags cho tung chunk de phuc vu retrieval, generation va debug

## Contract chunk da chot

`KnowledgeChunk` trong prototype da duoc doi theo dung schema DB-ready da chot truoc do:

- `id`
- `courseId`
- `documentId`
- `userId`
- `chunkIndex`
- `chunkingStrategy`
- `chunkType`
- `sectionTitle`
- `sourceType`
- `sourceName`
- `sourcePageFrom`
- `sourcePageTo`
- `language`
- `subjectCode`
- `rawText`
- `normalizedText`
- `markdownText`
- `topicTags`
- `embedding`
- `embeddingProvider`
- `embeddingModel`
- `embeddingDim`
- `taggingProvider`
- `taggingModel`
- `wordCount`
- `tokenCount`
- `charCount`
- `retrievalEnabled`
- `status`
- `createdAt`
- `updatedAt`

## Policy runtime

Policy hien tai duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/embedding-tagging-policy.json`

Policy chua:

- chunking defaults
- retrieval/status defaults
- tagging rules
- subject taxonomy

## Rubric va ground truth

Rubric nhe:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EMBEDDING_TAGGING.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EMBEDDING_TAGGING_CORE.json`

## Prompt

Auto tagging prompt hien tai duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

Da nang cap de:

- nhan taxonomy json
- chi duoc dung allowed tags
- uu tien it tag nhung dung
- khong invent tag

## Route da co

- `POST /api/ai-module/embedding-tagging`
- `POST /api/ai-module/embedding-tagging/debug`
- `GET /api/ai-module/embedding-tagging/policy`
- `GET /api/ai-module/embedding-tagging/rubric`
- `GET /api/ai-module/embedding-tagging/ground-truth`

## Output ket qua

Result hien tai da tach ro:

- `chunks`
- `embeddingTotals`
- `taggingTotals`
- `stageLogs`
- `usageLogs`
- `totals`

Debug result bo sung:

- `policy`
- `rubric`
- `groundTruth`

## Ghi chu nghiep vu

Phan embedding va tagging hien da ho tro:

- embedding model linh hoat
- tagging model linh hoat

Nghia la pipeline co the dung:

- 1 model embedding
- 1 model tagging khac

de benchmark chat luong tagging giua nhieu model khac nhau tren cung 1 bo chunk.
