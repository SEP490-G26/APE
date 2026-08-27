# Retrieval Planner Contracts

## 1. Mục tiêu

Tài liệu này chốt contract request/response cho tầng `retrieval planner` và `context pack builder`.

Mục tiêu là để khi code backend, chúng ta không phải đổi lại tư duy API giữa chừng.

Các contract này được thiết kế theo nguyên tắc:

- JSON-first
- provider-agnostic
- DB-ready
- benchmark-friendly
- production-expandable

## 2. Vai trò của retrieval planner

Retrieval planner đứng giữa `KnowledgeChunks` và downstream AI.

Luồng đúng:

`KnowledgeChunks -> Retrieval Planner -> AI_Context_Packs -> Question Generation / Review`

Planner chịu trách nhiệm:

1. nhận intent tạo câu hỏi hoặc review
2. lọc candidate chunks
3. re-rank candidate
4. pack context theo token budget
5. trả về plan hoặc pack có thể dùng ngay

## 3. Core enums / giá trị chuẩn hoá

### 3.1 `pack_type`

- `generation_context`
- `review_context`
- `mentor_context`
- `retrieval_debug`

### 3.2 `pack_strategy`

- `topic_focused`
- `chapter_focused`
- `difficulty_balanced`
- `summary_first`
- `hybrid`

### 3.3 `generation_mode`

- `single_question_precise`
- `small_batch`
- `coverage_exam`

### 3.4 `source_scope`

- `SYSTEM_CORE`
- `BYOS`
- `HYBRID`

### 3.5 `pack_status`

- `draft`
- `active`
- `stale`
- `archived`

## 4. Request contract: Retrieval planning

### 4.1 `RetrievalPlanRequest`

```json
{
  "userId": "user-001",
  "courseId": "course-java-oop",
  "documentId": null,
  "subject": "JAVA_OOP",
  "questionType": "PE",
  "difficulty": "Medium",
  "requestedQuestionCount": 1,
  "generationMode": "single_question_precise",
  "targetTopics": ["class", "constructor"],
  "preferredChapterIds": ["chapter-02"],
  "sourceScope": "SYSTEM_CORE",
  "language": "en",
  "useCachedPack": true,
  "forceRebuildPack": false,
  "includeChunkText": false,
  "maxCandidateCount": 40,
  "maxPackedTokens": 7000,
  "retrievalQuery": "Create one medium PE question about constructors and object initialization"
}
```

### 4.2 Field meaning

- `userId`: người gọi request
- `courseId`: môn học trong hệ thống; nullable cho benchmark tự do
- `documentId`: nếu muốn ép retrieval trong một tài liệu cụ thể
- `subject`: mã môn học chuẩn hoá
- `questionType`: `FE` hoặc `PE`
- `difficulty`: `Easy | Medium | Hard`
- `requestedQuestionCount`: số câu muốn sinh
- `generationMode`: mode để planner chọn token budget phù hợp
- `targetTopics`: topic mong muốn
- `preferredChapterIds`: ưu tiên chapter cụ thể nếu có
- `sourceScope`: dùng tri thức hệ thống, BYOS, hay cả hai
- `language`: filter ngôn ngữ nếu cần
- `useCachedPack`: có cho phép reuse pack cũ không
- `forceRebuildPack`: bỏ qua cache và pack lại từ đầu
- `includeChunkText`: debug option, cho phép trả chunk text trong response
- `maxCandidateCount`: trần số candidate trước khi re-rank
- `maxPackedTokens`: token budget tối đa cho context cuối
- `retrievalQuery`: câu mô tả intent retrieval

## 5. Response contract: Retrieval planning

### 5.1 `RetrievalPlanResult`

```json
{
  "planId": "rp-20260718-001",
  "packId": "pack-java-oop-constructor-medium-001",
  "usedCachedPack": false,
  "subject": "JAVA_OOP",
  "questionType": "PE",
  "difficulty": "Medium",
  "generationMode": "single_question_precise",
  "targetTopics": ["class", "constructor"],
  "retrievalQuery": "Create one medium PE question about constructors and object initialization",
  "tokenBudget": {
    "maxPackedTokens": 7000,
    "estimatedPackedTokens": 4820,
    "estimatedSourceTokens": 14620,
    "compressionRatio": 0.3297
  },
  "candidateStats": {
    "preFilterCount": 214,
    "topicMatchCount": 41,
    "vectorCandidateCount": 25,
    "lexicalCandidateCount": 19,
    "rerankedCount": 18,
    "selectedChunkCount": 4
  },
  "selectedChunks": [
    {
      "chunkId": "chunk-001",
      "chunkIndex": 12,
      "sectionTitle": "Constructors",
      "chapterTitle": "Classes and Objects",
      "topicPrimary": "constructor",
      "topicTags": ["constructor", "class", "object"],
      "tokenCount": 820,
      "vectorScore": 0.91,
      "lexicalScore": 0.77,
      "topicScore": 0.95,
      "finalScore": 0.89,
      "selectionRole": "primary"
    }
  ],
  "contextPack": {
    "packId": "pack-java-oop-constructor-medium-001",
    "packType": "generation_context",
    "packStrategy": "summary_first",
    "packStatus": "active",
    "packedSummaryText": "...",
    "packedContextText": "...",
    "tokenCount": 4820,
    "recommendedQuestionCount": 3,
    "maxQuestionCount": 5,
    "sourceChunkIds": ["chunk-001", "chunk-009", "chunk-011", "chunk-013"]
  },
  "stageLogs": [
    "metadata pre-filter applied",
    "hybrid retrieval executed",
    "candidate re-rank completed",
    "summary-first context pack built"
  ]
}
```

## 6. Response contract: Retrieval plan debug

### 6.1 `RetrievalPlanDebugResult`

Debug response mở rộng từ `RetrievalPlanResult` và thêm:

```json
{
  "filtersApplied": {
    "courseId": "course-java-oop",
    "subject": "JAVA_OOP",
    "questionType": "PE",
    "difficulty": "Medium",
    "sourceScope": "SYSTEM_CORE",
    "language": "en"
  },
  "candidatePools": {
    "topicCandidates": ["chunk-001", "chunk-007"],
    "vectorCandidates": ["chunk-001", "chunk-009"],
    "lexicalCandidates": ["chunk-011", "chunk-013"]
  },
  "rejectedChunks": [
    {
      "chunkId": "chunk-021",
      "reason": "duplicate_group_penalty"
    },
    {
      "chunkId": "chunk-035",
      "reason": "token_budget_overflow"
    }
  ],
  "scoringWeights": {
    "vector": 0.35,
    "topic": 0.20,
    "lexical": 0.15,
    "sectionPriority": 0.10,
    "assessmentValue": 0.10,
    "contentQuality": 0.05,
    "boost": 0.05
  }
}
```

## 7. Request contract: Context pack build directly

Trường hợp muốn build pack từ một tập chunk đã biết trước.

### 7.1 `ContextPackBuildRequest`

```json
{
  "userId": "user-001",
  "courseId": "course-c-basic",
  "documentId": "doc-lecture-01",
  "subject": "C_BASIC",
  "questionType": "FE",
  "difficulty": "Easy",
  "packType": "generation_context",
  "packStrategy": "summary_first",
  "generationMode": "small_batch",
  "targetTopics": ["variable", "data_type"],
  "chunkIds": ["chunk-001", "chunk-002", "chunk-005"],
  "maxPackedTokens": 5000,
  "rebuildSummary": false,
  "notes": "manual build for benchmark pack"
}
```

## 8. Response contract: Context pack build

### 8.1 `ContextPackBuildResult`

```json
{
  "packId": "pack-c-basic-variable-easy-001",
  "packStatus": "active",
  "packType": "generation_context",
  "packStrategy": "summary_first",
  "subject": "C_BASIC",
  "questionType": "FE",
  "difficulty": "Easy",
  "targetTopics": ["variable", "data_type"],
  "sourceChunkIds": ["chunk-001", "chunk-002", "chunk-005"],
  "sourceTokenCount": 6200,
  "tokenCount": 2870,
  "compressionRatio": 0.4629,
  "packedSummaryText": "...",
  "packedContextText": "...",
  "recommendedQuestionCount": 4,
  "maxQuestionCount": 6,
  "createdAt": "2026-07-18T10:25:00Z"
}
```

## 9. Response contract: Context pack detail

### 9.1 `ContextPackDetailResult`

```json
{
  "packId": "pack-c-basic-variable-easy-001",
  "packType": "generation_context",
  "packStrategy": "summary_first",
  "packStatus": "active",
  "subject": "C_BASIC",
  "questionType": "FE",
  "targetDifficulty": "Easy",
  "targetTopics": ["variable", "data_type"],
  "chapterScope": ["chapter-01"],
  "sourceChunkIds": ["chunk-001", "chunk-002", "chunk-005"],
  "sourceChunkRefs": [
    {
      "chunkId": "chunk-001",
      "chunkIndex": 1,
      "sectionTitle": "Variables",
      "topicPrimary": "variable",
      "tokenCount": 1200,
      "retrievalRank": 1
    }
  ],
  "retrievalQuery": "Create easy FE questions about variables and basic data types",
  "retrievalFilters": {
    "courseId": "course-c-basic",
    "sourceScope": "SYSTEM_CORE"
  },
  "packedSummaryText": "...",
  "packedContextText": "...",
  "packedMarkdownText": "...",
  "tokenCount": 2870,
  "sourceTokenCount": 6200,
  "compressionRatio": 0.4629,
  "recommendedQuestionCount": 4,
  "maxQuestionCount": 6,
  "usageCount": 3,
  "lastUsedAt": "2026-07-18T10:40:00Z",
  "expiresAt": "2026-08-18T00:00:00Z"
}
```

## 10. Response contract: Context pack list

### 10.1 `ContextPackSummaryResult`

```json
{
  "packId": "pack-c-basic-variable-easy-001",
  "subject": "C_BASIC",
  "questionType": "FE",
  "targetDifficulty": "Easy",
  "targetTopics": ["variable", "data_type"],
  "packStrategy": "summary_first",
  "tokenCount": 2870,
  "recommendedQuestionCount": 4,
  "usageCount": 3,
  "packStatus": "active",
  "updatedAt": "2026-07-18T10:25:00Z"
}
```

## 11. Contract gắn với question generation

### 11.1 Hướng production-ready

`QuestionGenerationRequest` về lâu dài nên hỗ trợ cả 2 mode:

1. truyền raw `chunks`
   - dùng cho benchmark/debug/manual test
2. truyền `contextPackId`
   - dùng cho production path

### 11.2 Contract đề xuất

```json
{
  "userId": "user-001",
  "courseId": "course-java-oop",
  "documentId": null,
  "isPublic": false,
  "subject": "JAVA_OOP",
  "difficulty": "Medium",
  "questionType": "PE",
  "count": 1,
  "generatorModel": "gpt-5.4-mini",
  "revisionFeedback": null,
  "previousQuestions": null,
  "contextPackId": "pack-java-oop-constructor-medium-001",
  "chunks": []
}
```

Quy tắc:

- nếu có `contextPackId` thì generation ưu tiên load pack
- `chunks` chỉ là fallback/debug path
- production flow nên yêu cầu ít nhất một trong hai:
  - `contextPackId`
  - `chunks`

## 12. Route contract đề xuất

### 12.1 Retrieval planner

- `POST /api/ai-module/retrieval/plan`
  - tạo plan và nếu cần thì build/load context pack
- `POST /api/ai-module/retrieval/plan/debug`
  - trả thêm rejected chunks, scoring weights, candidate pools

### 12.2 Context packs

- `POST /api/ai-module/context-packs/build`
  - build pack từ danh sách chunk đã biết
- `GET /api/ai-module/context-packs/{packId}`
  - lấy detail một pack
- `GET /api/ai-module/context-packs`
  - list packs theo filter `subject`, `questionType`, `difficulty`, `topic`, `status`
- `POST /api/ai-module/context-packs/{packId}/mark-stale`
  - đánh dấu pack cần rebuild

## 13. Gợi ý DTO C# sau này

### 13.1 Request DTO

- `RetrievalPlanRequest`
- `ContextPackBuildRequest`
- `ContextPackListFilterRequest`

### 13.2 Response DTO

- `RetrievalPlanResult`
- `RetrievalPlanDebugResult`
- `ContextPackBuildResult`
- `ContextPackDetailResult`
- `ContextPackSummaryResult`
- `RetrievalTokenBudgetDto`
- `RetrievedChunkScoreDto`

## 14. Nguyên tắc validate

### 14.1 Request validate

- `subject` bắt buộc
- `questionType` bắt buộc nếu mục tiêu là generation/review
- `requestedQuestionCount >= 1`
- `maxCandidateCount > 0`
- `maxPackedTokens > 0`
- `targetTopics` không bắt buộc nhưng nên có cho generation chính xác

### 14.2 Build validate

- `chunkIds` phải không rỗng với `ContextPackBuildRequest`
- `packStrategy` phải nằm trong enum chuẩn hoá
- `packType` phải nằm trong enum chuẩn hoá

## 15. Logging và history

Mỗi retrieval plan nên lưu tối thiểu:

- `planId`
- `packId`
- `request snapshot`
- `selected chunk ids`
- `token budget`
- `compression ratio`
- `usedCachedPack`
- `config refs` của policy/prompt nếu có

Điều này rất quan trọng để:

- benchmark cost
- giải thích tại sao một request đắt
- audit chất lượng grounding

## 16. Kết luận

Contract retrieval planner nên được chốt trước khi code vì đây là tầng sẽ quyết định:

- chi phí production của question generation
- cách tái sử dụng context
- khả năng benchmark có ý nghĩa trên tài liệu lớn

Nếu contract này ổn, bước code sau đó có thể đi rất thẳng:

1. DTO
2. interface service
3. in-memory/file-backed repository cho `AI_Context_Packs`
4. orchestrator route
5. nối sang generation flow
