# AI Rubric System

## Mục tiêu

Tài liệu này mô tả cách thiết kế và sử dụng bộ `difficulty rubric` cho chức năng:

- `AI Question Generation`
- `AI Review result of AI Question Generation`

Bộ rubric này được đặt theo hướng:

- prompt và nội dung rubric dùng tiếng Anh
- rubric là `structured JSON`, không phải chỉ là prompt text thủ công
- benchmark, prototype và production có thể dùng cùng một source of truth

## Vị trí rubric source

Rubric source hiện tại được đặt tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/`

Danh sách:

- `C_FE.json`
- `C_PE.json`
- `JAVA_OOP_FE.json`
- `JAVA_OOP_PE.json`
- `DSA_JAVA_FE.json`
- `DSA_JAVA_PE.json`
- `README.json`

Ground truth starter datasets cho `Question Generation` hiện đặt tại:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/`

## Tại sao dùng JSON thay vì prompt text thủ công

Nếu chỉ dùng prompt text:

- khó maintain
- khó version hoá
- dễ lệch giữa benchmark và production
- khó diff khi đổi tiêu chí đánh giá

Nếu dùng JSON:

- có 1 source of truth rõ ràng
- prototype và production có thể dùng chung
- có thể render thành prompt cho nhiều model/provider
- có thể ghi version, review, diff rất dễ

## Cấu trúc chung của 1 rubric JSON

Mỗi file rubric gồm các phần chính:

### 1. Metadata

- `rubric_id`
- `version`
- `language`
- `subject_code`
- `question_type`
- `title`

### 2. Difficulty definitions

`difficulty_definitions` chia thành:

- `easy`
- `medium`
- `hard`

Mỗi mục gồm:

- `description`
- `expected_skills`
- `should_avoid`

Ý nghĩa:

- `description`: định nghĩa mức độ khó
- `expected_skills`: AI reviewer kỳ vọng thấy gì ở mức độ này
- `should_avoid`: những dấu hiệu cho thấy câu hỏi đang vượt quá scope hoặc lệch mức

### 3. Review checks

`review_checks` là danh sách các tiêu chí mà AI reviewer phải check mỗi lần review.

Đây là lớp review học thuật hoặc nghiệp vụ.

### 4. Difficulty mismatch signals

`difficulty_mismatch_signals` chia theo:

- `easy`
- `medium`
- `hard`

Mỗi mục liệt kê các dấu hiệu cho thấy câu hỏi đang bị gán sai difficulty.

Ví dụ:

- gán `Easy` nhưng lại cần multi-step tracing
- gán `Hard` nhưng thực tế chỉ là definition recall

### 5. Acceptance rules

`acceptance_rules` quy định khi nào reviewer được chấp nhận câu hỏi.

### 6. Regeneration hints

`regeneration_hints` là gợi ý cho lần generate tiếp theo nếu AI reviewer reject.

## 6 tổ hợp rubric hiện tại

### 1. `C_FE`

Dùng cho:

- môn C nhập môn
- câu hỏi multiple choice

### 2. `C_PE`

Dùng cho:

- môn C nhập môn
- practice/code question

### 3. `JAVA_OOP_FE`

Dùng cho:

- môn Java OOP
- câu hỏi multiple choice

### 4. `JAVA_OOP_PE`

Dùng cho:

- môn Java OOP
- practice/code question

### 5. `DSA_JAVA_FE`

Dùng cho:

- môn Data Structures and Algorithms trên Java
- câu hỏi multiple choice

### 6. `DSA_JAVA_PE`

Dùng cho:

- môn Data Structures and Algorithms trên Java
- practice/code question

## Bối cảnh FPT University

Rubric source hiện tại đã được thêm `institution_context` ở mức nhẹ để bám sát đối tượng sử dụng:

- sinh viên Đại học FPT
- nhóm môn học có hướng gần với:
  - PRF-style foundational programming
  - PRO-style Java OOP
  - CSD-style data structures and algorithms

Mục đích:

- giữ style đề bài phù hợp với sinh viên mục tiêu
- không để rubric trở thành quá chung chung
- vẫn giữ nội dung ở mức học thuật, không phụ thuộc vào 1 syllabus nội bộ cụ thể

## Cách dùng trong prototype hiện tại

Prototype hiện tại nên dùng bộ rubric này theo flow:

1. Xác định:
   - `subject`
   - `question_type`
2. Chọn file rubric JSON tương ứng
3. Nhúng rubric JSON vào `question_review` prompt
4. AI reviewer dùng rubric đó để:
   - check scope
   - check difficulty
   - check quality
5. Nếu reject:
   - đưa issue + suggestion + regeneration hints vào vòng generate tiếp theo

Hiện tại prototype đã hỗ trợ route runtime để lấy source này:

- `GET /api/ai-module/question-generation/rubric?subject=...&questionType=...`
- `GET /api/ai-module/question-generation/ground-truth?subject=...&questionType=...`

## Cách dùng trong production sau này

Khi ghép vào BE tổng, không cần copy prompt text thủ công.

Chỉ cần:

1. giữ nguyên rubric JSON làm source of truth
2. đưa rubric vào:
   - DB
   - hoặc config/prompt registry của hệ thống
3. prompt template render rubric ra prompt string cho model
4. version hoá rubric nếu có thay đổi

Nghĩa là:

- production và prototype dùng chung nội dung rubric
- chỉ khác cách lưu trữ hoặc nạp dữ liệu

## Prompt và rubric: phần nào nên để tiếng Anh

Nên để tiếng Anh cho:

- `difficulty definitions`
- `review checks`
- `difficulty mismatch signals`
- `acceptance rules`
- `regeneration hints`
- prompt runtime gửi cho AI

Lý do:

- model đọc ổn định hơn
- giảm mơ hồ
- dễ maintain trên nhiều provider/model

Có thể để tiếng Việt cho:

- tài liệu nội bộ nhóm
- ghi chú báo cáo
- mô tả quy trình

## Rubric và rule-based checks khác nhau thế nào

### Rubric

Rubric là bộ tiêu chí để AI reviewer đánh giá:

- phạm vi kiến thức
- độ khó
- chất lượng câu hỏi
- sự phù hợp với môn học

### Rule-based checks

Rule-based checks là các check bằng code:

- schema đúng hay sai
- `correct_answer` có hợp lệ không
- có field bắt buộc không
- có duplicate không
- có source chunk id hợp lệ không

Cần cả hai.

Rubric không thay thế được validation bằng code.

## Hướng nâng cấp tiếp theo

### 1. Nối rubric vào code runtime

Hiện tại bộ rubric đã được tạo thành JSON source.

Bước tiếp theo là:

- viết service đọc rubric file
- `BuildReviewRubric(...)` không hard-code nữa
- render rubric JSON động vào prompt

### 2. Thêm difficulty alignment logs

Nên log thêm:

- rubric id nào đã được dùng
- requested difficulty
- reviewer difficulty alignment notes

### 3. Thêm rubric versioning

Mỗi rubric nên có:

- `version`
- `updated_at`
- `updated_by`
- change log nếu cần

## Kết luận

Bộ rubric này được thiết kế để:

- là source of truth chung cho benchmark, prototype và production
- giữ prompt ở dạng maintainable
- nâng chất lượng review difficulty theo từng môn học và từng loại câu hỏi

Nó không phải prompt text đơn lẻ.

Nó là một hệ thống rubric có cấu trúc, có thể render thành prompt và có thể version hoá về sau.
