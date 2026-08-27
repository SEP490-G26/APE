# BE Code Structure

Tai lieu nay mo ta structure code hien tai cua `APE_BE`, tap trung vao source code thuc te va bo qua `bin/`, `obj/`.

## 1. Tong quan solution

Solution hien tai duoc to chuc theo 5 khoi chinh:

- `API`
  - entrypoint web app
  - controllers, middleware, worker, DI/config

- `Application`
  - DTOs
  - interfaces
  - service layer
  - options
  - validation

- `Domain`
  - entities
  - enums
  - core business data model

- `Infrastructure`
  - repository implementations
  - MongoDB context
  - auth integrations
  - AI provider clients

- `BE_UnitTests`
  - backend unit tests

## 2. Thu muc goc `APE_BE`

```text
APE_BE/
├─ API/
├─ Application/
├─ BE_UnitTests/
├─ Doc/
├─ Domain/
├─ Infrastructure/
├─ APE_Core.sln
├─ PROJECT_CONTEXT.md
├─ PROJECT_MEMORY.txt
├─ README.md
├─ README.txt
├─ smoke-test.ps1
└─ copilot-instructions.txt
```

## 3. API layer

```text
API/
├─ Controllers/
│  ├─ AuthController.cs
│  ├─ CourseController.cs
│  ├─ DocumentController.cs
│  ├─ ExamController.cs
│  ├─ QuestionController.cs
│  ├─ SubmissionController.cs
│  └─ UserController.cs
├─ Middlewares/
│  └─ ExceptionMiddleware.cs
├─ Properties/
│  └─ launchSettings.json
├─ Workers/
│  └─ Judge0Worker.cs
├─ API.csproj
├─ Program.cs
├─ appsettings.json
├─ appsettings.Development.json
├─ Dockerfile
└─ APE_Core.http
```

Vai tro:
- `Program.cs`
  - DI registration
  - auth
  - CORS
  - Swagger
  - rate limiting
  - AI provider registration
  - MongoDB + worker bootstrap

- `Controllers`
  - expose REST API cho auth, course, document, exam, question, submission, user

- `Workers/Judge0Worker.cs`
  - background polling worker cho PE submission grading

## 4. Application layer

```text
Application/
├─ Common/
│  └─ ApiResponse.cs
├─ DTOs/
│  ├─ AIDtos.cs
│  ├─ AuthDtos.cs
│  ├─ CourseDtos.cs
│  ├─ CreateExamDto.cs
│  ├─ DocumentDtos.cs
│  ├─ ExamDtos.cs
│  ├─ Judge0Dto.cs
│  ├─ PESubmissionDto.cs
│  ├─ PracticeDtos.cs
│  ├─ QuestionDtos.cs
│  ├─ SubmissionDtos.cs
│  └─ UserProfileDto.cs
├─ Enums/
│  └─ SubmissionType.cs
├─ Exceptions/
│  ├─ ConflictException.cs
│  ├─ ForbiddenException.cs
│  ├─ NotFoundException.cs
│  ├─ UnauthorizedException.cs
│  └─ ValidationException.cs
├─ Interfaces/
│  ├─ IAIClientFactory.cs
│  ├─ IAIEmbeddingClient.cs
│  ├─ IAIEmbeddingTaggingService.cs
│  ├─ IAIExtractedContentService.cs
│  ├─ IAIFeatureRoutingService.cs
│  ├─ IAIGatekeeperService.cs
│  ├─ IAIPromptService.cs
│  ├─ IAITextClient.cs
│  ├─ IAIUsageLogRepository.cs
│  ├─ ICourseRepository.cs
│  ├─ IDocumentRepository.cs
│  ├─ IExamRepository.cs
│  ├─ IFEQuestionRepository.cs
│  ├─ IFESubmissionRepository.cs
│  ├─ IFileExtractionService.cs
│  ├─ IGoogleAuthService.cs
│  ├─ IJwtTokenGenerator.cs
│  ├─ IKnowledgeChunkRepository.cs
│  ├─ IPEQuestionRepository.cs
│  ├─ IPESubmissionRepository.cs
│  ├─ IPracticeSessionRepository.cs
│  └─ IUserRepository.cs
├─ Options/
│  ├─ AIOptions.cs
│  └─ Judge0Options.cs
├─ Services/
│  ├─ AIClientFactory.cs
│  ├─ AIEmbeddingTaggingService.cs
│  ├─ AIExtractedContentService.cs
│  ├─ AIFeatureRoutingService.cs
│  ├─ AIGatekeeperService.cs
│  ├─ AIPromptService.cs
│  ├─ AuthService.cs
│  ├─ DocumentService.cs
│  ├─ ExamService.cs
│  ├─ FESubmissionService.cs
│  ├─ FileExtractionService.cs
│  ├─ Judge0Service.cs
│  ├─ PESubmissionService.cs
│  ├─ PracticeService.cs
│  └─ UserService.cs
├─ Validators/
│  ├─ StartSessionDtoValidator.cs
│  └─ SubmissionValidators.cs
└─ Application.csproj
```

Vai tro:
- `Interfaces`
  - hop dong giua service va infrastructure
  - hop dong AI abstraction va repository abstraction

- `Services`
  - business logic layer
  - orchestration document ingestion
  - auth / exam / submission / practice
  - AI routing, gatekeeper, extracted content, embedding+tagging

- `Options`
  - bind config tu `appsettings`
  - `AIOptions.cs` la trung tam cho `Providers + Features`

## 5. Domain layer

```text
Domain/
├─ Entities/
│  ├─ AIAgent.cs
│  ├─ AIConfig.cs
│  ├─ AIKnowledgeAssessment.cs
│  ├─ AIMentorFeedback.cs
│  ├─ AIUsageLog.cs
│  ├─ CommonTypes.cs
│  ├─ Course.cs
│  ├─ CreditPackage.cs
│  ├─ DailyPracticeSummary.cs
│  ├─ Document.cs
│  ├─ Exam.cs
│  ├─ ExamAttemptSnapshot.cs
│  ├─ FE_Submission.cs
│  ├─ FEQuestion.cs
│  ├─ KnowledgeChunk.cs
│  ├─ KnowlegeChunk_QuestionBank.cs
│  ├─ Payment.cs
│  ├─ PE_Submission.cs
│  ├─ PEQuestion.cs
│  ├─ PracticeSession.cs
│  ├─ Prompt.cs
│  ├─ QuestionBank_Exam.cs
│  ├─ Repost.cs
│  ├─ SystemSetting.cs
│  ├─ User.cs
│  └─ UserAnalytics.cs
├─ Enums/
│  ├─ AIAgentRole.cs
│  ├─ AIProvider.cs
│  ├─ Difficulty.cs
│  ├─ DocumentStatus.cs
│  ├─ ExamMode.cs
│  ├─ ExamVisibility.cs
│  ├─ LeaderboardPeriod.cs
│  ├─ PaymentStatus.cs
│  ├─ PaymentType.cs
│  ├─ QuestionType.cs
│  ├─ ReportIssueCategory.cs
│  ├─ ReportStatus.cs
│  ├─ ReportTargetType.cs
│  ├─ SessionStatus.cs
│  ├─ SettingKey.cs
│  ├─ SubmissionStatus.cs
│  └─ UserRole.cs
└─ Domain.csproj
```

Vai tro:
- chua model nghiep vu goc
- khong chua repository implementation
- `KnowledgeChunk`, `Prompt`, `AIUsageLog`, `AIAgent`, `AIConfig` la cac entity quan trong cho AI pipeline

## 6. Infrastructure layer

```text
Infrastructure/
├─ AI/
│  ├─ AIClientJsonHelpers.cs
│  ├─ CohereEmbeddingClient.cs
│  ├─ CohereTextClient.cs
│  ├─ DisabledAIEmbeddingClient.cs
│  ├─ DisabledAITextClient.cs
│  ├─ GeminiTextClient.cs
│  └─ OpenAITextClient.cs
├─ Auth/
│  ├─ GoogleAuthService.cs
│  └─ JwtTokenGenerator.cs
├─ Data/
│  ├─ DbContext.cs
│  └─ DbInitializer.cs
├─ Persistence/
│  ├─ AIUsageLogRepository.cs
│  ├─ CourseRepository.cs
│  ├─ DocumentRepository.cs
│  ├─ ExamRepository.cs
│  ├─ FEQuestionRepository.cs
│  ├─ FESubmissionRepository.cs
│  ├─ KnowledgeChunkRepository.cs
│  ├─ PEQuestionRepository.cs
│  ├─ PESubmissionRepository.cs
│  ├─ PracticeSessionRepository.cs
│  └─ UserRepository.cs
└─ Infrastructure.csproj
```

Vai tro:
- `AI/`
  - provider clients cho `OpenAI`, `Gemini`, `Cohere`
  - helper parse JSON AI responses

- `Auth/`
  - Google login verification
  - JWT token generation

- `Data/`
  - MongoDB context va seed/index initialization

- `Persistence/`
  - repository implementation cho Application interfaces

## 7. Test layer

```text
BE_UnitTests/
├─ BE_UnitTests.csproj
├─ FESubmissionServiceTests.cs
├─ SubmissionControllerTests.cs
├─ UnitTest1.cs
└─ packages.config
```

Trang thai:
- da co mot so test cho submission flow
- van con dau vet migration cu (`packages.config`, backup tmp)

## 8. AI-related code map

Nhung file AI quan trong hien tai:

### 8.1 AI configuration
- `Application/Options/AIOptions.cs`
- `API/appsettings.json`

### 8.2 AI abstraction
- `Application/Interfaces/IAITextClient.cs`
- `Application/Interfaces/IAIEmbeddingClient.cs`
- `Application/Interfaces/IAIClientFactory.cs`
- `Application/Interfaces/IAIFeatureRoutingService.cs`
- `Application/Interfaces/IAIExtractedContentService.cs`
- `Application/Interfaces/IAIEmbeddingTaggingService.cs`

### 8.3 AI orchestration services
- `Application/Services/AIGatekeeperService.cs`
- `Application/Services/AIExtractedContentService.cs`
- `Application/Services/AIEmbeddingTaggingService.cs`
- `Application/Services/AIFeatureRoutingService.cs`
- `Application/Services/DocumentService.cs`

### 8.4 AI provider clients
- `Infrastructure/AI/OpenAITextClient.cs`
- `Infrastructure/AI/GeminiTextClient.cs`
- `Infrastructure/AI/CohereTextClient.cs`
- `Infrastructure/AI/CohereEmbeddingClient.cs`

### 8.5 AI persistence entities
- `Domain/Entities/KnowledgeChunk.cs`
- `Domain/Entities/Prompt.cs`
- `Domain/Entities/AIAgent.cs`
- `Domain/Entities/AIConfig.cs`
- `Domain/Entities/AIUsageLog.cs`
- `Domain/Entities/AIMentorFeedback.cs`
- `Domain/Entities/AIKnowledgeAssessment.cs`

## 9. Luong code backend hien tai

Backend hien tai co the hieu theo luong:

1. `Controller`
   - nhan HTTP request

2. `Application Service`
   - xu ly business flow
   - goi repository / AI service / Judge0 service

3. `Infrastructure`
   - goi MongoDB
   - goi provider ngoai (`Google`, `Judge0`, `OpenAI`, `Gemini`, `Cohere`)

4. `Domain`
   - object va enum duoc chia se giua cac layer

## 10. Diem can nho khi tiep tuc phat trien

- `Document ingestion` da duoc noi voi `Gatekeeper`, `Extracted Content`, `Embedding + AutoTagging`.
- `PDF extraction` hien van o muc placeholder; `txt/docx` da co extraction that.
- `AI config` da tach `Providers` va `Features`, phu hop cho routing linh hoat va FE override sau nay.
- `Generation`, `Review`, `Mentor`, `Study Advice` chua duoc noi day du theo workflow SRS.
- `Prompt management`, `credit deduction/rollback`, `cost tracking`, va `user-level custom AI model override` van can implementation tiep.

## 11. Tai lieu lien quan trong `Doc`

- `Doc/SRS_Project_Context.md`
- `Doc/AI_Functions_Real_Project_Summary.md`
- `Doc/AI_Backend_Update_Log.md`
- `Doc/BE_Code_Structure.md`
