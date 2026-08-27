# Hướng dẫn chuyển từ Prototype sang BE Production

## Mục tiêu

Tài liệu này dùng để chốt rất rõ:

- prototype hiện tại đã dùng được đến mức nào
- khi chuyển sang BE thật thì phần nào giữ nguyên
- phần nào cần refactor
- phần nào phải thay thế hoàn toàn

Mục đích là để:

- không đi sai hướng ở các chat session sau
- các thành viên khác trong nhóm đọc nhanh vẫn hiểu nên làm gì tiếp
- tách bạch giữa `prototype test harness` và `production integration`

## Kết luận ngắn

Prototype hiện tại đã đạt mức:

- `Ready for quality testing`
- `Ready for production-oriented integration design`

Prototype hiện tại chưa đạt mức:

- `Production-ready implementation`

Nói ngắn gọn:

- có thể dùng ngay để test quality, cost, prompt version, evidence
- có thể dùng làm core logic để ghép vào BE
- nhưng khi ghép vào BE thật vẫn phải thay hạ tầng lưu trữ, execution, auth, quota, và một số integration layer

## 1. Những gì có thể giữ nguyên

Đây là các phần nên giữ nguyên tư duy và phần lớn logic khi chuyển vào BE thật.

### 1.1 AI pipeline contracts

Giữ nguyên:

- request/response contracts
- stage logs
- usage logs
- normalized output JSON
- pipeline orchestration theo function

Lý do:

- đây là lõi giúp benchmark và production dùng chung logic
- nếu đổi các contract này quá nhiều thì report, UI test, và DB mapping sẽ bị lệch

### 1.2 Cấu trúc module theo function

Giữ nguyên:

- Gatekeeper
- Extracted Content
- Embedding + Auto Tagging
- Retrieval Planner + Context Packing
- Question Generation + Review
- Code Mentor

Lý do:

- đây là decomposition đúng theo nghiệp vụ AI của hệ thống
- về sau có thể đổi provider, DB, parser, nhưng không nên phá nhỏ lại theo kiểu service rời rạc khó quản lý

### 1.3 Config-driven AI

Giữ nguyên:

- prompt versioning
- policy
- rubric
- ground truth
- ai-config-history snapshot

Lý do:

- đây là phần rất quan trọng cho quality testing và báo cáo
- về production chỉ đổi `source of truth` từ file sang DB, không nên đổi tư duy

### 1.4 Report/evidence flow

Giữ nguyên:

- run history
- evidence export
- benchmark workbook sync
- config snapshot tracking

Lý do:

- đây là mỏ neo để chứng minh cost, quality, prompt change, output evidence
- production phase vẫn cần giữ để debug, audit, và regression testing

## 2. Những gì nên refactor khi ghép BE

Đây là các phần không cần bỏ đi, nhưng cần refactor để khớp codebase thật.

### 2.1 File-backed store -> repository abstraction chuẩn BE

Hiện tại:

- prompt/policy/rubric/ground truth đang file-backed
- run history/context packs/drafts cũng file-backed

Khi ghép BE:

- giữ interface hiện tại nếu hợp lý
- refactor implementation sang repository thật
- tránh để service business đọc file trực tiếp

Mục tiêu:

- Application layer không phụ thuộc vào `App_Data`

### 2.2 Provider gateway

Hiện tại:

- gateway đang phục vụ tốt cho prototype và benchmark

Khi ghép BE:

- chuẩn hóa retry/timeout/circuit-breaker
- chuẩn hóa provider error mapping
- chuẩn hóa usage capture theo provider

Mục tiêu:

- provider layer đủ ổn định cho production traffic

### 2.3 Operator UI

Hiện tại:

- UI phục vụ test quality và benchmark

Khi ghép BE:

- không bê nguyên UI này vào sản phẩm chính
- chỉ giữ nó như internal operator/test harness hoặc admin tool

Mục tiêu:

- UI prototype tiếp tục sống như công cụ test nội bộ
- không trộn với FE chính của sản phẩm

### 2.4 Report sync scripts

Hiện tại:

- script sync workbook/CSV/evidence rất phù hợp cho prototype

Khi ghép BE:

- giữ lại cho benchmark/internal QA
- không nên coi đây là core business service runtime của production

Mục tiêu:

- production dùng DB/log thật
- benchmark harness vẫn dùng script export riêng

## 3. Những gì phải thay thế khi vào production

Đây là các phần prototype không thể giữ nguyên khi đưa vào BE thật.

### 3.1 Local file runtime storage

Phải thay:

- `run-history/`
- `uploads/`
- `extraction-drafts/`
- `context-packs/`
- `benchmark-sessions/`

Bằng:

- Mongo collections
- object/file storage thật nếu cần
- production logging / audit persistence

### 3.2 Local upload handling

Phải thay:

- local file path trong `App_Data/uploads`

Bằng:

- storage service thật
- metadata/document management của hệ thống chính

### 3.3 Prototype parser assumptions

Phải nâng cấp:

- `docx/pptx` parser hiện tại mới ở mức prototype thực dụng

Production cần:

- parser ổn định hơn
- OCR/vision strategy rõ ràng hơn
- error handling tốt hơn cho file lỗi, file nặng, file hỏng

### 3.4 Static mentor review only

Hiện tại:

- Code Mentor chưa compile/run thật

Production cần:

- nối Judge0 hoặc execution service thật
- tách rõ static review và verified execution feedback

### 3.5 Auth / credit / quota

Hiện tại:

- prototype chưa gắn đầy đủ auth thật, quota thật, credit thật

Production cần:

- nối user context thật
- credit deduction
- quota / plan / permission / course ownership

## 4. Checklist theo nhóm việc

## 4.1 Persistence integration

Phải làm:

- [ ] thay file-backed prompt/policy/rubric/ground truth bằng repository DB
- [ ] thay run history bằng collection log thật
- [ ] thay extraction drafts bằng collection draft thật
- [ ] thay context packs bằng collection thật
- [ ] map đầy đủ `ObjectId` với các collection BE tổng

Xong khi:

- không còn service business nào phụ thuộc trực tiếp `App_Data`

## 4.2 Document ingestion integration

Phải làm:

- [ ] nối document metadata thật của hệ thống
- [ ] thay local upload path bằng storage thật
- [ ] chuẩn hóa file parser/OCR pipeline cho tài liệu lớn
- [ ] lưu draft review của human-in-the-loop xuống DB

Xong khi:

- người dùng không bị mất draft nếu tiến trình lỗi hoặc restart

## 4.3 Question generation integration

Phải làm:

- [ ] dùng Mongo repository thật cho `KnowledgeChunks`
- [ ] dùng retrieval planner thật thay vì feed chunks tay từ UI
- [ ] nối duplicate detection với DB
- [ ] nối publish workflow cho question bank
- [ ] nối metadata thật như `course_id`, `document_id`, `created_by`, `is_public`

Xong khi:

- câu hỏi sinh ra có thể đi từ AI sang question bank thật

## 4.4 Code mentor integration

Phải làm:

- [ ] nối `Submission` thật
- [ ] lưu `AI_Mentor_Feedbacks` thật
- [ ] nối Judge0/execution service
- [ ] tách feedback AI với result compile/run verified

Xong khi:

- mentor feedback không còn là chỉ-review-tĩnh

## 4.5 Usage/cost integration

Phải làm:

- [ ] ưu tiên raw usage capture cho từng provider
- [ ] chuẩn hóa cost catalog production
- [ ] nối `AI_Usage_Logs` thật
- [ ] đảm bảo usage log map được với user/agent/submission/document

Xong khi:

- cost trong report gần với production thật nhất có thể

## 5. Những gì prototype hiện tại đã đủ để support production phase

Hiện tại đã đủ tốt cho:

- test quality giữa các model
- test cost/token/latency
- so sánh prompt version
- lưu evidence và parsed output
- kiểm chứng pipeline nghiệp vụ trước khi merge vào BE
- làm nền cho DB design và integration planning

Đây là lý do có thể xem prototype hiện tại là:

- `production-oriented test harness`

chứ không còn là benchmark demo đơn thuần.

## 6. Những gì chưa nên làm sai hướng

Không nên:

- nhét thẳng logic file store vào BE chính
- bê nguyên UI prototype thành UI sản phẩm
- coi report script là hạ tầng production runtime
- bỏ prompt/rubric/policy versioning khi vào BE
- trộn lẫn benchmark concerns với business concerns trong cùng controller/service mà không tách rõ interface

## 7. Thứ tự tích hợp khuyến nghị

Thứ tự nên làm:

1. Mongo repository + collection mapping
2. prompt/policy/rubric/ground truth source chuyển sang DB
3. extraction draft persistence
4. knowledge chunk retrieval thật
5. question generation publish flow
6. mentor + Judge0 integration
7. credit/quota/auth integration

## 8. Định nghĩa done cho phase ghép BE

Có thể xem là xong phase ghép BE khi:

- AI core pipeline vẫn giữ nguyên logic benchmark/test đã ổn định
- không còn phụ thuộc local file runtime cho business data
- cost/log/prompt version vẫn audit được
- question generation đi được tới question bank thật
- mentor đi được tới submission thật
- human-in-the-loop draft không bị mất
- provider usage/cost được ghi nhận đáng tin cậy

## 9. Kết luận cho các chat session sau

Nếu mở chat session mới, hãy mặc định hiểu rằng:

- prototype hiện tại đã đủ tốt để test quality
- mục tiêu tiếp theo không phải viết lại từ đầu
- mục tiêu là `integrate`, `refactor`, `replace infrastructure`, không phải phá core pipeline

Nói ngắn gọn:

- `keep the AI core`
- `replace temporary infrastructure`
- `connect real BE data and execution services`

