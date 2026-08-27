using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using MongoDB.Bson;

namespace DevDatabaseBootstrap;

public static class BootstrapManifest
{
    public const string FixtureVersion = "APE_WORKER_E2E_V1";
    public const string ApiUserSecretsId = "2258fc78-e0b1-4e21-928d-bac439bd07e0";
    public const int CLanguageId = 50;
    public const int JavaLanguageId = 62;
    public const string CSourceFilename = "main.c";
    public const string JavaSourceFilename = "Main.java";

    public const string AdminUserId = "64b100000000000000000001";
    public const string StudentUserId = "64b100000000000000000002";
    public const string CCourseId = "64b100000000000000000011";
    public const string JavaCourseId = "64b100000000000000000012";
    public const string CExamId = "64b100000000000000000021";
    public const string JavaExamId = "64b100000000000000000022";
    public const string CQuestionId = "64b100000000000000000031";
    public const string JavaQuestionId = "64b100000000000000000032";
    public const string CSessionId = "64b100000000000000000041";
    public const string JavaSessionId = "64b100000000000000000042";

    public static readonly DateTime FixtureCreatedAtUtc =
        new(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc);

    public static readonly DateTime FixtureModifiedAtUtc =
        new(2026, 7, 30, 0, 15, 0, DateTimeKind.Utc);

    public static readonly DateTime FixtureSessionStartUtc =
        new(2026, 7, 30, 0, 30, 0, DateTimeKind.Utc);

    public static readonly string[] DeterministicIds =
    [
        AdminUserId,
        StudentUserId,
        CCourseId,
        JavaCourseId,
        CExamId,
        JavaExamId,
        CQuestionId,
        JavaQuestionId,
        CSessionId,
        JavaSessionId
    ];

    public static FixtureBundle CreateFixtureBundle()
    {
        return new FixtureBundle(
            Admin: BuildAdminUser(),
            Student: BuildStudentUser(),
            CCourse: BuildCCourse(),
            JavaCourse: BuildJavaCourse(),
            CQuestion: BuildCQuestion(),
            JavaQuestion: BuildJavaQuestion(),
            CExam: BuildCExam(),
            JavaExam: BuildJavaExam(),
            CSession: BuildCSession(),
            JavaSession: BuildJavaSession());
    }

    public static PESubmissionInputDto CreateJavaWorkerSubmissionRequest()
    {
        return new PESubmissionInputDto
        {
            SessionId = JavaSessionId,
            QuestionId = JavaQuestionId,
            RequestedLanguageId = JavaLanguageId,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = JavaSourceFilename,
                    Content =
                        """
                        import java.util.Scanner;

                        public class Main {
                            public static void main(String[] args) {
                                Scanner scanner = new Scanner(System.in);
                                long value = scanner.nextLong();
                                System.out.print(value * value);
                            }
                        }
                        """
                }
            ]
        };
    }

    public static PESubmissionInputDto CreateJavaCompilationErrorSubmissionRequest()
    {
        return new PESubmissionInputDto
        {
            SessionId = JavaSessionId,
            QuestionId = JavaQuestionId,
            RequestedLanguageId = JavaLanguageId,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = JavaSourceFilename,
                    Content =
                        """
                        public class Main {
                            public static void main(String[] args) {
                                System.out.println("compile error")
                            }
                        }
                        """
                }
            ]
        };
    }

    public static PESubmissionInputDto CreateJavaWrongAnswerSubmissionRequest()
    {
        return new PESubmissionInputDto
        {
            SessionId = JavaSessionId,
            QuestionId = JavaQuestionId,
            RequestedLanguageId = JavaLanguageId,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = JavaSourceFilename,
                    Content =
                        """
                        import java.util.Scanner;

                        public class Main {
                            public static void main(String[] args) {
                                Scanner scanner = new Scanner(System.in);
                                long value = scanner.nextLong();
                                System.out.print(value + 1);
                            }
                        }
                        """
                }
            ]
        };
    }

    public static PESubmissionInputDto CreateJavaRuntimeErrorSubmissionRequest()
    {
        return new PESubmissionInputDto
        {
            SessionId = JavaSessionId,
            QuestionId = JavaQuestionId,
            RequestedLanguageId = JavaLanguageId,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = JavaSourceFilename,
                    Content =
                        """
                        import java.util.Scanner;

                        public class Main {
                            public static void main(String[] args) {
                                Scanner scanner = new Scanner(System.in);
                                scanner.nextLong();
                                throw new RuntimeException("intentional runtime failure");
                            }
                        }
                        """
                }
            ]
        };
    }

    public static PESubmissionInputDto CreateJavaTimeLimitSubmissionRequest()
    {
        return new PESubmissionInputDto
        {
            SessionId = JavaSessionId,
            QuestionId = JavaQuestionId,
            RequestedLanguageId = JavaLanguageId,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = JavaSourceFilename,
                    Content =
                        """
                        import java.util.Scanner;

                        public class Main {
                            public static void main(String[] args) {
                                Scanner scanner = new Scanner(System.in);
                                scanner.nextLong();

                                while (true) {
                                    // Intentional CPU loop for Time Limit Exceeded verification.
                                }
                            }
                        }
                        """
                }
            ]
        };
    }

    public static PESubmissionInputDto CreateCWorkerSubmissionRequest()
    {
        return new PESubmissionInputDto
        {
            SessionId = CSessionId,
            QuestionId = CQuestionId,
            RequestedLanguageId = CLanguageId,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = CSourceFilename,
                    Content =
                        """
                        #include <stdio.h>

                        int main(void)
                        {
                            long long first;
                            long long second;

                            if (scanf("%lld %lld", &first, &second) != 2)
                            {
                                return 1;
                            }

                            printf("%lld", first + second);
                            return 0;
                        }
                        """
                }
            ]
        };
    }

    public static bool IsDeterministicObjectId(string value) =>
        ObjectId.TryParse(value, out _);

    public static bool IsFixtureUser(User user) =>
        user.Id is AdminUserId or StudentUserId &&
        user.Email.Contains(FixtureVersion, StringComparison.OrdinalIgnoreCase);

    public static bool IsFixtureCourse(Course course) =>
        course.Id is CCourseId or JavaCourseId &&
        (course.Code.Contains(FixtureVersion, StringComparison.OrdinalIgnoreCase) ||
         course.Name.Contains(FixtureVersion, StringComparison.OrdinalIgnoreCase));

    public static bool IsFixtureQuestion(PEQuestion question) =>
        question.Id is CQuestionId or JavaQuestionId &&
        question.Title.Contains(FixtureVersion, StringComparison.OrdinalIgnoreCase) &&
        question.Description.Contains(FixtureVersion, StringComparison.OrdinalIgnoreCase);

    public static bool IsFixtureExam(Exam exam) =>
        exam.Id is CExamId or JavaExamId &&
        exam.Title.Contains(FixtureVersion, StringComparison.OrdinalIgnoreCase);

    public static bool IsFixtureSession(PracticeSession session) =>
        session.Id is CSessionId or JavaSessionId &&
        session.StudentId == StudentUserId &&
        (session.ExamId == CExamId || session.ExamId == JavaExamId);

    private static User BuildAdminUser() =>
        new()
        {
            Id = AdminUserId,
            Email = "ape_worker_e2e_v1.admin@local.invalid",
            FullName = $"{FixtureVersion} Admin",
            Role = "Admin",
            Status = "Active",
            GoogleId = null,
            AvatarUrl = null,
            ExpPoints = 0,
            AiWalletBalanceVnd = 0,
            CurrentStreak = 0,
            HighestStreak = 0,
            LastPracticeDate = null,
            Badges = [],
            RefreshTokens = [],
            CreatedAt = FixtureCreatedAtUtc
        };

    private static User BuildStudentUser() =>
        new()
        {
            Id = StudentUserId,
            Email = "ape_worker_e2e_v1.student@local.invalid",
            FullName = $"{FixtureVersion} Student",
            Role = "Student",
            Status = "Active",
            GoogleId = null,
            AvatarUrl = null,
            ExpPoints = 0,
            AiWalletBalanceVnd = 0,
            CurrentStreak = 0,
            HighestStreak = 0,
            LastPracticeDate = null,
            Badges = [],
            RefreshTokens = [],
            CreatedAt = FixtureCreatedAtUtc
        };

    private static Course BuildCCourse() =>
        new()
        {
            Id = CCourseId,
            Name = $"{FixtureVersion} PRF192 C Course",
            Code = $"{FixtureVersion}_PRF192_C",
            Description = $"{FixtureVersion} deterministic C worker fixture course.",
            SyllabusPath = null,
            ExamMatrix = [],
            Status = "Active",
            CreatedBy = AdminUserId,
            CreatedAt = FixtureCreatedAtUtc,
            LastModifiedBy = AdminUserId,
            LastModifiedAt = FixtureModifiedAtUtc
        };

    private static Course BuildJavaCourse() =>
        new()
        {
            Id = JavaCourseId,
            Name = $"{FixtureVersion} PRO192 Java Course",
            Code = $"{FixtureVersion}_PRO192_JAVA",
            Description = $"{FixtureVersion} deterministic Java worker fixture course.",
            SyllabusPath = null,
            ExamMatrix = [],
            Status = "Active",
            CreatedBy = AdminUserId,
            CreatedAt = FixtureCreatedAtUtc,
            LastModifiedBy = AdminUserId,
            LastModifiedAt = FixtureModifiedAtUtc
        };

    private static PEQuestion BuildCQuestion() =>
        new()
        {
            Id = CQuestionId,
            CourseId = CCourseId,
            SourceDocumentIds = [],
            TopicTags = ["c", "sum", FixtureVersion],
            Difficulty = "Easy",
            Title = $"{FixtureVersion} C Sum",
            Description = $"{FixtureVersion} read two integers and print their sum.",
            SkeletonCode =
            [
                new CodeFile
                {
                    Filename = CSourceFilename,
                    Content = "#include <stdio.h>\n\nint main(void)\n{\n    return 0;\n}\n",
                    IsReadOnly = false
                }
            ],
            SolutionCode = [],
            TestCases =
            [
                new TestCase
                {
                    Input = "1 2\n",
                    ExpectedOutput = "3\n",
                    IsSample = true,
                    IsHidden = false,
                    TimeLimitMs = 2000,
                    MemoryLimitKb = 65536
                },
                new TestCase
                {
                    Input = "-5 8\n",
                    ExpectedOutput = "3\n",
                    IsSample = false,
                    IsHidden = true,
                    TimeLimitMs = 2000,
                    MemoryLimitKb = 65536
                },
                new TestCase
                {
                    Input = "100 200\n",
                    ExpectedOutput = "300\n",
                    IsSample = false,
                    IsHidden = true,
                    TimeLimitMs = 2000,
                    MemoryLimitKb = 65536
                }
            ],
            Hints = [],
            AllowedLanguageIds = [CLanguageId],
            DefaultLanguageId = CLanguageId,
            Status = "Active",
            IsPublic = true,
            Source = "Admin",
            SourceScope = "SYSTEM",
            OwnerUserId = null,
            CreatedBy = AdminUserId,
            CreatedAt = FixtureCreatedAtUtc,
            QuestionFingerprint = null,
            LastModifiedBy = AdminUserId,
            LastModifiedAt = FixtureModifiedAtUtc,
            DisabledReason = null,
            DisabledBy = null,
            DisabledAt = null
        };

    private static PEQuestion BuildJavaQuestion() =>
        new()
        {
            Id = JavaQuestionId,
            CourseId = JavaCourseId,
            SourceDocumentIds = [],
            TopicTags = ["java", "square", FixtureVersion],
            Difficulty = "Easy",
            Title = $"{FixtureVersion} Java Square",
            Description = $"{FixtureVersion} read one integer and print its square.",
            SkeletonCode =
            [
                new CodeFile
                {
                    Filename = JavaSourceFilename,
                    Content = "public class Main {\n    public static void main(String[] args) {\n    }\n}\n",
                    IsReadOnly = false
                }
            ],
            SolutionCode = [],
            TestCases =
            [
                new TestCase
                {
                    Input = "2\n",
                    ExpectedOutput = "4\n",
                    IsSample = true,
                    IsHidden = false,
                    TimeLimitMs = 3000,
                    MemoryLimitKb = 131072
                },
                new TestCase
                {
                    Input = "-3\n",
                    ExpectedOutput = "9\n",
                    IsSample = false,
                    IsHidden = true,
                    TimeLimitMs = 3000,
                    MemoryLimitKb = 131072
                },
                new TestCase
                {
                    Input = "10\n",
                    ExpectedOutput = "100\n",
                    IsSample = false,
                    IsHidden = true,
                    TimeLimitMs = 3000,
                    MemoryLimitKb = 131072
                }
            ],
            Hints = [],
            AllowedLanguageIds = [JavaLanguageId],
            DefaultLanguageId = JavaLanguageId,
            Status = "Active",
            IsPublic = true,
            Source = "Admin",
            SourceScope = "SYSTEM",
            OwnerUserId = null,
            CreatedBy = AdminUserId,
            CreatedAt = FixtureCreatedAtUtc,
            QuestionFingerprint = null,
            LastModifiedBy = AdminUserId,
            LastModifiedAt = FixtureModifiedAtUtc,
            DisabledReason = null,
            DisabledBy = null,
            DisabledAt = null
        };

    private static Exam BuildCExam() =>
        new()
        {
            Id = CExamId,
            Title = $"{FixtureVersion} C Exam",
            CourseId = CCourseId,
            ExamType = "Mixed",
            Mode = ExamMode.Practice,
            TimeLimit = 60,
            CreatedBy = AdminUserId,
            Visibility = ExamVisibility.Public,
            FeExamQuestions = [],
            PeExamQuestions =
            [
                new PeExamQuestion
                {
                    PeQuestionId = CQuestionId,
                    AssignedPoints = 10
                }
            ],
            CreatedAt = FixtureCreatedAtUtc
        };

    private static Exam BuildJavaExam() =>
        new()
        {
            Id = JavaExamId,
            Title = $"{FixtureVersion} Java Exam",
            CourseId = JavaCourseId,
            ExamType = "Mixed",
            Mode = ExamMode.Practice,
            TimeLimit = 60,
            CreatedBy = AdminUserId,
            Visibility = ExamVisibility.Public,
            FeExamQuestions = [],
            PeExamQuestions =
            [
                new PeExamQuestion
                {
                    PeQuestionId = JavaQuestionId,
                    AssignedPoints = 10
                }
            ],
            CreatedAt = FixtureCreatedAtUtc
        };

    private static PracticeSession BuildCSession() =>
        new()
        {
            Id = CSessionId,
            StudentId = StudentUserId,
            ExamId = CExamId,
            StartTime = FixtureSessionStartUtc,
            EndTime = null,
            Status = SessionStatus.InProgress,
            TotalScore = 0,
            DraftCodes = []
        };

    private static PracticeSession BuildJavaSession() =>
        new()
        {
            Id = JavaSessionId,
            StudentId = StudentUserId,
            ExamId = JavaExamId,
            StartTime = FixtureSessionStartUtc,
            EndTime = null,
            Status = SessionStatus.InProgress,
            TotalScore = 0,
            DraftCodes = []
        };
}

public sealed record FixtureBundle(
    User Admin,
    User Student,
    Course CCourse,
    Course JavaCourse,
    PEQuestion CQuestion,
    PEQuestion JavaQuestion,
    Exam CExam,
    Exam JavaExam,
    PracticeSession CSession,
    PracticeSession JavaSession);
