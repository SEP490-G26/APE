# AI Extracted Content Design

## Mục tiêu

`AI Extracted Content` có vai trò:

- nhận nội dung tài liệu đã được đưa vào module
- chuyển nội dung thành markdown sạch để dùng cho chunking và embedding
- nếu có image marker và có vision model, mở rộng nội dung ảnh thành markdown
- tạo ra bản nháp trung gian để human review trước khi sang embedding
- hỗ trợ BYOS bằng cách lưu trạng thái và chi phí xử lý để tránh gọi OCR/vision lặp lại

## Scope hiện tại

Prototype hiện tại xử lý 2 mode:

- `TextOnly`
- `FullMultimodalPage`

Scope runtime hiện tại:

- có route upload ingest: `POST /api/ai-module/uploads/ingest`
- `docx`: đọc text từ `word/document.xml`, bóc media từ `word/media`
- `pptx`: đọc text từ `ppt/slides/*.xml`, bóc media từ `ppt/media`
- `pdf`: đọc text bằng parser thật `PdfPig`
- `image`: lưu file và trả marker để vision flow xử lý

Nó vẫn chưa là OCR/parser production-final, nhưng đã được nâng cấp thành parser prototype thực dụng cho production test harness.

## Policy runtime

Policy hiện tại được lưu tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/extracted-content-policy.json`

Policy chứa:

- supported modes
- image markers
- normalization rules
- warning rules

## Rubric và ground truth

Rubric nhẹ:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/EXTRACTED_CONTENT.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/EXTRACTED_CONTENT_CORE.json`

## Route đã có

- `POST /api/ai-module/extracted-content`
- `POST /api/ai-module/extracted-content/debug`
- `GET /api/ai-module/extracted-content/policy`
- `GET /api/ai-module/extracted-content/rubric`
- `GET /api/ai-module/extracted-content/ground-truth`

## Output chuẩn hoá

Output extraction hiện tại gồm:

- `rawText`
- `normalizedMarkdown`
- `wordCount`
- `embeddedImageCount`
- `parserName`
- `visionModelName`
- `extractionMode`
- `detectedImageReferences`
- `warnings`

Trong production flow nên chuẩn hoá thêm các trường nghiệp vụ để lưu vào 2 collection:

- `DocumentExtractionDrafts`
- `DocumentExtractionDraftContents`

Nhóm trường cấp tài liệu:

- `cleanMarkdown`
- `approvedMarkdown`
- `reviewStatus`
- `reviewedBy`
- `reviewedAt`
- `visionExpansions`
- `sourceChecksum`
- `chunkingReady`

Nhóm trường cấp segment:

- `segmentIndex`
- `sourcePageFrom`
- `sourcePageTo`
- `rawText`
- `cleanMarkdown`
- `approvedMarkdown`
- `visionExpansions`
- `wordCount`
- `tokenCount`
- `extractionCostUsd`

`uploads/ingest` còn trả thêm:

- `savedPath`
- `suggestedMode`
- `suggestedRawContent`
- `parserVersion`
- `extractedImagePaths`
- `canUseAsText`
- `canUseAsVisionInput`

## Warning logic

Hệ thống có thể cảnh báo:

- input rỗng
- có image marker nhưng không có vision model
- sau normalize không còn đủ text hữu ích

## Ghi chú nghiệp vụ

Chức năng này chỉ nên làm:

- chuyển nội dung về dạng markdown sử dụng được
- báo rõ warning nếu chất lượng input kém
- dừng ở trạng thái `needs_review` để human kiểm tra và duyệt

### Collection nghiệp vụ đề nghị

Để phục vụ production và BYOS, bước này nên lưu vào:

- `DocumentExtractionDrafts`
- `DocumentExtractionDraftContents`

Schema chi tiết:

- `Docs/Prototype_Document/DB_Resources/DocumentExtractionDrafts.md`
- `Docs/Prototype_Document/DB_Resources/DocumentExtractionDraftContents.md`

### Ý nghĩa với BYOS và pricing

Với tài liệu cá nhân do sinh viên tải lên, hệ thống có thể tính credit dựa trên:

- độ lớn file
- số trang
- số từ ước lượng
- số ảnh cần vision
- token thực tế của extraction/vision
- chi phí USD thực tế hoặc estimate

Hai collection trên chính là nơi lưu snapshot để:

- tránh xử lý lại cùng một tài liệu
- audit tại sao một lần BYOS ingestion bị trừ từng ấy credit
- làm căn cứ set pricing theo độ lớn và độ phức tạp tài liệu

Nó không nên kiêm nhiệm:

- whitelist classification
- embedding
- tagging
- question generation
