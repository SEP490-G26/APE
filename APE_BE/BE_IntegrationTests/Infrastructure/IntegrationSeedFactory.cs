using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;

namespace BE_IntegrationTests.Infrastructure;

public sealed class IntegrationSeedFactory
{
    public const string CourseId = "64b0000000000000000000c1";
    public const string ActiveCourseId = "64b000000000000000000201";
    public const string InactiveCourseId = "64b000000000000000000202";
    public const string ExamId = "64b0000000000000000000e1";
    public const string StudentASessionId = "64b0000000000000000000d1";
    public const string StudentBSessionId = "64b0000000000000000000d2";
    public const string PeQuestionId = "64b0000000000000000000f1";
    public const string SystemFeQuestionEasyId = "64b000000000000000000101";
    public const string SystemFeQuestionMediumId = "64b000000000000000000102";
    public const string SystemPeQuestionId = "64b000000000000000000103";
    public const string ByosDocumentId = "64b000000000000000000104";
    public const string SystemDocumentId = "64b000000000000000000105";
    public const string ByosChunkId = "64b000000000000000000106";
    public const string PrivateByosFeQuestionId = "64b000000000000000000107";
    public const string PrivateByosPeQuestionId = "64b000000000000000000108";
    public const string PublicExamId = "64b000000000000000000109";
    public const string PrivateOwnedExamId = "64b00000000000000000010a";
    public const string AdminPracticeSetupExamId = "64b00000000000000000010b";
    public const string MixedExamId = "64b00000000000000000010c";
    public const string Batch2CourseId = "64b00000000000000000010d";
    public const string Batch2SystemDocumentId = "64b00000000000000000010e";
    public const string Batch2StudentADocumentId = "64b00000000000000000010f";
    public const string Batch2StudentBDocumentId = "64b000000000000000000110";
    public const string Batch2SystemDraftId = "64b000000000000000000111";
    public const string Batch2StudentADraftId = "64b000000000000000000112";
    public const string Batch2StudentBDraftId = "64b000000000000000000113";
    public const string Batch2SystemChunkId = "64b00000000000000000011b";
    public const string Batch2StudentAChunkId = "64b00000000000000000011c";
    public const string Batch2StudentBChunkId = "64b00000000000000000011d";
    public const string Batch2AdminFeQuestionId = "64b000000000000000000114";
    public const string Batch2AdminPeQuestionId = "64b000000000000000000115";
    public const string Batch2AdminDraftFeQuestionId = "64b000000000000000000116";
    public const string Batch2StudentAFeQuestionId = "64b000000000000000000117";
    public const string Batch2StudentAPeQuestionId = "64b000000000000000000118";
    public const string Batch2StudentADraftQuestionId = "64b000000000000000000119";
    public const string Batch2StudentBDraftQuestionId = "64b00000000000000000011a";

    private readonly DbContext _dbContext;

    public IntegrationSeedFactory(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedUsersAsync()
    {
        await _dbContext.Users.InsertManyAsync(
        [
            CreateUser(TestIdentity.Admin),
            CreateUser(TestIdentity.StudentA),
            CreateUser(TestIdentity.StudentB)
        ]);
    }

    public async Task<CourseScenario> SeedCourseScenarioAsync()
    {
        await SeedUsersAsync();

        var activeCourse = new Course
        {
            Id = ActiveCourseId,
            Code = "INT-COURSE-ACTIVE",
            Name = "Active Integration Course",
            Description = "Active course for integration tests",
            Status = "Active",
            CreatedBy = TestIdentity.Admin.UserId,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            ExamMatrix =
            [
                new ExamMatrixItem
                {
                    Difficulty = "Easy",
                    Count = 5,
                    PointsPerQuestion = 1
                }
            ]
        };

        var inactiveCourse = new Course
        {
            Id = InactiveCourseId,
            Code = "INT-COURSE-INACTIVE",
            Name = "Inactive Integration Course",
            Description = "Inactive course for integration tests",
            Status = "Inactive",
            CreatedBy = TestIdentity.Admin.UserId,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExamMatrix =
            [
                new ExamMatrixItem
                {
                    Difficulty = "Medium",
                    Count = 3,
                    PointsPerQuestion = 2
                }
            ]
        };

        await _dbContext.Courses.InsertManyAsync([activeCourse, inactiveCourse]);
        return new CourseScenario(activeCourse, inactiveCourse);
    }

    public async Task<string> SeedUserWithRefreshTokenAsync(
        TestIdentity identity,
        string refreshToken,
        DateTime? expiresUtc = null)
    {
        var user = CreateUser(
            identity,
            [
                new RefreshToken
                {
                    Token = refreshToken,
                    Expires = expiresUtc ?? DateTime.UtcNow.AddDays(7),
                    Created = DateTime.UtcNow.AddMinutes(-10),
                    CreatedByIp = "127.0.0.1"
                }
            ]);

        await _dbContext.Users.InsertOneAsync(user);
        return user.Id;
    }

    public async Task<ProgrammingPracticeScenario> SeedProgrammingPracticeScenarioAsync()
    {
        await SeedUsersAsync();

        var course = new Course
        {
            Id = CourseId,
            Name = "Integration Test Course",
            Code = "INT-PE-001",
            Description = "Integration course for PE pilots",
            Status = "Active",
            CreatedBy = TestIdentity.Admin.UserId,
            CreatedAt = DateTime.UtcNow
        };

        var question = new PEQuestion
        {
            Id = PeQuestionId,
            CourseId = course.Id,
            Title = "Echo input",
            Description = "Return stdin exactly as provided.",
            Status = "Active",
            AllowedLanguageIds = [50],
            DefaultLanguageId = 50,
            Source = "Admin",
            SourceScope = "SYSTEM",
            CreatedBy = TestIdentity.Admin.UserId,
            TestCases =
            [
                new TestCase
                {
                    Input = "ping",
                    ExpectedOutput = "ping",
                    IsHidden = false,
                    TimeLimitMs = 1000,
                    MemoryLimitKb = 262144
                },
                new TestCase
                {
                    Input = "hello",
                    ExpectedOutput = "hello",
                    IsHidden = true,
                    TimeLimitMs = 1000,
                    MemoryLimitKb = 262144
                }
            ],
            SkeletonCode =
            [
                CodeFile.Create("main.c", "int main(){return 0;}")
            ],
            SolutionCode =
            [
                CodeFile.Create("main.c", "int main(){return 0;}")
            ]
        };

        var exam = new Exam
        {
            Id = ExamId,
            Title = "Integration Practice Exam",
            CourseId = course.Id,
            ExamType = "PE",
            Mode = ExamMode.Practice,
            CreatedBy = TestIdentity.Admin.UserId,
            Visibility = ExamVisibility.Public,
            PresetScope = "Course",
            PeExamQuestions =
            [
                new PeExamQuestion
                {
                    PeQuestionId = question.Id,
                    AssignedPoints = 10
                }
            ]
        };

        var studentASession = new PracticeSession
        {
            Id = StudentASessionId,
            StudentId = TestIdentity.StudentA.UserId,
            ExamId = exam.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            LastResumedAt = DateTime.UtcNow.AddMinutes(-5),
            IsPaused = false,
            Status = SessionStatus.InProgress,
            TotalScore = 0
        };

        var studentBSession = new PracticeSession
        {
            Id = StudentBSessionId,
            StudentId = TestIdentity.StudentB.UserId,
            ExamId = exam.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-3),
            LastResumedAt = DateTime.UtcNow.AddMinutes(-3),
            IsPaused = false,
            Status = SessionStatus.InProgress,
            TotalScore = 0
        };

        await _dbContext.Courses.InsertOneAsync(course);
        await _dbContext.PEQuestions.InsertOneAsync(question);
        await _dbContext.Exams.InsertOneAsync(exam);
        await _dbContext.PracticeSessions.InsertManyAsync([studentASession, studentBSession]);

        return new ProgrammingPracticeScenario(course, exam, question, studentASession, studentBSession);
    }

    public async Task<ExamCatalogScenario> SeedExamCatalogScenarioAsync()
    {
        await SeedUsersAsync();

        var course = new Course
        {
            Id = CourseId,
            Name = "Integration Test Course",
            Code = "INT-CORE-001",
            Description = "Integration course for core student APIs",
            Status = "Active",
            CreatedBy = TestIdentity.Admin.UserId,
            CreatedAt = DateTime.UtcNow
        };

        var byosDocument = new Document
        {
            Id = ByosDocumentId,
            CourseId = course.Id,
            UserId = TestIdentity.StudentA.UserId,
            FileName = "student-a-byos.pdf",
            FilePath = "/integration/student-a-byos.pdf",
            FileType = "application/pdf",
            FileSizeBytes = 1024,
            Source = "BYOS",
            Status = DocumentStatus.Approved,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };

        var systemDocument = new Document
        {
            Id = SystemDocumentId,
            CourseId = course.Id,
            UserId = TestIdentity.Admin.UserId,
            FileName = "system-syllabus.pdf",
            FilePath = "/integration/system-syllabus.pdf",
            FileType = "application/pdf",
            FileSizeBytes = 2048,
            Source = "SystemSyllabus",
            Status = DocumentStatus.Approved,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-14)
        };

        var byosChunk = new KnowledgeChunk
        {
            Id = ByosChunkId,
            DocumentId = byosDocument.Id,
            CourseId = course.Id,
            UserId = TestIdentity.StudentA.UserId,
            ChunkIndex = 0,
            SubjectCode = course.Code,
            RawText = "BYOS chunk for owned question generation",
            WordCount = 7,
            TokenCount = 7,
            CharCount = 40,
            Status = "active",
            RetrievalEnabled = true
        };

        var systemFeEasy = CreateFeQuestion(
            SystemFeQuestionEasyId,
            course.Id,
            "System FE Easy",
            "Easy",
            ["Arrays"],
            isPublic: true,
            ownerUserId: null,
            source: "Admin",
            sourceScope: "SYSTEM");

        var systemFeMedium = CreateFeQuestion(
            SystemFeQuestionMediumId,
            course.Id,
            "System FE Medium",
            "Medium",
            ["Loops"],
            isPublic: true,
            ownerUserId: null,
            source: "Admin",
            sourceScope: "SYSTEM");

        var privateByosFe = CreateFeQuestion(
            PrivateByosFeQuestionId,
            course.Id,
            "BYOS FE Owned",
            "Easy",
            ["BYOS"],
            isPublic: false,
            ownerUserId: TestIdentity.StudentA.UserId,
            source: "BYOS",
            sourceScope: "PRIVATE",
            sourceDocumentIds: [byosDocument.Id],
            sourceChunkIds: [byosChunk.Id]);

        var systemPe = CreatePeQuestion(
            SystemPeQuestionId,
            course.Id,
            "System PE Public",
            "Medium",
            ["Algorithms"],
            isPublic: true,
            ownerUserId: null,
            source: "Admin",
            sourceScope: "SYSTEM");

        var privateByosPe = CreatePeQuestion(
            PrivateByosPeQuestionId,
            course.Id,
            "BYOS PE Owned",
            "Easy",
            ["BYOS"],
            isPublic: false,
            ownerUserId: TestIdentity.StudentA.UserId,
            source: "BYOS",
            sourceScope: "PRIVATE",
            sourceDocumentIds: [byosDocument.Id],
            sourceChunkIds: [byosChunk.Id]);

        var adminPracticeSetupExam = new Exam
        {
            Id = AdminPracticeSetupExamId,
            Title = "Admin Practice Setup FE",
            CourseId = course.Id,
            ExamType = "FE",
            Mode = ExamMode.Practice,
            CreatedBy = TestIdentity.Admin.UserId,
            Visibility = ExamVisibility.Public,
            PresetScope = "Course",
            FeExamQuestions = [systemFeEasy.Id, systemFeMedium.Id],
            CreatedAt = DateTime.UtcNow.AddMinutes(-12)
        };

        var publicExam = new Exam
        {
            Id = PublicExamId,
            Title = "Student Public FE Exam",
            CourseId = course.Id,
            ExamType = "FE",
            Mode = ExamMode.Practice,
            CreatedBy = TestIdentity.Admin.UserId,
            Visibility = ExamVisibility.Public,
            PresetScope = "Course",
            FeExamQuestions = [systemFeEasy.Id],
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };

        var privateOwnedExam = new Exam
        {
            Id = PrivateOwnedExamId,
            Title = "Student A Private BYOS FE Exam",
            CourseId = course.Id,
            ExamType = "FE",
            Mode = ExamMode.Practice,
            CreatedBy = TestIdentity.StudentA.UserId,
            Visibility = ExamVisibility.Private,
            PresetScope = "Course",
            FeExamQuestions = [privateByosFe.Id],
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var mixedExam = new Exam
        {
            Id = MixedExamId,
            Title = "Mixed FE PE Exam",
            CourseId = course.Id,
            ExamType = "Mixed",
            Mode = ExamMode.Practice,
            CreatedBy = TestIdentity.Admin.UserId,
            Visibility = ExamVisibility.Public,
            PresetScope = "Course",
            FeExamQuestions = [systemFeEasy.Id],
            PeExamQuestions =
            [
                new PeExamQuestion
                {
                    PeQuestionId = systemPe.Id,
                    AssignedPoints = 10
                }
            ],
            CreatedAt = DateTime.UtcNow.AddMinutes(-9)
        };

        await _dbContext.Courses.InsertOneAsync(course);
        await _dbContext.Documents.InsertManyAsync([byosDocument, systemDocument]);
        await _dbContext.KnowledgeChunks.InsertOneAsync(byosChunk);
        await _dbContext.FEQuestions.InsertManyAsync([systemFeEasy, systemFeMedium, privateByosFe]);
        await _dbContext.PEQuestions.InsertManyAsync([systemPe, privateByosPe]);
        await _dbContext.Exams.InsertManyAsync([adminPracticeSetupExam, publicExam, privateOwnedExam, mixedExam]);

        return new ExamCatalogScenario(
            course,
            byosDocument,
            systemDocument,
            byosChunk,
            systemFeEasy,
            systemFeMedium,
            privateByosFe,
            systemPe,
            privateByosPe,
            adminPracticeSetupExam,
            publicExam,
            privateOwnedExam,
            mixedExam);
    }

    public async Task<Batch2KnowledgeScenario> SeedBatch2KnowledgeScenarioAsync()
    {
        await SeedUsersAsync();

        var course = new Course
        {
            Id = Batch2CourseId,
            Name = "Batch 2 Integration Course",
            Code = "PRN212",
            Description = "Shared Batch 2 integration course",
            Status = "Active",
            CreatedBy = TestIdentity.Admin.UserId,
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };

        var systemDocument = CreateDocument(
            Batch2SystemDocumentId,
            course.Id,
            TestIdentity.Admin.UserId,
            "system-queues.txt",
            "integration-storage/system-queues.txt",
            ".txt",
            "SystemSyllabus",
            Batch2SystemDraftId);

        var studentADocument = CreateDocument(
            Batch2StudentADocumentId,
            course.Id,
            TestIdentity.StudentA.UserId,
            "student-a-queues.txt",
            "integration-storage/student-a-queues.txt",
            ".txt",
            "BYOS",
            Batch2StudentADraftId);

        var studentBDocument = CreateDocument(
            Batch2StudentBDocumentId,
            course.Id,
            TestIdentity.StudentB.UserId,
            "student-b-queues.txt",
            "integration-storage/student-b-queues.txt",
            ".txt",
            "BYOS",
            Batch2StudentBDraftId);

        var systemDraft = CreateDraft(Batch2SystemDraftId, systemDocument.Id, TestIdentity.Admin.UserId, course.Id, "system_seeded");
        var studentADocumentDraft = CreateDraft(Batch2StudentADraftId, studentADocument.Id, TestIdentity.StudentA.UserId, course.Id, "byos");
        var studentBDocumentDraft = CreateDraft(Batch2StudentBDraftId, studentBDocument.Id, TestIdentity.StudentB.UserId, course.Id, "byos");

        var systemChunk = CreateChunk(Batch2SystemChunkId, systemDocument.Id, course.Id, TestIdentity.Admin.UserId, "Queue enqueue dequeue stacks linked list.");
        var studentAChunk = CreateChunk(Batch2StudentAChunkId, studentADocument.Id, course.Id, TestIdentity.StudentA.UserId, "Student A BYOS queue and stack notes.");
        var studentBChunk = CreateChunk(Batch2StudentBChunkId, studentBDocument.Id, course.Id, TestIdentity.StudentB.UserId, "Student B BYOS queue notes.");

        var adminFe = CreateFeQuestion(
            Batch2AdminFeQuestionId,
            course.Id,
            "Batch2 Admin FE Active",
            "Easy",
            ["queues", "stacks"],
            isPublic: true,
            ownerUserId: null,
            source: "Admin",
            sourceScope: "SYSTEM");

        var adminPe = CreatePeQuestion(
            Batch2AdminPeQuestionId,
            course.Id,
            "Batch2 Admin PE Active",
            "Medium",
            ["queues"],
            isPublic: true,
            ownerUserId: null,
            source: "Admin",
            sourceScope: "SYSTEM");

        var adminDraftFe = CreateFeQuestion(
            Batch2AdminDraftFeQuestionId,
            course.Id,
            "Batch2 Admin FE Draft",
            "Easy",
            ["queues"],
            isPublic: false,
            ownerUserId: null,
            source: "Admin",
            sourceScope: "SYSTEM");
        adminDraftFe.Status = "Draft";

        var studentAFe = CreateFeQuestion(
            Batch2StudentAFeQuestionId,
            course.Id,
            "Batch2 Student A FE Active",
            "Easy",
            ["queues"],
            isPublic: false,
            ownerUserId: TestIdentity.StudentA.UserId,
            source: "Student",
            sourceScope: "BYOS",
            sourceDocumentIds: [studentADocument.Id],
            sourceChunkIds: [studentAChunk.Id]);

        var studentAPe = CreatePeQuestion(
            Batch2StudentAPeQuestionId,
            course.Id,
            "Batch2 Student A PE Active",
            "Easy",
            ["queues"],
            isPublic: false,
            ownerUserId: TestIdentity.StudentA.UserId,
            source: "Student",
            sourceScope: "BYOS",
            sourceDocumentIds: [studentADocument.Id],
            sourceChunkIds: [studentAChunk.Id]);

        var studentADraftQuestion = CreateFeQuestion(
            Batch2StudentADraftQuestionId,
            course.Id,
            "Batch2 Student A FE Draft",
            "Medium",
            ["stacks"],
            isPublic: false,
            ownerUserId: TestIdentity.StudentA.UserId,
            source: "Student",
            sourceScope: "BYOS",
            sourceDocumentIds: [studentADocument.Id],
            sourceChunkIds: [studentAChunk.Id]);
        studentADraftQuestion.Status = "Draft";

        var studentBDraftQuestion = CreateFeQuestion(
            Batch2StudentBDraftQuestionId,
            course.Id,
            "Batch2 Student B FE Draft",
            "Medium",
            ["queues"],
            isPublic: false,
            ownerUserId: TestIdentity.StudentB.UserId,
            source: "Student",
            sourceScope: "BYOS",
            sourceDocumentIds: [studentBDocument.Id],
            sourceChunkIds: [studentBChunk.Id]);
        studentBDraftQuestion.Status = "Draft";

        await _dbContext.Courses.InsertOneAsync(course);
        await _dbContext.Documents.InsertManyAsync([systemDocument, studentADocument, studentBDocument]);
        await _dbContext.AIExtractionDrafts.InsertManyAsync([systemDraft, studentADocumentDraft, studentBDocumentDraft]);
        await _dbContext.KnowledgeChunks.InsertManyAsync([systemChunk, studentAChunk, studentBChunk]);
        await _dbContext.FEQuestions.InsertManyAsync([adminFe, adminDraftFe, studentAFe, studentADraftQuestion, studentBDraftQuestion]);
        await _dbContext.PEQuestions.InsertManyAsync([adminPe, studentAPe]);

        return new Batch2KnowledgeScenario(
            course,
            systemDocument,
            studentADocument,
            studentBDocument,
            systemDraft,
            studentADocumentDraft,
            studentBDocumentDraft,
            systemChunk,
            studentAChunk,
            studentBChunk,
            adminFe,
            adminPe,
            adminDraftFe,
            studentAFe,
            studentAPe,
            studentADraftQuestion,
            studentBDraftQuestion);
    }

    private static FEQuestion CreateFeQuestion(
        string id,
        string courseId,
        string title,
        string difficulty,
        List<string> topicTags,
        bool isPublic,
        string? ownerUserId,
        string source,
        string sourceScope,
        List<string>? sourceDocumentIds = null,
        List<string>? sourceChunkIds = null)
    {
        return new FEQuestion
        {
            Id = id,
            CourseId = courseId,
            Title = title,
            Description = $"{title} description",
            Difficulty = difficulty,
            TopicTags = topicTags,
            Options = ["A", "B", "C", "D"],
            CorrectAnswer = ["A"],
            Explanation = $"{title} explanation",
            Status = "Active",
            Source = source,
            SourceScope = sourceScope,
            OwnerUserId = ownerUserId,
            CreatedBy = ownerUserId ?? TestIdentity.Admin.UserId,
            IsPublic = isPublic,
            SourceDocumentIds = sourceDocumentIds ?? new List<string>(),
            LegacySourceChunkIds = sourceChunkIds ?? new List<string>()
        };
    }

    private static PEQuestion CreatePeQuestion(
        string id,
        string courseId,
        string title,
        string difficulty,
        List<string> topicTags,
        bool isPublic,
        string? ownerUserId,
        string source,
        string sourceScope,
        List<string>? sourceDocumentIds = null,
        List<string>? sourceChunkIds = null)
    {
        return new PEQuestion
        {
            Id = id,
            CourseId = courseId,
            Title = title,
            Description = $"{title} description",
            Difficulty = difficulty,
            TopicTags = topicTags,
            Status = "Active",
            IsPublic = isPublic,
            Source = source,
            SourceScope = sourceScope,
            OwnerUserId = ownerUserId,
            CreatedBy = ownerUserId ?? TestIdentity.Admin.UserId,
            SourceDocumentIds = sourceDocumentIds ?? [],
            LegacyChunkIds = sourceChunkIds ?? [],
            AllowedLanguageIds = [50],
            DefaultLanguageId = 50,
            SkeletonCode =
            [
                CodeFile.Create("main.c", "int main(){return 0;}")
            ],
            SolutionCode =
            [
                CodeFile.Create("main.c", "int main(){return 0;}")
            ],
            TestCases =
            [
                new TestCase
                {
                    Input = "ping",
                    ExpectedOutput = "ping",
                    IsHidden = false,
                    TimeLimitMs = 1000,
                    MemoryLimitKb = 262144
                }
            ]
        };
    }

    private static User CreateUser(
        TestIdentity identity,
        List<RefreshToken>? refreshTokens = null)
    {
        return new User
        {
            Id = identity.UserId,
            GoogleId = $"it-{identity.UserId}",
            Email = identity.Email,
            FullName = identity.FullName,
            Role = identity.Role,
            Status = "Active",
            AvatarUrl = $"https://example.test/{identity.UserId}.png",
            AiWalletBalanceVnd = 500_000,
            CurrentStreak = 1,
            HighestStreak = 1,
            LastLoginDate = DateTime.UtcNow.AddDays(-1),
            RefreshTokens = refreshTokens ?? new List<RefreshToken>()
        };
    }

    private static Document CreateDocument(
        string id,
        string courseId,
        string userId,
        string fileName,
        string filePath,
        string fileType,
        string source,
        string draftId)
    {
        return new Document
        {
            Id = id,
            CourseId = courseId,
            UserId = userId,
            FileName = fileName,
            FilePath = filePath,
            FileType = fileType,
            FileSizeBytes = 512,
            FileChecksum = $"{id}-checksum",
            NormalizedContentChecksum = $"{id}-normalized",
            Source = source,
            SubjectCode = "PRN212",
            GatekeeperVerdict = "supported",
            LastExtractionDraftId = draftId,
            Status = DocumentStatus.Completed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            LastExtractedAt = DateTime.UtcNow.AddDays(-3),
            LastEmbeddedAt = DateTime.UtcNow.AddDays(-3),
            ChapterSummaries =
            [
                new DocumentChapterSummary
                {
                    ChapterKey = "chapter-1",
                    ChapterTitle = "Controlled Chapter 1",
                    ChapterOrder = 1,
                    ChunkCount = 1,
                    EstimatedTokens = 64,
                    CoveredTopics = ["queues", "stacks"],
                    SampleSectionTitles = ["Controlled Section"],
                    OverviewShort = "Controlled chapter overview."
                }
            ]
        };
    }

    private static AIExtractionDraft CreateDraft(
        string id,
        string documentId,
        string userId,
        string courseId,
        string ownershipType)
    {
        return new AIExtractionDraft
        {
            Id = id,
            DocumentId = documentId,
            UserId = userId,
            CourseId = courseId,
            SourceType = "txt",
            SourceName = $"{documentId}.txt",
            SourceStoragePath = $"integration-storage/{documentId}.txt",
            SourceSizeBytes = 512,
            SourceChecksum = $"{documentId}-checksum",
            Language = "en",
            SubjectCode = "PRN212",
            OwnershipType = ownershipType,
            IngestParser = "controlled-parser",
            IngestParserVersion = "v1",
            ExtractionMode = "text_only",
            ReviewStatus = "auto_ingested",
            RawTextPreview = "Queues and stacks preview.",
            CleanMarkdownPreview = "## Controlled Document\nQueues and stacks preview.",
            ApprovedMarkdownPreview = "## Controlled Document\nQueues and stacks preview.",
            TotalPages = 1,
            TotalSegments = 1,
            ApprovedSegments = 1,
            RejectedSegments = 0,
            ChunkingReady = true,
            ChunkCount = 1,
            LastEmbeddingRunId = $"embed-{documentId}",
            CandidateTitles = ["Controlled Document"],
            CandidateChapterMarkers = ["chapter-1"],
            CleanDisplayTitleCandidates = ["Controlled Document"],
            ChapterSummaries =
            [
                new DocumentChapterSummary
                {
                    ChapterKey = "chapter-1",
                    ChapterTitle = "Controlled Chapter 1",
                    ChapterOrder = 1,
                    ChunkCount = 1,
                    EstimatedTokens = 64,
                    CoveredTopics = ["queues", "stacks"],
                    SampleSectionTitles = ["Controlled Section"],
                    OverviewShort = "Controlled chapter overview."
                }
            ],
            CreditChargeStatus = "waived",
            CreditChargeAmount = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        };
    }

    private static KnowledgeChunk CreateChunk(
        string id,
        string documentId,
        string courseId,
        string userId,
        string rawText)
    {
        var expandedText = string.Join(
            " ",
            Enumerable.Repeat(
                $"{rawText} Queues support enqueue, dequeue, peek, front, rear, traversal, linked implementation, array implementation, overflow handling, underflow handling, and FIFO reasoning for data-structure exercises.",
                6));

        return new KnowledgeChunk
        {
            Id = id,
            DocumentId = documentId,
            CourseId = courseId,
            UserId = userId,
            ChunkIndex = 0,
            SourceType = "txt",
            SourceName = $"{documentId}.txt",
            SubjectCode = "PRN212",
            ChapterKey = "chapter-1",
            ChapterTitle = "Controlled Chapter 1",
            ChapterOrder = 1,
            SectionTitle = "Controlled Section",
            TopicTags = ["queues", "stacks"],
            RawText = expandedText,
            ContentText = expandedText,
            MarkdownText = expandedText,
            NormalizedText = expandedText,
            WordCount = expandedText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
            TokenCount = Math.Max(220, expandedText.Length / 4),
            CharCount = expandedText.Length,
            Status = "active",
            RetrievalEnabled = true,
            SourcePageFrom = 1,
            SourcePageTo = 1
        };
    }
}

public sealed record ProgrammingPracticeScenario(
    Course Course,
    Exam Exam,
    PEQuestion Question,
    PracticeSession StudentASession,
    PracticeSession StudentBSession);

public sealed record ExamCatalogScenario(
    Course Course,
    Document ByosDocument,
    Document SystemDocument,
    KnowledgeChunk ByosChunk,
    FEQuestion SystemFeEasy,
    FEQuestion SystemFeMedium,
    FEQuestion PrivateByosFe,
    PEQuestion SystemPe,
    PEQuestion PrivateByosPe,
    Exam AdminPracticeSetupExam,
    Exam PublicExam,
    Exam PrivateOwnedExam,
    Exam MixedExam);

public sealed record CourseScenario(
    Course ActiveCourse,
    Course InactiveCourse);

public sealed record Batch2KnowledgeScenario(
    Course Course,
    Document SystemDocument,
    Document StudentADocument,
    Document StudentBDocument,
    AIExtractionDraft SystemDraft,
    AIExtractionDraft StudentADraft,
    AIExtractionDraft StudentBDraft,
    KnowledgeChunk SystemChunk,
    KnowledgeChunk StudentAChunk,
    KnowledgeChunk StudentBChunk,
    FEQuestion AdminFeQuestion,
    PEQuestion AdminPeQuestion,
    FEQuestion AdminDraftFeQuestion,
    FEQuestion StudentAFeQuestion,
    PEQuestion StudentAPeQuestion,
    FEQuestion StudentADraftQuestion,
    FEQuestion StudentBDraftQuestion);
