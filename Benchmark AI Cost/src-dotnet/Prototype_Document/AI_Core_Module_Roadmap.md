# AI Core Module Roadmap

## 1. Muc tieu

Tai lieu nay chot huong phat trien module AI theo dung ban chat moi:

- khong con la benchmark tool don le
- tro thanh `AI Core Prototype`
- vua dung de benchmark, test accuracy, test cost
- vua la nen de ghep vao BE chinh cua do an

Module nay phai duoc xay theo huong:

- JSON-first
- pipeline-first
- provider-agnostic
- prompt-versioned
- co kha nang doi sang DB that sau nay

## 2. Vai tro cua module

Module AI nay dong thoi phuc vu 3 viec:

### 2.1 Benchmark

- test token
- test cost
- test latency
- so sanh model
- luu log chi tiet theo tung stage

### 2.2 Prototype nghiep vu

- chay duoc cac luong AI chinh cua do an
- mo phong dung pipeline san pham
- kiem tra output JSON co dung schema mong muon khong

### 2.3 Nen de tich hop production

- co the noi vao BE tong
- doi repository tam sang Mongo repository
- map object sang collection that
- giu nguyen logic orchestration va provider gateway

## 3. 6 chuc nang AI cot loi

Module phai bao gom day du 6 chuc nang sau:

1. `AI Gatekeeper`
2. `AI Extracted Content`
3. `AI Embedding`
4. `AI Auto Tagging`
5. `AI Question Generation + AI Review`
6. `AI Code Mentor`

Luu y:

- `Question Generation` va `Review` la 2 agent rieng ve mat logic
- co the chay cung 1 model, cung 1 provider, hoac 2 model khac nhau
- `Embedding` va `Auto Tagging` la 2 stage rieng, khong nen gop thanh 1 log duy nhat

## 4. Kien truc muc tieu

### 4.1 Domain

Chua:

- entity nghiep vu AI
- result contract
- token/cost/log contract
- review decision
- question schema

### 4.2 Application

Chua:

- DTO request/response
- interface cho provider gateway
- orchestrator cho tung pipeline
- rule xu ly pipeline

### 4.3 Infrastructure

Chua:

- provider adapter
- prompt store
- parser / chunking / file extraction
- repository tam
- sau nay se them Mongo adapter

### 4.4 API

Chua:

- route cho tung function AI
- route benchmark/test
- route provider management
- route prompt management

## 5. Phan chia giai doan

### Phase 1 - Core Benchmarkable Prototype

Muc tieu:

- chay duoc end-to-end
- tra JSON duoc
- test duoc token/cost/log
- doi duoc model/provider

Done criteria:

- ca 6 function co route rieng
- provider load tu `.env`
- prompt khong hard-code hoan toan trong service
- moi pipeline co `stageLogs` va `usageLogs`

Trang thai hien tai:

- da dat phan lon
- nhung van chua du production-ready

### Phase 2 - Schema Hardening

Muc tieu:

- ep output dung schema DB / collection that
- giam output loi / thieu field / null field

Can lam:

1. siet prompt generation
2. them validation sau generation
3. them review rubric ro rang
4. neu output sai schema thi reject som

Done criteria:

- FE question output dung schema FE
- PE question output dung schema PE
- mentor output co cau truc on dinh
- gatekeeper va extraction co verdict ro

### Phase 3 - Subject-aware Intelligence

Muc tieu:

- dua module ve dung logic hoc tap cua do an

Can lam:

1. tach prompt theo mon:
   - C
   - Java OOP
   - DSA Java
2. tach rubric theo 6 to hop:
   - C FE
   - C PE
   - Java FE
   - Java PE
   - DSA FE
   - DSA PE
3. chot topic tag taxonomy theo tung mon

Done criteria:

- review khong con generic
- generation va mentor co context theo mon hoc

### Phase 4 - Persistence-ready

Muc tieu:

- co the dua vao BE tong ma khong sua lai toan bo pipeline

Can lam:

1. thay repository tam bang Mongo adapter
2. map entity sang collection that
3. them id mapping that (`ObjectId`)
4. them log persistence cho `AI_Usage_Logs`
5. them prompt version persistence

Done criteria:

- module co the luu / doc tu Mongo
- output JSON va du lieu DB khong lech nhau

### Phase 5 - Production Integration

Muc tieu:

- merge vao codebase BE tong

Can lam:

1. flatten module vao 4 layer cua BE chinh
2. noi auth, credit, quota, user context
3. noi course/document/question/submission data
4. rate limit va timeout
5. quan ly loi provider that

Done criteria:

- co the goi module tu nghiep vu that
- benchmark va production dung chung core pipeline

## 6. Cac pipeline phai chot

### 6.1 Ingestion pipeline

Luong:

`Input file -> Gatekeeper -> Extracted Content / Parser -> Chunking -> Embedding -> Auto Tagging`

Output:

- document metadata
- chunks
- usage logs
- stage logs

### 6.2 Generation-review pipeline

Luong:

`Chunks -> Generator -> Reviewer -> Pass / Regenerate -> End`

Quy tac:

- toi da 2 vong
- ho tro:
  - `SingleAgent`
  - `SameModelDualRole`
  - `DualAgent`

Output:

- generated questions
- review decision
- usage logs theo tung attempt

### 6.3 Code mentor pipeline

Luong:

`Problem + Submission Code -> Mentor AI -> Feedback JSON`

Output:

- verdict
- issues
- suggestions
- complexity neu co

## 7. Nhung phan phai duoc xem la "production-critical"

Day la nhung phan khong duoc phep lam theo kieu demo:

1. prompt management
2. output schema validation
3. usage logging
4. provider configuration
5. retry / timeout / fail-safe
6. subject-aware rubric
7. mapping collection schema

Neu 7 phan nay khong on, module se chi dung duoc de benchmark, chua du de dua vao he thong that.

## 8. Nhung gi can sua khi noi vao DB that

### 8.1 KnowledgeChunks

Can map dung vao collection `KnowledgeChunks` va bo sung cac field metadata can thiet.

### 8.2 FE_Questions / PE_Questions

Question generation phai tra JSON de co the:

- save thang vao `FE_Questions`
- save thang vao `PE_Questions`

neu khong thi phai co 1 lop mapper ro rang o application/infrastructure.

### 8.3 AI_Mentor_Feedbacks

Mentor output phai co cau truc co the map sang:

- `feedback_text`
- `suggested_complexity`
- metadata usage log neu can

### 8.4 AI_Usage_Logs

Moi lan goi AI la 1 ban ghi log rieng.

Can giu:

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

## 9. Thu tu uu tien de phat trien tiep

Thu tu de nghi:

1. chot contract JSON cho 6 function
2. siet output generation/review
3. viet rubric theo mon hoc + loai cau hoi
4. chot schema `KnowledgeChunks`
5. them Mongo repository adapter
6. them mapping log sang `AI_Usage_Logs`
7. sau cung moi hoan thien FE test

Ly do:

- FE co the doi sau
- core pipeline neu sai thi FE dep cung vo ich

## 10. Ranh gioi giua benchmark va production

### Benchmark mode

Uu tien:

- chay nhanh
- de so sanh model
- log nhieu
- linh hoat doi model/prompt

### Production mode

Uu tien:

- output on dinh
- schema chac
- luu DB dung
- fail-safe ro rang
- quan ly credit/quota

Core chung giua 2 mode:

- provider gateway
- prompt registry
- orchestrator
- schema contract
- usage logging

## 11. Ket luan

Module nay phai duoc xem la:

- `AI Core Prototype`
- khong phai mot tool benchmark tam thoi

Benchmark tu gio chi la 1 cach van hanh module de:

- do chi phi
- do ket qua
- so sanh model

Con muc tieu chinh la:

- xay dung 1 core module co the dua vao BE tong cua do an.
