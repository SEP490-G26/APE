# AI Rubric System

## Muc tieu

Tai lieu nay mo ta cach thiet ke va su dung bo `difficulty rubric` cho chuc nang:

- `AI Question Generation`
- `AI Review result of AI Question Generation`

Bo rubric nay duoc dat theo huong:

- prompt va noi dung rubric dung tieng Anh
- rubric la `structured JSON`, khong phai chi la prompt text thu cong
- benchmark, prototype, va production co the dung cung mot source of truth

## Vi tri rubric source

Rubric source hien tai duoc dat tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/`

Danh sach:

- `C_FE.json`
- `C_PE.json`
- `JAVA_OOP_FE.json`
- `JAVA_OOP_PE.json`
- `DSA_JAVA_FE.json`
- `DSA_JAVA_PE.json`
- `README.json`

Ground truth starter datasets cho `Question Generation` hien dat tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/`

## Tai sao dung JSON thay vi prompt text thu cong

Neu chi dung prompt text:

- kho maintain
- kho version hoa
- de lech giua benchmark va production
- kho diff khi doi tieu chi danh gia

Neu dung JSON:

- co 1 source of truth ro rang
- prototype va production co the dung chung
- co the render thanh prompt cho nhieu model/provider
- co the ghi version, review, diff rat de

## Cau truc chung cua 1 rubric JSON

Moi file rubric gom cac phan chinh:

### 1. Metadata

- `rubric_id`
- `version`
- `language`
- `subject_code`
- `question_type`
- `title`

### 2. Difficulty definitions

`difficulty_definitions` chia thanh:

- `easy`
- `medium`
- `hard`

Moi muc gom:

- `description`
- `expected_skills`
- `should_avoid`

Y nghia:

- `description`: dinh nghia muc do kho
- `expected_skills`: AI reviewer ky vong thay gi o muc do nay
- `should_avoid`: nhung dau hieu cho thay cau hoi dang vuot qua scope hoac lech muc

### 3. Review checks

`review_checks` la danh sach cac tieu chi ma AI reviewer phai check moi lan review.

Day la lop review hoc thuat / nghiep vu.

### 4. Difficulty mismatch signals

`difficulty_mismatch_signals` chia theo:

- `easy`
- `medium`
- `hard`

Moi muc liet ke cac dau hieu cho thay cau hoi dang bi gan sai difficulty.

Vi du:

- gan `Easy` nhung lai can multi-step tracing
- gan `Hard` nhung thuc te chi la definition recall

### 5. Acceptance rules

`acceptance_rules` quy dinh khi nao reviewer duoc chap nhan cau hoi.

### 6. Regeneration hints

`regeneration_hints` la goi y cho lan generate tiep theo neu AI reviewer reject.

## 6 to hop rubric hien tai

### 1. `C_FE`

Dung cho:

- mon C nhap mon
- cau hoi multiple choice

### 2. `C_PE`

Dung cho:

- mon C nhap mon
- practice/code question

### 3. `JAVA_OOP_FE`

Dung cho:

- mon Java OOP
- cau hoi multiple choice

### 4. `JAVA_OOP_PE`

Dung cho:

- mon Java OOP
- practice/code question

### 5. `DSA_JAVA_FE`

Dung cho:

- mon Data Structures and Algorithms tren Java
- cau hoi multiple choice

### 6. `DSA_JAVA_PE`

Dung cho:

- mon Data Structures and Algorithms tren Java
- practice/code question

## Bo canh FPT University

Rubric source hien tai da duoc them `institution_context` o muc nhe de bam sat doi tuong su dung:

- sinh vien Dai hoc FPT
- nhom mon hoc co huong gan voi:
  - PRF-style foundational programming
  - PRO-style Java OOP
  - CSD-style data structures and algorithms

Muc dich:

- giu style de bai phu hop voi sinh vien muc tieu
- khong de rubric tro thanh qua chung chung
- van giu noi dung o muc hoc thuat, khong phu thuoc vao 1 syllabus noi bo cu the

## Cach dung trong prototype hien tai

Prototype hien tai nen dung bo rubric nay theo flow:

1. Xac dinh:
   - `subject`
   - `question_type`
2. Chon file rubric JSON tuong ung
3. Nhung rubric JSON vao `question_review` prompt
4. AI reviewer dung rubric do de:
   - check scope
   - check difficulty
   - check quality
5. Neu reject:
   - dua issue + suggestion + regeneration hints vao vong generate tiep theo

Hien tai prototype da ho tro route runtime de lay source nay:

- `GET /api/ai-module/question-generation/rubric?subject=...&questionType=...`
- `GET /api/ai-module/question-generation/ground-truth?subject=...&questionType=...`

## Cach dung trong production sau nay

Khi ghep vao BE tong, khong can copy prompt text thu cong.

Chi can:

1. giu nguyen rubric JSON lam source of truth
2. dua rubric vao:
   - DB
   - hoac config/prompt registry cua he thong
3. prompt template render rubric ra prompt string cho model
4. version hoa rubric neu co thay doi

Nghia la:

- production va prototype dung chung noi dung rubric
- chi khac cach luu tru / nạp du lieu

## Prompt va rubric: phan nao nen de tieng Anh

Nen de tieng Anh cho:

- `difficulty definitions`
- `review checks`
- `difficulty mismatch signals`
- `acceptance rules`
- `regeneration hints`
- prompt runtime gui cho AI

Ly do:

- model doc on dinh hon
- giam mo ho
- de maintain tren nhieu provider/model

Co the de tieng Viet cho:

- tai lieu noi bo nhom
- ghi chu bao cao
- mo ta quy trinh

## Rubric va rule-based checks khac nhau the nao

### Rubric

Rubric la bo tieu chi de AI reviewer danh gia:

- pham vi kien thuc
- do kho
- chat luong cau hoi
- su phu hop voi mon hoc

### Rule-based checks

Rule-based checks la cac check bang code:

- schema dung hay sai
- `correct_answer` co hop le khong
- co field bat buoc khong
- co duplicate khong
- co source chunk id hop le khong

Can ca hai.

Rubric khong thay the duoc validation bang code.

## Huong nang cap tiep theo

### 1. Noi rubric vao code runtime

Hien tai bo rubric da duoc tao thanh JSON source.

Buoc tiep theo la:

- viet service doc rubric file
- `BuildReviewRubric(...)` khong hard-code nua
- render rubric JSON dong vao prompt

### 2. Them difficulty alignment logs

Nen log them:

- rubric id nao da duoc dung
- requested difficulty
- reviewer difficulty alignment notes

### 3. Them rubric versioning

Moi rubric nen co:

- `version`
- `updated_at`
- `updated_by`
- change log neu can

## Ket luan

Bo rubric nay duoc thiet ke de:

- la source of truth chung cho benchmark, prototype, va production
- giu prompt o dang maintainable
- nang chat luong review difficulty theo tung mon hoc va tung loai cau hoi

No khong phai prompt text don le.

No la mot he thong rubric co cau truc, co the render thanh prompt va co the version hoa ve sau.
