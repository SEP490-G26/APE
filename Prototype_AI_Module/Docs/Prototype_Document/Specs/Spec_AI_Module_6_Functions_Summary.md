# AI Module 6 Functions Summary

## Mục tiêu

Tài liệu này tổng hợp toàn bộ 6 chức năng AI đã được mô hình hoá trong prototype `.NET` để:

- benchmark cost
- test output
- so sánh model
- làm module neo để ghép vào BE thật sau này

6 chức năng gồm:

1. `AI Gatekeeper`
2. `AI Extracted Content`
3. `AI Embedding + Auto Tagging`
4. `AI Question Generation`
5. `AI Review Result of AI Question Generation`
6. `AI Code Mentor`

Lưu ý:

- trong flow production mới, `AI Extracted Content` và `AI Embedding + Auto Tagging` vẫn là 2 năng lực AI tách biệt
- nhưng ở mức nghiệp vụ tài liệu, chúng phải đi qua 2 collection trung gian:
  - `DocumentExtractionDrafts`
  - `DocumentExtractionDraftContents`
  để hỗ trợ `human in the loop` và BYOS pricing

## Kiến trúc tổng quát

Pipeline tổng quát hiện tại:

`Input file -> Gatekeeper -> Extracted Content -> Embedding + Auto Tagging -> Question Generation -> Question Review -> Code Mentor / các flow khác`

Mục tiêu của prototype:

- JSON-first
- DB-ready
- provider-flexible
- có prompt, policy, rubric, ground truth theo từng function

## 1. AI Gatekeeper

### Vai trò

- đọc nhanh nội dung tài liệu
- xác định tài liệu có thuộc whitelist môn học được hệ thống hỗ trợ hay không

### Whitelist hiện tại

- `C`
- `JAVA_OOP`
- `DSA_JAVA`

### Output chuẩn hoá

- `isSupported`
- `verdict`
- `primaryDomain`
- `matchedSubjects`
- `confidence`
- `reason`
- `rejectionReasonCode`
- `detectedTopics`
- `modelName`

### Route

- `POST /api/ai-module/gatekeeper`
- `POST /api/ai-module/gatekeeper/debug`
- `GET /api/ai-module/gatekeeper/policy`
- `GET /api/ai-module/gatekeeper/rubric`
- `GET /api/ai-module/gatekeeper/ground-truth`

### File liên quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/gatekeeper-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/GATEKEEPER.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/GATEKEEPER_CORE.json`
- note: `Docs/Prototype_Document/Specs/Spec_AI_Gatekeeper_Whitelist_Design.md`

### Trạng thái

- đã có precheck rule-based
- đã có whitelist policy
- đã có debug route
- đã đủ benchmark và ghép BE

## 2. AI Extracted Content

### Vai trò

- nhận nội dung tài liệu đầu vào
- chuyển nội dung thành markdown sạch
- nếu có image marker và có vision model thì mở rộng phần ảnh thành markdown

### Mode hiện tại

- `TextOnly`
- `FullMultimodalPage`

### Output chuẩn hoá

- `rawText`
- `normalizedMarkdown`
- `wordCount`
- `embeddedImageCount`
- `parserName`
- `visionModelName`
- `extractionMode`
- `detectedImageReferences`
- `warnings`

### Route

- `POST /api/ai-module/extracted-content`
- `POST /api/ai-module/extracted-content/debug`
- `GET /api/ai-module/extracted-content/policy`
- `GET /api/ai-module/extracted-content/rubric`
- `GET /api/ai-module/extracted-content/ground-truth`

### File liên quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/extracted-content-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EXTRACTED_CONTENT.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EXTRACTED_CONTENT_CORE.json`
- note: `Docs/Prototype_Document/Specs/Spec_AI_Extracted_Content_Design.md`

### Trạng thái

- đã có warning system
- đã có debug route
- đã đủ cho benchmark và pipeline prototype
- đã có route upload ingest cho `pdf/docx/pptx/image`
- `pdf` đã dùng parser `PdfPig`
- `docx/pptx` đã bóc được media từ package và chèn image marker
- chưa phải parser/OCR production-final
- production nên lưu output vào:
  - `DocumentExtractionDrafts` ở cấp tài liệu
  - `DocumentExtractionDraftContents` ở cấp segment
  trước khi sang embedding

## 3. AI Embedding + Auto Tagging

### Vai trò

- nhận nội dung đã extract
- chia chunk
- embedding từng chunk
- gắn topic tags cho từng chunk
- trong production nên chỉ chạy trên `approved_markdown` đã qua human review ở `DocumentExtractionDraftContents`

### Contract `KnowledgeChunk`

Đã được chuẩn hoá theo schema DB-ready:

- `Id`
- `CourseId`
- `DocumentId`
- `UserId`
- `ChunkIndex`
- `ChunkingStrategy`
- `ChunkType`
- `SectionTitle`
- `SourceType`
- `SourceName`
- `SourcePageFrom`
- `SourcePageTo`
- `Language`
- `SubjectCode`
- `RawText`
- `NormalizedText`
- `MarkdownText`
- `TopicTags`
- `Embedding`
- `EmbeddingProvider`
- `EmbeddingModel`
- `EmbeddingDim`
- `TaggingProvider`
- `TaggingModel`
- `WordCount`
- `TokenCount`
- `CharCount`
- `RetrievalEnabled`
- `Status`
- `CreatedAt`
- `UpdatedAt`

### Kết quả trả về

- `chunks`
- `embeddingTotals`
- `taggingTotals`
- `stageLogs`
- `usageLogs`
- `totals`

### Route

- `POST /api/ai-module/embedding-tagging`
- `POST /api/ai-module/embedding-tagging/debug`
- `GET /api/ai-module/embedding-tagging/policy`
- `GET /api/ai-module/embedding-tagging/rubric`
- `GET /api/ai-module/embedding-tagging/ground-truth`

### File liên quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/embedding-tagging-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EMBEDDING_TAGGING.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EMBEDDING_TAGGING_CORE.json`
- note: `Docs/Prototype_Document/Specs/Spec_AI_Embedding_AutoTagging_Design.md`

### Trạng thái

- đã support embedding model và tagging model tách riêng
- đã có taxonomy policy
- đã có debug route
- đã đủ để benchmark cost và xem xét chất lượng tagging
- production nên nhận input từ `DocumentExtractionDraftContents` thay vì nhận file thô trực tiếp

## 4. AI Question Generation

### Vai trò

- nhận `KnowledgeChunks`
- tạo câu hỏi `FE` hoặc `PE`
- ép output bám sát schema question bank

### Output FE

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `options`
- `correct_answer`
- `explanation`

### Output PE

- `type`
- `topic_tags`
- `difficulty`
- `title`
- `description`
- `source_chunk_ids`
- `skeleton_code`
- `solution_code`
- `test_cases`

### Route

- `POST /api/ai-module/question-generation`
- `POST /api/ai-module/question-generation/debug`
- `GET /api/ai-module/question-generation/rubric`
- `GET /api/ai-module/question-generation/ground-truth`
- `POST /api/ai-module/question-generation/regression`

### File liên quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/*.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/QGEN_*.json`
- note:
  - `Docs/Prototype_Document/DB_Resources/AI_Generation_Review_Pipeline.md`
  - `Docs/Prototype_Document/DB_Resources/AI_Generation_Review_Export.md`
  - `Docs/Prototype_Document/Specs/Spec_Question_Generation_Review_FE_PE_Summary.md`

### Trạng thái

- đã có normalize output
- đã có schema validation
- đã có DB-ready mapping FE/PE
- đã có duplicate check mức cơ bản
- đã có difficulty alignment
- `question-generation/debug` hiện đang là route placeholder, trả cùng payload runtime như route thường

## 5. AI Review Result of AI Question Generation

### Vai trò

- đọc kết quả của AI generator
- đối chiếu với chunk context
- đối chiếu với rubric
- accept hoặc reject

### Mode

- `SingleAgent`
- `SameModelDualRole`
- `DualAgent`

### Luồng

- AI1 tạo đề
- AI2 review
- nếu fail thì AI1 được tạo lại tối đa 1 lần nữa

### Route

- `POST /api/ai-module/question-review`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/generation-review/debug`
- `POST /api/ai-module/generation-review/export`

### Export package

Đã có gói JSON chuẩn hoá gồm:

- `metadata`
- `request`
- `rubric`
- `questions`
- `schemaMapping`
- `difficultyAlignment`
- `mappedFeQuestions`
- `mappedPeQuestions`
- `review`
- `attemptsUsed`
- `stageLogs`
- `usageLogs`
- `totals`

### Trạng thái

- đã có rubric runtime theo môn + FE/PE
- đã có ground truth starter
- đã có regression runner
- đã có retrieval planner + context pack runtime cho mode production-test-harness
- đã đủ để benchmark và ghép với BE tổng
- `generation-review/debug` hiện đang là route placeholder, trả cùng payload runtime như route thường

### Flow khuyến nghị hiện tại

Nếu benchmark nhanh:

- `Embedding + Auto Tagging`
- export / import `chunks`
- `generation-review`

Nếu benchmark gần production:

- `Embedding + Auto Tagging`
- `retrieval/plan`
- lấy `contextPackId`
- `generation-review` với `contextPackId`

Nếu muốn so model công bằng:

- dùng cùng một `contextPackId`
- đổi `generatorModel` và/hoặc `reviewerModel`
- như vậy input ngữ cảnh được giữ nguyên

## 6. AI Code Mentor

### Vai trò

- nhận đề bài + code sinh viên nộp
- phân tích lỗi có thể suy ra từ code
- trả về feedback có cấu trúc

### Scope hiện tại

- không compile/run code
- không giả vờ đã chạy code
- phân tích dựa trên problem + code + rubric/policy

Judge0 sẽ là phần nâng cấp sau khi ghép production.

### Output chuẩn hoá

- `verdict`
- `issueCategories`
- `issues`
- `suggestions`
- `failingScenarios`
- `confidence`
- `complexity`
- `modelName`

### Route

- `POST /api/ai-module/code-mentor`
- `POST /api/ai-module/code-mentor/debug`
- `GET /api/ai-module/code-mentor/policy`
- `GET /api/ai-module/code-mentor/rubric`
- `GET /api/ai-module/code-mentor/ground-truth`

### File liên quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/code-mentor-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/CODE_MENTOR.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/CODE_MENTOR_CORE.json`
- note: `Docs/Prototype_Document/Specs/Spec_AI_Code_Mentor_Design.md`

### Trạng thái

- đã có policy/rubric/ground truth riêng
- đã có debug route
- đã đủ benchmark và test feedback structure
- chưa có judge/runtime integration

## Config và quản lý prompt/policy/rubric/ground truth

### Prompt

Prompt hiện tại là file-backed:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- `src-dotnet/Ape.AiModule.Api/App_Data/ai-config-history/prompts/`

Route prompt đã có:

- `GET /api/ai-module/prompts`
- `GET /api/ai-module/prompts/{key}`
- `GET /api/ai-module/prompts/{key}/history`
- `POST /api/ai-module/prompts/reload`
- `PUT /api/ai-module/prompts/{key}`
- `POST /api/ai-module/prompts/render`

Nguyên tắc hiện tại của prototype:

- `ai-prompts.json` = current active snapshot
- `ai-config-history/prompts/` = immutable append-history
- policy, rubric, ground truth cũng đi theo cùng nguyên tắc dưới `App_Data/ai-config-history/`

### Các collection DB-ready đã chốt theo hướng versioned snapshot

- `AI_Prompt_Versions`
- `AI_Policy_Versions`
- `AI_Rubric_Versions`
- `AI_GroundTruth_Sets`

Tài liệu liên quan:

- `Docs/Prototype_Document/DB_Resources/AI_Configuration_Collections.md`
- `Docs/Prototype_Document/DB_Resources/AI_Policies.md`
- `Docs/Prototype_Document/DB_Resources/AI_Rubrics.md`
- `Docs/Prototype_Document/DB_Resources/AI_GroundTruth_Sets.md`

### Lưu ý quan trọng về `AI_Policies`

Module prototype hiện tại dùng file policy trong `App_Data` làm source of truth runtime.

Nhưng khi đưa vào DB thật, đã chốt theo hướng:

- file prototype = payload runtime
- DB = wrapper record `AI_Policies` + `content_json`

Tài liệu đối chiếu chi tiết:

- `Docs/Prototype_Document/Guides/Guide_App_Data_vs_DB_Mapping.md`

## Log và benchmark

Prototype đã có:

- `stageLogs`
- `usageLogs`
- `TokenCostBreakdown`
- `NormalizedModelFields`
- `NormalizedError`
- `UsageCapture`

Giúp:

- tính token in/out
- tính cost theo stage
- lưu payload debug
- so sánh model
- phân biệt `raw` usage và `estimated` usage
- chuẩn hoá lỗi provider để so sánh benchmark / production test
- tổng hợp session benchmark và export TSV/JSON

Run history summary hiện tại còn có:

- `modelFields`
- `usageSource`
- `error`
- `totals`

Tài liệu liên quan:

- `Docs/Prototype_Document/Guides/Guide_AI_Pipeline_Log_Storage.md`
- `Docs/Prototype_Document/Guides/Guide_AI_Usage_Log_Pipeline.md`
- `Docs/Prototype_Document/Guides/Guide_AI_Module_Run.md`

## Trạng thái module hiện tại

Module AI prototype hiện tại đã đạt mức:

- có 6 AI function
- có prompt file-backed
- có policy/rubric/ground truth theo function
- có route debug
- có output JSON chuẩn hoá
- có log token/cost
- có `usage_source` (`raw` / `estimated` / `mixed`)
- có `normalized error catalog` ở mức runtime
- có benchmark session save/open
- có parser PDF thật cho production test harness
- có retrieval planner và file-backed context pack store
- có khả năng ghép vào BE tổng sau này

## Đánh giá prototype testing/production hiện tại

Ở mức prototype test harness, module đã ổn cho:

- benchmark cost
- benchmark token
- so sánh model
- test pipeline nghiệp vụ chính
- giữ bằng chứng JSON/TSV/history

Nhưng chưa nên xem là production-final vì:

- `question-generation/debug` và `generation-review/debug` chưa có debug payload riêng
- `full-pipeline` hiện gồm `ingestion + generation/review`, chưa chạy `code mentor`
- repository vẫn là in-memory/file-backed
- parser `docx/pptx/pdf` đã dùng được cho prototype, nhưng chưa đạt mức semantic parser/OCR cuối cùng

## Những gì còn lại khi ghép production

1. Mongo repository thật
2. Judge0 compile/run cho `PE` và `Code Mentor`
3. provider price catalog production-final
4. vector duplicate detection thật
5. parser/OCR production-final cho tài liệu phức tạp hơn nữa
6. lưu prompt/policy/rubric/ground truth từ DB thay vì file
7. gắn version refs của config vào run history / usage log production

## Kết luận

Tính đến hiện tại, prototype đã không còn là bộ benchmark thử nghiệm đơn lẻ nữa, mà đã trở thành:

- một `AI core module`
- có thể benchmark
- có thể test
- có thể dùng làm nền tảng để ghép vào hệ thống thật
