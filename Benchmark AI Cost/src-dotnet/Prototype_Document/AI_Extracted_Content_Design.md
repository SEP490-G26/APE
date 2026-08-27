# AI Extracted Content Design

## Muc tieu

`AI Extracted Content` co vai tro:

- nhan noi dung tai lieu da duoc dua vao module
- chuyen noi dung thanh markdown sach de dung cho chunking va embedding
- neu co image marker va co vision model, mo rong noi dung anh thanh markdown

## Scope hien tai

Prototype hien tai xu ly 2 mode:

- `TextOnly`
- `FullMultimodalPage`

Scope runtime hien tai:

- co route upload ingest: `POST /api/ai-module/uploads/ingest`
- `docx`: doc text tu `word/document.xml`, boc media tu `word/media`
- `pptx`: doc text tu `ppt/slides/*.xml`, boc media tu `ppt/media`
- `pdf`: doc text bang parser that `PdfPig`
- `image`: luu file va tra marker de vision flow xu ly

No van chua la OCR/parser production-final, nhung da duoc nang cap thanh parser prototype thuc dung cho production test harness.

## Policy runtime

Policy hien tai duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/extracted-content-policy.json`

Policy chua:

- supported modes
- image markers
- normalization rules
- warning rules

## Rubric va ground truth

Rubric nhe:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EXTRACTED_CONTENT.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EXTRACTED_CONTENT_CORE.json`

## Route da co

- `POST /api/ai-module/extracted-content`
- `POST /api/ai-module/extracted-content/debug`
- `GET /api/ai-module/extracted-content/policy`
- `GET /api/ai-module/extracted-content/rubric`
- `GET /api/ai-module/extracted-content/ground-truth`

## Output chuan hoa

Output extraction hien tai gom:

- `rawText`
- `normalizedMarkdown`
- `wordCount`
- `embeddedImageCount`
- `parserName`
- `visionModelName`
- `extractionMode`
- `detectedImageReferences`
- `warnings`

`uploads/ingest` con tra them:

- `savedPath`
- `suggestedMode`
- `suggestedRawContent`
- `parserVersion`
- `extractedImagePaths`
- `canUseAsText`
- `canUseAsVisionInput`

## Warning logic

He thong co the canh bao:

- input rong
- co image marker nhung khong co vision model
- sau normalize khong con du text huu ich

## Ghi chu nghiep vu

Chuc nang nay chi nen lam:

- chuyen noi dung ve dang markdown su dung duoc
- bao ro warning neu chat luong input kem

No khong nen kiem nhiem:

- whitelist classification
- embedding
- tagging
- question generation
