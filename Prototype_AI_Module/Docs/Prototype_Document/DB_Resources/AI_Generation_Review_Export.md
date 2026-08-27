# AI Generation Review Export

## Mục tiêu

Tài liệu này mô tả gói JSON chuẩn hoá được trả về bởi route:

- `POST /api/ai-module/generation-review/export`

Nó dùng cho 4 mục đích:

1. benchmark chi phí
2. debug output generator + reviewer
3. export kết quả test để đưa vào báo cáo
4. làm contract tạm trước khi nối vào Mongo repository thật

## Payload metadata

`metadata` gồm:

- `packageId`: id của gói export
- `subject`
- `questionType`
- `requestedDifficulty`
- `mode`
- `generatorModel`
- `reviewerModel`
- `maxAttempts`
- `generatorPromptKey`
- `reviewerPromptKey`
- `rubricId`
- `rubricVersion`
- `groundTruthDatasetId`
- `exportedAt`

## Payload request snapshot

`request` là snapshot của input gốc:

- `userId`
- `courseId`
- `documentId`
- `isPublic`
- `subject`
- `difficulty`
- `questionType`
- `count`
- `mode`
- `generatorModel`
- `reviewerModel`
- `maxAttempts`
- `chunks`

`chunks` được đóng gói thành:

- `chunkId`
- `contentText`
- `topicTags`
- `sectionTitle`
- `language`

## Payload kết quả

Gói export trả về các khối sau:

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

## Giá trị thực tế của export package

Nó giúp team:

- so sánh 2 model trên cùng 1 input
- xem AI sai ở đâu: schema, difficulty, duplicate hay review
- export nguyên gói JSON cho FE render
- tạo bằng chứng rõ ràng cho việc chọn model dựa trên cost + output

## Trạng thái hiện tại

Đã xong ở mức prototype:

- contract ổn định
- build xanh
- không phụ thuộc Mongo
- có thể nối vào repository thật sau

Chưa xong ở mức production:

- chưa lưu export package vào DB
- chưa gắn version thật từ `AI_Prompt_Versions`
- chưa gắn `AI_Rubrics` và `AI_GroundTruth_Sets` từ Mongo
