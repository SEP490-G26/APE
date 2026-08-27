# AI Code Mentor Design

## Muc tieu

`AI Code Mentor` co vai tro:

- nhan de bai va doan code sinh vien nop
- phan tich loi ky thuat co the suy ra tu code
- tra ve feedback co cau truc de sinh vien biet sai o dau va nen sua gi

## Scope hien tai

Prototype hien tai:

- khong compile/run code
- khong ket luan dua tren ket qua judge that
- chi review dua tren de bai + code + rubric/policy

Compile/run verification se de cho Judge0 hoac pipeline execution sau nay.

## Policy runtime

Policy hien tai duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/code-mentor-policy.json`

Policy chua:

- supported languages
- issue categories
- verdict set
- feedback rules

## Rubric va ground truth

Rubric nhe:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/CODE_MENTOR.json`

Ground truth core:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/CODE_MENTOR_CORE.json`

## Prompt

Prompt hien tai duoc luu tai:

- `src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json`

Da nang cap de:

- nhan policy json
- nhan rubric json
- tra JSON only
- tach ro verdict, categories, issues, suggestions, failing scenarios, confidence, complexity

## Route da co

- `POST /api/ai-module/code-mentor`
- `POST /api/ai-module/code-mentor/debug`
- `GET /api/ai-module/code-mentor/policy`
- `GET /api/ai-module/code-mentor/rubric`
- `GET /api/ai-module/code-mentor/ground-truth`

## Output chuan hoa

Mentor feedback hien tai gom:

- `verdict`
- `issueCategories`
- `issues`
- `suggestions`
- `failingScenarios`
- `confidence`
- `complexity`
- `modelName`

## Ghi chu nghiep vu

Mentor nen:

- noi dung cu the
- tranh phe binh mo ho
- de xuat cach sua ro rang
- khong khang dinh da run code neu chua run

Neu sau nay ghep Judge0:

- Mentor co the nhan them compile result / runtime result / failed test cases
- khi do chat luong feedback se tang ro ret
