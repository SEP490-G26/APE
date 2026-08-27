# AI Module 6 Functions Summary

## Muc tieu

Tai lieu nay tong hop toan bo 6 chuc nang AI da duoc mo hinh hoa trong prototype `.NET` de:

- benchmark cost
- test output
- so sanh model
- lam module neo de ghep vao BE that sau nay

6 chuc nang gom:

1. `AI Gatekeeper`
2. `AI Extracted Content`
3. `AI Embedding + Auto Tagging`
4. `AI Question Generation`
5. `AI Review Result of AI Question Generation`
6. `AI Code Mentor`

## Kien truc tong quat

Pipeline tong quat hien tai:

`Input file -> Gatekeeper -> Extracted Content -> Embedding + Auto Tagging -> Question Generation -> Question Review -> Code Mentor / cac flow khac`

Muc tieu cua prototype:

- JSON-first
- DB-ready
- provider-flexible
- co prompt, policy, rubric, ground truth theo tung function

## 1. AI Gatekeeper

### Vai tro

- doc nhanh noi dung tai lieu
- xac dinh tai lieu co thuoc whitelist mon hoc duoc he thong ho tro hay khong

### Whitelist hien tai

- `C`
- `JAVA_OOP`
- `DSA_JAVA`

### Output chuan hoa

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

### File lien quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/gatekeeper-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/GATEKEEPER.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/GATEKEEPER_CORE.json`
- note: `Docs/AI_Gatekeeper_Whitelist_Design.md`

### Trang thai

- da co precheck rule-based
- da co whitelist policy
- da co debug route
- da du benchmark va ghep BE

## 2. AI Extracted Content

### Vai tro

- nhan noi dung tai lieu dau vao
- chuyen noi dung thanh markdown sach
- neu co image marker va co vision model thi mo rong phan anh thanh markdown

### Mode hien tai

- `TextOnly`
- `FullMultimodalPage`

### Output chuan hoa

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

### File lien quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/extracted-content-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EXTRACTED_CONTENT.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EXTRACTED_CONTENT_CORE.json`
- note: `Docs/AI_Extracted_Content_Design.md`

### Trang thai

- da co warning system
- da co debug route
- da du cho benchmark va pipeline prototype
- da co route upload ingest cho `pdf/docx/pptx/image`
- `pdf` da dung parser `PdfPig`
- `docx/pptx` da boc duoc media tu package va chen image marker
- chua phai parser/OCR production-final

## 3. AI Embedding + Auto Tagging

### Vai tro

- nhan noi dung da extract
- chia chunk
- embedding tung chunk
- gan topic tags cho tung chunk

### Contract `KnowledgeChunk`

Da duoc chuan hoa theo schema DB-ready:

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

### Ket qua tra ve

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

### File lien quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/embedding-tagging-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EMBEDDING_TAGGING.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EMBEDDING_TAGGING_CORE.json`
- note: `Docs/AI_Embedding_AutoTagging_Design.md`

### Trang thai

- da support embedding model va tagging model tach rieng
- da co taxonomy policy
- da co debug route
- da du de benchmark cost va xem xet chat luong tagging

## 4. AI Question Generation

### Vai tro

- nhan `KnowledgeChunks`
- tao cau hoi `FE` hoac `PE`
- ep output bam sat schema question bank

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

### File lien quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/*.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/QGEN_*.json`
- note:
  - `Docs/DB resource/AI_Generation_Review_Pipeline.md`
  - `Docs/DB resource/AI_Generation_Review_Export.md`
  - `Docs/Question_Generation_Review_FE_PE_Summary.md`

### Trang thai

- da co normalize output
- da co schema validation
- da co DB-ready mapping FE/PE
- da co duplicate check muc co ban
- da co difficulty alignment
- `question-generation/debug` hien dang la route placeholder, tra cung payload runtime nhu route thuong

## 5. AI Review Result of AI Question Generation

### Vai tro

- doc ket qua cua AI generator
- doi chieu voi chunk context
- doi chieu voi rubric
- accept hoac reject

### Mode

- `SingleAgent`
- `SameModelDualRole`
- `DualAgent`

### Luong

- AI1 tao de
- AI2 review
- neu fail thi AI1 duoc tao lai toi da 1 lan nua

### Route

- `POST /api/ai-module/question-review`
- `POST /api/ai-module/generation-review`
- `POST /api/ai-module/generation-review/debug`
- `POST /api/ai-module/generation-review/export`

### Export package

Da co goi JSON chuan hoa gom:

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

### Trang thai

- da co rubric runtime theo mon + FE/PE
- da co ground truth starter
- da co regression runner
- da du de benchmark va ghep voi BE tong
- `generation-review/debug` hien dang la route placeholder, tra cung payload runtime nhu route thuong

## 6. AI Code Mentor

### Vai tro

- nhan de bai + code sinh vien nop
- phan tich loi co the suy ra tu code
- tra ve feedback co cau truc

### Scope hien tai

- khong compile/run code
- khong gia vo da chay code
- phan tich dua tren problem + code + rubric/policy

Judge0 se la phan nang cap sau khi ghep production.

### Output chuan hoa

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

### File lien quan

- prompt: `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`
- policy: `src-dotnet/Ape.AiModule.Api/App_Data/code-mentor-policy.json`
- rubric: `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/CODE_MENTOR.json`
- ground truth: `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/CODE_MENTOR_CORE.json`
- note: `Docs/AI_Code_Mentor_Design.md`

### Trang thai

- da co policy/rubric/ground truth rieng
- da co debug route
- da du benchmark va test feedback structure
- chua co judge/runtime integration

## Config va quan ly prompt/policy/rubric/ground truth

### Prompt

Prompt hien tai la file-backed:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

Route prompt da co:

- `GET /api/ai-module/prompts`
- `GET /api/ai-module/prompts/{key}`
- `POST /api/ai-module/prompts/reload`
- `PUT /api/ai-module/prompts/{key}`
- `POST /api/ai-module/prompts/render`

### Cac collection DB-ready da chot

- `AI_Prompt_Versions`
- `AI_Policies`
- `AI_Rubrics`
- `AI_GroundTruth_Sets`

Tai lieu lien quan:

- `Docs/DB resource/AI_Configuration_Collections.md`
- `Docs/DB resource/AI_Policies.md`
- `Docs/DB resource/AI_Rubrics.md`
- `Docs/DB resource/AI_GroundTruth_Sets.md`

### Luu y quan trong ve `AI_Policies`

Module prototype hien tai dung file policy trong `App_Data` lam source of truth runtime.

Nhung khi dua vao DB that, da chot theo huong:

- file prototype = payload runtime
- DB = wrapper record `AI_Policies` + `content_json`

Tai lieu doi chieu chi tiet:

- `Docs/App_Data_Source_Of_Truth_vs_DB_Mapping.md`

## Log va benchmark

Prototype da co:

- `stageLogs`
- `usageLogs`
- `TokenCostBreakdown`
- `NormalizedModelFields`
- `NormalizedError`
- `UsageCapture`

Giup:

- tinh token in/out
- tinh cost theo stage
- luu payload debug
- so sanh model
- phan biet `raw` usage va `estimated` usage
- chuan hoa loi provider de so sanh benchmark / production test
- tong hop session benchmark va export TSV/JSON

Run history summary hien tai con co:

- `modelFields`
- `usageSource`
- `error`
- `totals`

Tai lieu lien quan:

- `Docs/AI_Pipeline_Log_Storage.md`
- `Docs/AI_Usage_Log_Pipeline.md`
- `Docs/AI_Module_Run_Guide.md`

## Trang thai module hien tai

Module AI prototype hien tai da dat muc:

- co 6 AI function
- co prompt file-backed
- co policy/rubric/ground truth theo function
- co route debug
- co output JSON chuan hoa
- co log token/cost
- co `usage_source` (`raw` / `estimated` / `mixed`)
- co `normalized error catalog` o muc runtime
- co benchmark session save/open
- co parser PDF that cho production test harness
- co kha nang ghep vao BE tong sau nay

## Danh gia prototype testing/production hien tai

O muc prototype test harness, module da on cho:

- benchmark cost
- benchmark token
- so sanh model
- test pipeline nghiep vu chinh
- giu bang chung JSON/TSV/history

Nhung chua nen xem la production-final vi:

- `question-generation/debug` va `generation-review/debug` chua co debug payload rieng
- `full-pipeline` hien gom `ingestion + generation/review`, chua chay `code mentor`
- repository van la in-memory/file-backed
- parser `docx/pptx/pdf` da dung duoc cho prototype, nhung chua dat muc semantic parser/OCR cuoi cung

## Nhung gi con lai khi ghep production

1. Mongo repository that
2. Judge0 compile/run cho `PE` va `Code Mentor`
3. provider price catalog production-final
4. vector duplicate detection that
5. parser/OCR production-final cho tai lieu phuc tap hon nua
6. luu prompt/rubric/ground truth tu DB thay vi file

## Ket luan

Tinh den hien tai, prototype da khong con la bo benchmark thu nghiem don le nua, ma da tro thanh:

- mot `AI core module`
- co the benchmark
- co the test
- co the dung lam nen tang de ghep vao he thong that
