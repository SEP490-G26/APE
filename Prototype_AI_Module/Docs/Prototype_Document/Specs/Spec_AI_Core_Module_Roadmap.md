# AI Core Module Roadmap

## 1. Mục tiêu

Tài liệu này chốt hướng phát triển module AI theo đúng bản chất mới:

- không còn là benchmark tool đơn lẻ
- trở thành `AI Core Prototype`
- vừa dùng để benchmark, test accuracy, test cost
- vừa là nền để ghép vào BE chính của đồ án

Module này phải được xây theo hướng:

- JSON-first
- pipeline-first
- provider-agnostic
- prompt-versioned
- có khả năng đổi sang DB thật sau này

## 2. Vai trò của module

Module AI này đồng thời phục vụ 3 việc:

### 2.1 Benchmark

- test token
- test cost
- test latency
- so sánh model
- lưu log chi tiết theo từng stage

### 2.2 Prototype nghiệp vụ

- chạy được các luồng AI chính của đồ án
- mô phỏng đúng pipeline sản phẩm
- kiểm tra output JSON có đúng schema mong muốn không

### 2.3 Nền để tích hợp production

- có thể nối vào BE tổng
- đổi repository tạm sang Mongo repository
- map object sang collection thật
- giữ nguyên logic orchestration và provider gateway

## 3. 6 chức năng AI cốt lõi

Module phải bao gồm đầy đủ 6 chức năng sau:

1. `AI Gatekeeper`
2. `AI Extracted Content`
3. `AI Embedding`
4. `AI Auto Tagging`
5. `AI Question Generation + AI Review`
6. `AI Code Mentor`

Lưu ý:

- `Question Generation` và `Review` là 2 agent riêng về mặt logic
- có thể chạy cùng 1 model, cùng 1 provider hoặc 2 model khác nhau
- `Embedding` và `Auto Tagging` là 2 stage riêng, không nên gộp thành 1 log duy nhất

## 4. Kiến trúc mục tiêu

### 4.1 Domain

Chứa:

- entity nghiệp vụ AI
- result contract
- token/cost/log contract
- review decision
- question schema

### 4.2 Application

Chứa:

- DTO request/response
- interface cho provider gateway
- orchestrator cho từng pipeline
- rule xử lý pipeline

### 4.3 Infrastructure

Chứa:

- provider adapter
- prompt store
- parser / chunking / file extraction
- repository tạm
- sau này sẽ thêm Mongo adapter

### 4.4 API

Chứa:

- route cho từng function AI
- route benchmark/test
- route provider management
- route prompt management

## 5. Phân chia giai đoạn

### Phase 1 - Core Benchmarkable Prototype

Mục tiêu:

- chạy được end-to-end
- trả JSON được
- test được token/cost/log
- đổi được model/provider

Done criteria:

- cả 6 function có route riêng
- provider load từ `.env`
- prompt không hard-code hoàn toàn trong service
- mỗi pipeline có `stageLogs` và `usageLogs`

Trạng thái hiện tại:

- đã đạt phần lớn
- nhưng vẫn chưa đủ production-ready

### Phase 2 - Schema Hardening

Mục tiêu:

- ép output đúng schema DB / collection thật
- giảm output lỗi / thiếu field / null field

Cần làm:

1. siết prompt generation
2. thêm validation sau generation
3. thêm review rubric rõ ràng
4. nếu output sai schema thì reject sớm

Done criteria:

- FE question output đúng schema FE
- PE question output đúng schema PE
- mentor output có cấu trúc ổn định
- gatekeeper và extraction có verdict rõ

### Phase 3 - Subject-aware Intelligence

Mục tiêu:

- đưa module về đúng logic học tập của đồ án

Cần làm:

1. tách prompt theo môn:
   - C
   - Java OOP
   - DSA Java
2. tách rubric theo 6 tổ hợp:
   - C FE
   - C PE
   - Java FE
   - Java PE
   - DSA FE
   - DSA PE
3. chốt topic tag taxonomy theo từng môn

Done criteria:

- review không còn generic
- generation và mentor có context theo môn học

### Phase 4 - Persistence-ready

Mục tiêu:

- có thể đưa vào BE tổng mà không sửa lại toàn bộ pipeline

Cần làm:

1. thay repository tạm bằng Mongo adapter
2. map entity sang collection thật
3. thêm id mapping thật (`ObjectId`)
4. thêm log persistence cho `AI_Usage_Logs`
5. thêm prompt version persistence

Done criteria:

- module có thể lưu / đọc từ Mongo
- output JSON và dữ liệu DB không lệch nhau

### Phase 5 - Production Integration

Mục tiêu:

- merge vào codebase BE tổng

Cần làm:

1. flatten module vào 4 layer của BE chính
2. nối auth, credit, quota, user context
3. nối course/document/question/submission data
4. rate limit và timeout
5. quản lý lỗi provider thật

Done criteria:

- có thể gọi module từ nghiệp vụ thật
- benchmark và production dùng chung core pipeline

## 6. Các pipeline phải chốt

### 6.1 Ingestion pipeline

Luồng:

`Input file -> Gatekeeper -> Extracted Content / Parser -> Chunking -> Embedding -> Auto Tagging -> Retrieval Planner -> Context Packing`

Output:

- document metadata
- knowledge chunks / context packs
- usage logs
- stage logs

### 6.2 Generation-review pipeline

Luồng:

`knowledge chunks / context packs -> Generator -> Reviewer -> Pass / Regenerate -> End`

Quy tắc:

- tối đa 2 vòng
- hỗ trợ:
  - `SingleAgent`
  - `SameModelDualRole`
  - `DualAgent`

Output:

- generated questions
- review decision
- usage logs theo từng attempt

### 6.3 Code mentor pipeline

Luồng:

`Problem + Submission Code -> Mentor AI -> Feedback JSON`

Output:

- verdict
- issues
- suggestions
- complexity nếu có

## 7. Những phần phải được xem là production-critical

Đây là những phần không được phép làm theo kiểu demo:

1. prompt management
2. output schema validation
3. usage logging
4. provider configuration
5. retry / timeout / fail-safe
6. subject-aware rubric
7. mapping collection schema

Nếu 7 phần này không ổn, module sẽ chỉ dùng được để benchmark, chưa đủ để đưa vào hệ thống thật.

## 8. Những gì cần sửa khi nối vào DB thật

### 8.1 Knowledge chunks / context packs

Cần map đúng vào collection `KnowledgeChunks / AI_Context_Packs` và bổ sung các field metadata cần thiết.

### 8.2 FE_Questions / PE_Questions

Question generation phải trả JSON để có thể:

- save thẳng vào `FE_Questions`
- save thẳng vào `PE_Questions`

nếu không thì phải có 1 lớp mapper rõ ràng ở application/infrastructure.

### 8.3 AI_Mentor_Feedbacks

Mentor output phải có cấu trúc có thể map sang:

- `feedback_text`
- `suggested_complexity`
- metadata usage log nếu cần

### 8.4 AI_Usage_Logs

Mỗi lần gọi AI là 1 bản ghi log riêng.

Cần giữ:

- `pipeline_run_id`
- `stage_name`
- `attempt_index`
- `provider`
- `model_name`
- `input_tokens`
- `output_tokens`
- `input_cost_usd`
- `output_cost_usd`
- `cost_usd`
- `payload_data`

## 9. Thứ tự ưu tiên để phát triển tiếp

Thứ tự đề nghị:

1. chốt contract JSON cho 6 function
2. siết output generation/review
3. viết rubric theo môn học + loại câu hỏi
4. chốt schema `KnowledgeChunks / AI_Context_Packs`
5. thêm Mongo repository adapter
6. thêm mapping log sang `AI_Usage_Logs`
7. sau cùng mới hoàn thiện FE test

Lý do:

- FE có thể đợi sau
- core pipeline nếu sai thì FE đẹp cũng vô ích

## 10. Ranh giới giữa benchmark và production

### Benchmark mode

Ưu tiên:

- chạy nhanh
- dễ so sánh model
- log nhiều
- linh hoạt đổi model/prompt

### Production mode

Ưu tiên:

- output ổn định
- schema chắc
- lưu DB đúng
- fail-safe rõ ràng
- quản lý credit/quota

Core chung giữa 2 mode:

- provider gateway
- prompt registry
- orchestrator
- schema contract
- usage logging

## 11. Kết luận

Module này phải được xem là:

- `AI Core Prototype`
- không phải một tool benchmark tạm thời

Benchmark từ giờ chỉ là 1 cách vận hành module để:

- đo chi phí
- đo kết quả
- so sánh model

Còn mục tiêu chính là:

- xây dựng 1 core module có thể đưa vào BE tổng của đồ án.

