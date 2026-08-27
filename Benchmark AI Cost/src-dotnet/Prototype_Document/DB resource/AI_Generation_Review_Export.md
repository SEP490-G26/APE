# AI Generation Review Export

## Muc tieu

Tai lieu nay mo ta goi JSON chuan hoa duoc tra ve boi route:

- `POST /api/ai-module/generation-review/export`

No dung cho 4 muc dich:

1. benchmark chi phi
2. debug output generator + reviewer
3. export ket qua test de dua vao bao cao
4. lam contract tam truoc khi noi vao Mongo repository that

## Payload metadata

`metadata` gom:

- `packageId`: id cua goi export
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

`request` la snapshot cua input goc:

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

`chunks` duoc dong bang thanh:

- `chunkId`
- `contentText`
- `topicTags`
- `sectionTitle`
- `language`

## Payload ket qua

Goi export tra ve cac khoi sau:

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

## Gia tri thuc te cua export package

No giup team:

- so sanh 2 model tren cung 1 input
- xem AI sai o dau: schema, difficulty, duplicate hay review
- export nguyen goi JSON cho FE render
- tao bang chung ro rang cho viec chon model dua tren cost + output

## Trang thai hien tai

Da xong o muc prototype:

- contract on dinh
- build xanh
- khong phu thuoc Mongo
- co the noi vao repository that sau

Chua xong o muc production:

- chua luu export package vao DB
- chua gan version that tu `AI_Prompt_Versions`
- chua gan `AI_Rubrics` va `AI_GroundTruth_Sets` tu Mongo
