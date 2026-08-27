using Application;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net;
using System.Linq.Expressions;

namespace DevDatabaseBootstrap;

internal sealed class BootstrapRunner
{
    private static readonly string[] RequiredPESubmissionIndexNames =
    [
        "ux_pe_submission_session_question_attempt",
        "ix_pe_submission_session_submitted_at",
        "ix_pe_submission_status_submitted_at"
    ];

    private readonly DbContext _context;
    private readonly IReadOnlyList<ProductionCollectionInfo> _collections;
    public BootstrapRunner(DbContext context)
    {
        _context = context;
        _collections = BootstrapIntrospection.GetProductionCollections(context);
    }

    public async Task<int> InspectAsync(CancellationToken cancellationToken)
    {
        var report = await BuildInspectionReportAsync(cancellationToken);
        PrintInspectionReport(report);
        return 0;
    }

    public async Task<int> InitializeAsync(CancellationToken cancellationToken)
    {
        var existingCollections = await GetExistingCollectionNamesAsync(cancellationToken);
        var createdCollections = new List<string>();

        foreach (var collection in _collections)
        {
            if (existingCollections.Contains(collection.CollectionName))
            {
                continue;
            }

            await _context.Database.CreateCollectionAsync(
                collection.CollectionName,
                cancellationToken: cancellationToken);

            createdCollections.Add(collection.CollectionName);
        }

        await DbInitializer.InitializeAsync(_context, cancellationToken);

        var indexReport = await BuildPESubmissionIndexReportAsync(cancellationToken);

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine($"Collections created: {(createdCollections.Count == 0 ? "none" : string.Join(", ", createdCollections))}");
        Console.WriteLine($"PESubmission required indexes present: {indexReport.AllRequiredIndexesPresent}");

        foreach (var indexName in indexReport.IndexNames.OrderBy(name => name, StringComparer.Ordinal))
        {
            Console.WriteLine($"Index: {indexName}");
        }

        return indexReport.AllRequiredIndexesPresent ? 0 : 1;
    }

    public async Task<int> SeedWorkerE2EAsync(CancellationToken cancellationToken)
    {
        var initializeExitCode = await InitializeAsync(cancellationToken);
        if (initializeExitCode != 0)
        {
            return initializeExitCode;
        }

        var fixtures = BootstrapManifest.CreateFixtureBundle();

        await UpsertFixtureAsync(
            _context.Users,
            fixtures.Admin,
            BootstrapManifest.IsFixtureUser,
            user => user.Email,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.Users,
            fixtures.Student,
            BootstrapManifest.IsFixtureUser,
            user => user.Email,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.Courses,
            fixtures.CCourse,
            BootstrapManifest.IsFixtureCourse,
            course => course.Code,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.Courses,
            fixtures.JavaCourse,
            BootstrapManifest.IsFixtureCourse,
            course => course.Code,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.PEQuestions,
            fixtures.CQuestion,
            BootstrapManifest.IsFixtureQuestion,
            question => question.Title,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.PEQuestions,
            fixtures.JavaQuestion,
            BootstrapManifest.IsFixtureQuestion,
            question => question.Title,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.Exams,
            fixtures.CExam,
            BootstrapManifest.IsFixtureExam,
            exam => exam.Title,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.Exams,
            fixtures.JavaExam,
            BootstrapManifest.IsFixtureExam,
            exam => exam.Title,
            cancellationToken);

        await UpsertFixtureAsync(
            _context.PracticeSessions,
            fixtures.CSession,
            BootstrapManifest.IsFixtureSession,
            session => $"{session.StudentId}:{session.ExamId}",
            cancellationToken);

        await UpsertFixtureAsync(
            _context.PracticeSessions,
            fixtures.JavaSession,
            BootstrapManifest.IsFixtureSession,
            session => $"{session.StudentId}:{session.ExamId}",
            cancellationToken);

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine($"Fixture version: {BootstrapManifest.FixtureVersion}");
        Console.WriteLine("Seed status: deterministic fixture upsert complete.");

        return 0;
    }

    public async Task<int> VerifyWorkerE2EAsync(CancellationToken cancellationToken)
    {
        var report = await BuildVerificationReportAsync(cancellationToken);
        PrintVerificationReport(report);
        return report.IsSuccess ? 0 : 1;
    }

    public async Task<int> CleanWorkerE2EAsync(CancellationToken cancellationToken)
    {
        var fixtureSessionIds = new[]
        {
            BootstrapManifest.CSessionId,
            BootstrapManifest.JavaSessionId
        };

        var fixtureQuestionIds = new[]
        {
            BootstrapManifest.CQuestionId,
            BootstrapManifest.JavaQuestionId
        };

        await _context.PE_Submissions.DeleteManyAsync(
            Builders<PE_Submission>.Filter.Or(
                Builders<PE_Submission>.Filter.In(
                    submission => submission.SessionId,
                    fixtureSessionIds),
                Builders<PE_Submission>.Filter.In(
                    submission => submission.QuestionId,
                    fixtureQuestionIds)),
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.PracticeSessions,
            BootstrapManifest.CSessionId,
            BootstrapManifest.IsFixtureSession,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.PracticeSessions,
            BootstrapManifest.JavaSessionId,
            BootstrapManifest.IsFixtureSession,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.Exams,
            BootstrapManifest.CExamId,
            BootstrapManifest.IsFixtureExam,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.Exams,
            BootstrapManifest.JavaExamId,
            BootstrapManifest.IsFixtureExam,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.PEQuestions,
            BootstrapManifest.CQuestionId,
            BootstrapManifest.IsFixtureQuestion,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.PEQuestions,
            BootstrapManifest.JavaQuestionId,
            BootstrapManifest.IsFixtureQuestion,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.Courses,
            BootstrapManifest.CCourseId,
            BootstrapManifest.IsFixtureCourse,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.Courses,
            BootstrapManifest.JavaCourseId,
            BootstrapManifest.IsFixtureCourse,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.Users,
            BootstrapManifest.StudentUserId,
            BootstrapManifest.IsFixtureUser,
            cancellationToken);

        await DeleteIfFixtureAsync(
            _context.Users,
            BootstrapManifest.AdminUserId,
            BootstrapManifest.IsFixtureUser,
            cancellationToken);

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("Cleanup status: deterministic worker fixture cleanup complete.");

        return 0;
    }

    public async Task<int> SeedAiArtifactsAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        await AIArtifactDbSeeder.SeedAsync(
            _context,
            configuration.RootConfiguration,
            null,
            cancellationToken);

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("AI Rule Artifacts Seeding status: Successfully seeded, updated and activated all AI rule artifacts in MongoDB!");
        return 0;
    }

    public async Task<int> SeedAiAgentsAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var sp = services.BuildServiceProvider();
        var dataProtectionProvider = sp.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
        var secretProtector = new Infrastructure.AI.AISecretProtector(dataProtectionProvider);

        await AIAgentBootstrapSeeder.SeedAsync(
            _context,
            configuration.RootConfiguration,
            secretProtector,
            cancellationToken);

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("AI Agents Seeding status: Successfully seeded, updated and activated all AI Agents and fallbacks in MongoDB!");
        return 0;
    }

    public async Task<int> TestGatekeeperDbFirstAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("  TESTING GATEKEEPER DB-FIRST PIPELINE (REAL MONGODB RUNTIME)");
        Console.WriteLine("=================================================================");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration.RootConfiguration);
        services.AddApplication(configuration.RootConfiguration);
        services.AddSingleton(_context);

        var mockEnvironment = new MockHostEnvironment { EnvironmentName = "Development" };
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(mockEnvironment);
        services.AddMemoryCache();
        services.Configure<Application.Options.AIOptions>(configuration.RootConfiguration.GetSection("AI"));
        services.AddSingleton<IAISecretProtector, Infrastructure.AI.AISecretProtector>();
        services.AddScoped<IAIProviderSettingsResolver, Infrastructure.AI.DbFirstAIProviderSettingsResolver>();
        services.AddScoped<IAIProviderCredentialAdminService, Infrastructure.AI.AIProviderCredentialAdminService>();
        services.AddScoped<IAIAgentRoutingConfigService, Infrastructure.AI.DbFirstAIAgentRoutingConfigService>();
        services.AddScoped<IAIAgentAdminService, Infrastructure.AI.AIAgentAdminService>();
        services.AddScoped<IAIClientFactory, Application.Services.AIClientFactory>();
        services.AddScoped<IAIExecutionService, Application.Services.AIExecutionService>();
        services.AddScoped<IAIFeatureRoutingService, Application.Services.AIFeatureRoutingService>();
        services.AddScoped<IAIPromptService, Application.Services.AIPromptService>();
        services.AddScoped<IAIArtifactCatalogService, Infrastructure.AI.DbFirstAIArtifactCatalogService>();
        services.AddScoped<IAIGatekeeperService, Application.Services.AIGatekeeperService>();
        services.AddScoped<IFileExtractionService, Application.Services.FileExtractionService>();
        services.AddScoped<IAITextClient, Infrastructure.AI.DisabledAITextClient>();
        services.AddScoped<IAIEmbeddingClient, Infrastructure.AI.DisabledAIEmbeddingClient>();
        services.AddScoped<IAIUsageLogRepository, Infrastructure.Persistence.AIUsageLogRepository>();
        services.AddScoped<IAIVndBillingTransactionRepository, Infrastructure.Persistence.AIVndBillingTransactionRepository>();
        services.AddScoped<ISystemSettingRepository, Infrastructure.Persistence.SystemSettingRepository>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var catalogService = scope.ServiceProvider.GetRequiredService<IAIArtifactCatalogService>();
        var gatekeeperService = scope.ServiceProvider.GetRequiredService<IAIGatekeeperService>();
        var extractionService = scope.ServiceProvider.GetRequiredService<IFileExtractionService>();

        // 1. Verify what prompt is loaded from MongoDB
        var activePrompt = await catalogService.GetPromptAsync("gatekeeper", cancellationToken);
        Console.WriteLine($"\n[1] ACTIVE PROMPT FROM MONGODB: Key={activePrompt?.Key}, Version={activePrompt?.Version}, IsActive={activePrompt?.IsActive}");
        if (activePrompt is not null)
        {
            var previewLen = Math.Min(140, activePrompt.SystemPrompt.Length);
            Console.WriteLine($"[1.1] System Prompt Preview: {activePrompt.SystemPrompt[..previewLen]}...");
        }

        var activePolicy = await catalogService.GetPolicyAsync("gatekeeper-policy.json", cancellationToken);
        if (activePolicy.HasValue)
        {
            Console.WriteLine($"\n[2] ACTIVE POLICY FROM MONGODB: PolicyId={activePolicy.Value.GetProperty("policy_id").GetString()}, Version={activePolicy.Value.GetProperty("version").GetString()}");
        }

        // 2. Test File 1: vietnamese_oop_garbage.txt
        var filePath1 = Path.Combine(workspaceRoot, "BE_UnitTests", "FileTest", "vietnamese_oop_garbage.txt");
        Console.WriteLine($"\n[3] TESTING FILE 1 (Vietnamese Java OOP with accents): {filePath1}");
        await using (var stream = File.OpenRead(filePath1))
        {
            var extracted = await extractionService.ExtractTextAsync(stream, "vietnamese_oop_garbage.txt", cancellationToken);
            Console.WriteLine($"Extracted text preview ({extracted.RawText.Length} chars):\n---\n{extracted.RawText.Trim()}\n---");

            var result = await gatekeeperService.ValidateAsync(
                extracted.RawText,
                new GatekeeperRequestContext
                {
                    SubjectHint = "JAVA_OOP",
                    FileName = "vietnamese_oop_garbage.txt"
                },
                cancellationToken);

            Console.WriteLine($"\nGATEKEEPER RESULT FOR FILE 1:");
            Console.WriteLine($"  IsSupported: {result.IsSupported}");
            Console.WriteLine($"  Verdict: {result.Verdict}");
            Console.WriteLine($"  PrimaryDomain: {result.PrimaryDomain}");
            Console.WriteLine($"  Confidence: {result.Confidence}");
            Console.WriteLine($"  RejectionReasonCode: {result.RejectionReasonCode}");
            Console.WriteLine($"  Reason: {result.Reason}");
        }

        // 3. Test File 2: vietnamese_short_oop.txt
        var filePath2 = Path.Combine(workspaceRoot, "BE_UnitTests", "FileTest", "vietnamese_short_oop.txt");
        Console.WriteLine($"\n[4] TESTING FILE 2 (Short unaccented Vietnamese text): {filePath2}");
        await using (var stream = File.OpenRead(filePath2))
        {
            var extracted = await extractionService.ExtractTextAsync(stream, "vietnamese_short_oop.txt", cancellationToken);
            Console.WriteLine($"Extracted text preview ({extracted.RawText.Length} chars):\n---\n{extracted.RawText.Trim()}\n---");

            var result = await gatekeeperService.ValidateAsync(
                extracted.RawText,
                new GatekeeperRequestContext
                {
                    SubjectHint = "JAVA_OOP",
                    FileName = "vietnamese_short_oop.txt"
                },
                cancellationToken);

            Console.WriteLine($"\nGATEKEEPER RESULT FOR FILE 2:");
            Console.WriteLine($"  IsSupported: {result.IsSupported}");
            Console.WriteLine($"  Verdict: {result.Verdict}");
            Console.WriteLine($"  PrimaryDomain: {result.PrimaryDomain}");
            Console.WriteLine($"  Confidence: {result.Confidence}");
            Console.WriteLine($"  RejectionReasonCode: {result.RejectionReasonCode}");
            Console.WriteLine($"  Reason: {result.Reason}");
        }

        // 4. Test File 3: Valid English Java OOP (Polymorphism.docx)
        var filePath3 = Path.Combine(workspaceRoot, "BE_UnitTests", "FileTest", "Polymorphism.docx");
        if (File.Exists(filePath3))
        {
            Console.WriteLine($"\n[5] TESTING FILE 3 (Valid English OOP DOCX): {filePath3}");
            await using var stream = File.OpenRead(filePath3);
            var extracted = await extractionService.ExtractTextAsync(stream, "Polymorphism.docx", cancellationToken);
            var result = await gatekeeperService.ValidateAsync(
                extracted.RawText,
                new GatekeeperRequestContext
                {
                    SubjectHint = "JAVA_OOP",
                    FileName = "Polymorphism.docx"
                },
                cancellationToken);

            Console.WriteLine($"\nGATEKEEPER RESULT FOR FILE 3 (ENGLISH DOCX):");
            Console.WriteLine($"  IsSupported: {result.IsSupported}");
            Console.WriteLine($"  Verdict: {result.Verdict}");
            Console.WriteLine($"  PrimaryDomain: {result.PrimaryDomain}");
            Console.WriteLine($"  Confidence: {result.Confidence}");
            Console.WriteLine($"  RejectionReasonCode: {result.RejectionReasonCode ?? "null (ACCEPTED)"}");
        }

        Console.WriteLine("\n=================================================================");
        Console.WriteLine("  DB-FIRST GATEKEEPER TEST COMPLETED SUCCESSFULLY!");
        Console.WriteLine("=================================================================");
        return 0;
    }

    public async Task<int> CreateJavaWorkerSubmissionAsync(
        BootstrapConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var existingSubmissions =
            await GetJavaFixtureSubmissionsAsync(cancellationToken);

        if (existingSubmissions.Count > 0)
        {
            Console.WriteLine("Java worker submission already exists. Cleanup is required before creating another one.");

            foreach (var submission in existingSubmissions
                .OrderBy(item => item.SubmittedAt))
            {
                Console.WriteLine(
                    $"ExistingSubmission: Id={submission.Id}; Status={submission.Status}; AttemptCount={submission.AttemptCount}; SubmittedAt={submission.SubmittedAt:o}; CompletedAt={(submission.CompletedAt.HasValue ? submission.CompletedAt.Value.ToString("o") : "null")}");
            }

            return 1;
        }

        using var serviceProvider = BuildSubmissionCreationServiceProvider(configuration);
        using var scope = serviceProvider.CreateScope();

        var submissionService =
            scope.ServiceProvider.GetRequiredService<IPESubmissionService>();

        var request = BootstrapManifest.CreateJavaWorkerSubmissionRequest();

        var response = await submissionService.SubmitAsync(
            BootstrapManifest.StudentUserId,
            request,
            cancellationToken);

        if (!response.Success)
        {
            Console.WriteLine(
                $"Submission creation failed: {response.Error}");
            return 1;
        }

        var createdSubmission = await _context.PE_Submissions
            .Find(item => item.Id == response.Data!.SubmissionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (createdSubmission is null)
        {
            Console.WriteLine("Submission was reported as accepted but was not found in MongoDB.");
            return 1;
        }

        var preWorkerVerification = VerifyPreWorkerSubmission(createdSubmission);

        PrintPreWorkerSubmissionReport(
            _context.Database.DatabaseNamespace.DatabaseName,
            createdSubmission,
            preWorkerVerification);

        return preWorkerVerification.IsSuccess ? 0 : 1;
    }

    public async Task<int> CreateCWorkerSubmissionAsync(
        BootstrapConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var existingSubmissions =
            await GetCFixtureSubmissionsAsync(cancellationToken);

        if (existingSubmissions.Count > 0)
        {
            Console.WriteLine("C worker submission already exists. Cleanup is required before creating another one.");

            foreach (var submission in existingSubmissions
                .OrderBy(item => item.SubmittedAt))
            {
                Console.WriteLine(
                    $"ExistingSubmission: Id={submission.Id}; Status={submission.Status}; AttemptCount={submission.AttemptCount}; SubmittedAt={submission.SubmittedAt:o}; CompletedAt={(submission.CompletedAt.HasValue ? submission.CompletedAt.Value.ToString("o") : "null")}");
            }

            return 1;
        }

        using var serviceProvider = BuildSubmissionCreationServiceProvider(configuration);
        using var scope = serviceProvider.CreateScope();

        var submissionService =
            scope.ServiceProvider.GetRequiredService<IPESubmissionService>();

        var request = BootstrapManifest.CreateCWorkerSubmissionRequest();

        var response = await submissionService.SubmitAsync(
            BootstrapManifest.StudentUserId,
            request,
            cancellationToken);

        if (!response.Success)
        {
            Console.WriteLine(
                $"Submission creation failed: {response.Error}");
            return 1;
        }

        var createdSubmission = await _context.PE_Submissions
            .Find(item => item.Id == response.Data!.SubmissionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (createdSubmission is null)
        {
            Console.WriteLine("Submission was reported as accepted but was not found in MongoDB.");
            return 1;
        }

        var preWorkerVerification = VerifyPreWorkerSubmission(
            createdSubmission,
            BootstrapManifest.CSessionId,
            BootstrapManifest.CQuestionId,
            BootstrapManifest.CLanguageId,
            BootstrapManifest.CSourceFilename);

        PrintPreWorkerSubmissionReport(
            _context.Database.DatabaseNamespace.DatabaseName,
            createdSubmission,
            preWorkerVerification);

        return preWorkerVerification.IsSuccess ? 0 : 1;
    }

    public async Task<int> VerifyJavaWorkerResultAsync(
        CancellationToken cancellationToken)
    {
        var report = await BuildJavaWorkerResultReportAsync(cancellationToken);
        PrintJavaWorkerResultReport(
            _context.Database.DatabaseNamespace.DatabaseName,
            report);
        return report.IsSuccess ? 0 : 1;
    }

    public async Task<int> VerifyCWorkerResultAsync(
        CancellationToken cancellationToken)
    {
        var report = await BuildCWorkerResultReportAsync(cancellationToken);
        PrintCWorkerResultReport(
            _context.Database.DatabaseNamespace.DatabaseName,
            report);
        return report.IsSuccess ? 0 : 1;
    }

    public async Task<int> RunJavaHttpE2EAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var javaSubmissions =
            await GetJavaFixtureSubmissionsAsync(cancellationToken);

        var attemptOneSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 1);

        var attemptTwoSubmissions = javaSubmissions
            .Where(item => item.AttemptCount == 2)
            .OrderBy(item => item.SubmittedAt)
            .ToList();

        if (attemptTwoSubmissions.Count > 0)
        {
            Console.WriteLine("Java HTTP attempt 2 already exists. Cleanup is required before creating another one.");

            foreach (var submission in attemptTwoSubmissions)
            {
                Console.WriteLine(
                    $"ExistingSubmission: Id={submission.Id}; Status={submission.Status}; AttemptCount={submission.AttemptCount}; SubmittedAt={submission.SubmittedAt:o}; CompletedAt={(submission.CompletedAt.HasValue ? submission.CompletedAt.Value.ToString("o") : "null")}");
            }

            return 1;
        }

        var apiProcesses =
            AuthenticatedHttpWorkflowRunner
                .DiscoverApiProcesses(workspaceRoot);

        var apiSelection =
            AuthenticatedHttpWorkflowRunner
                .SelectSingleApiProcess(apiProcesses);

        if (!apiSelection.IsSuccess)
        {
            Console.WriteLine(apiSelection.Error);
            return 1;
        }

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("AuthenticationPath: Development-only production JWT generator fallback");
        Console.WriteLine("GoogleOAuthExercised: False");
        Console.WriteLine($"ApiProcessId: {apiSelection.Process!.ProcessId}");
        Console.WriteLine($"ApiProcessPath: {apiSelection.Process.ExecutablePath}");

        var tokenReceipt =
            await AuthenticatedHttpWorkflowRunner
                .IssueFixtureStudentAccessTokenAsync(
                    configuration,
                    _context,
                    cancellationToken);

        Console.WriteLine($"AccessTokenReceived: {tokenReceipt.HasAccessToken}");
        Console.WriteLine($"AccessTokenLength: {tokenReceipt.TokenLength}");
        Console.WriteLine($"AuthenticatedStudentId: {tokenReceipt.StudentId}");
        Console.WriteLine($"AuthenticatedRole: {tokenReceipt.Role}");
        Console.WriteLine($"TokenLifetimePresent: {tokenReceipt.LifetimePresent}");

        using var httpClient =
            AuthenticatedHttpWorkflowRunner.CreateHttpClient(
                tokenReceipt.AccessToken!);

        var profile = await AuthenticatedHttpWorkflowRunner
            .ReadProfileAsync(
                httpClient,
                cancellationToken);

        if (profile?.Data is null)
        {
            Console.WriteLine("Authenticated profile lookup failed.");
            return 1;
        }

        var creationResult =
            await AuthenticatedHttpWorkflowRunner
                .CreateSubmissionAsync(
                    httpClient,
                    cancellationToken);

        Console.WriteLine(
            $"SubmissionHttpStatus: {(int)creationResult.StatusCode}");

        if (creationResult.StatusCode != HttpStatusCode.Accepted ||
            creationResult.Response?.Success != true ||
            creationResult.Response.Data is null)
        {
            Console.WriteLine(
                $"SubmissionCreationSucceeded: False; ResponseBodyLength={creationResult.ResponseBodyLength}");
            return 1;
        }

        var submissionId =
            creationResult.Response.Data.SubmissionId;

        Console.WriteLine(
            $"CreatedSubmissionId: {submissionId}");

        var observedStatuses =
            new List<SubmissionProcessingStatus>();

        PESubmissionDetailDto? httpTerminalSubmission = null;
        PE_Submission? mongoTerminalSubmission = null;

        var deadlineUtc = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadlineUtc)
        {
            var pollResult =
                await AuthenticatedHttpWorkflowRunner
                    .GetSubmissionAsync(
                        httpClient,
                        submissionId,
                        cancellationToken);

            if (pollResult.StatusCode == HttpStatusCode.OK &&
                pollResult.Response?.Success == true &&
                pollResult.Response.Data is not null)
            {
                httpTerminalSubmission =
                    pollResult.Response.Data;

                observedStatuses.Add(
                    httpTerminalSubmission.Status);

                if (httpTerminalSubmission.Status is
                    SubmissionProcessingStatus.Completed or
                    SubmissionProcessingStatus.Failed)
                {
                    break;
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);
        }

        mongoTerminalSubmission = await _context.PE_Submissions
            .Find(item => item.Id == submissionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (httpTerminalSubmission is null ||
            mongoTerminalSubmission is null)
        {
            Console.WriteLine("Terminal submission verification failed because one or both submission views were unavailable.");
            return 1;
        }

        var existingCSubmission =
            (await GetCFixtureSubmissionsAsync(cancellationToken))
            .OrderByDescending(item => item.SubmittedAt)
            .FirstOrDefault();

        var noTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithoutTokenAsync(
                    submissionId,
                    cancellationToken);

        var malformedTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithMalformedTokenAsync(
                    submissionId,
                    cancellationToken);

        var verification =
            AuthenticatedHttpResultVerifier.Validate(
                httpTerminalSubmission,
                mongoTerminalSubmission,
                attemptOneSubmission,
                existingCSubmission,
                creationResult.StatusCode,
                observedStatuses,
                noTokenStatus,
                malformedTokenStatus);

        Console.WriteLine(
            $"ObservedHttpStatuses: {string.Join(" -> ", observedStatuses.Distinct())}");
        Console.WriteLine(
            $"TerminalHttpStatus: {httpTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalMongoStatus: {mongoTerminalSubmission.Status}");
        Console.WriteLine(
            $"MongoAttemptCount: {mongoTerminalSubmission.AttemptCount}");
        Console.WriteLine(
            $"MongoMaxScore: {mongoTerminalSubmission.MaxScore}");
        Console.WriteLine(
            $"NoTokenResultStatus: {(int)noTokenStatus}");
        Console.WriteLine(
            $"MalformedTokenResultStatus: {(int)malformedTokenStatus}");
        Console.WriteLine(
            $"VerificationSuccess: {verification.IsSuccess}");

        foreach (var failure in verification.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }

        return verification.IsSuccess ? 0 : 1;
    }

    public async Task<int> RunJavaHttpCompilationErrorE2EAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var javaSubmissions =
            await GetJavaFixtureSubmissionsAsync(cancellationToken);

        var attemptOneSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 1);

        var attemptTwoSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 2);

        var attemptThreeSubmissions = javaSubmissions
            .Where(item => item.AttemptCount == 3)
            .OrderBy(item => item.SubmittedAt)
            .ToList();

        if (attemptThreeSubmissions.Count > 0)
        {
            Console.WriteLine("Java HTTP attempt 3 already exists. Cleanup is required before creating another one.");

            foreach (var submission in attemptThreeSubmissions)
            {
                Console.WriteLine(
                    $"ExistingSubmission: Id={submission.Id}; Status={submission.Status}; Verdict={submission.FinalVerdict?.ToString() ?? "null"}; Score={submission.QuestionScore}; AttemptCount={submission.AttemptCount}; SubmittedAt={submission.SubmittedAt:o}; CompletedAt={(submission.CompletedAt.HasValue ? submission.CompletedAt.Value.ToString("o") : "null")}");
            }

            return 1;
        }

        var apiProcesses =
            AuthenticatedHttpWorkflowRunner
                .DiscoverApiProcesses(workspaceRoot);

        var apiSelection =
            AuthenticatedHttpWorkflowRunner
                .SelectSingleApiProcess(apiProcesses);

        if (!apiSelection.IsSuccess)
        {
            Console.WriteLine(apiSelection.Error);
            return 1;
        }

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("AuthenticationPath: Development-only production JWT generator fallback");
        Console.WriteLine("GoogleOAuthExercised: False");
        Console.WriteLine($"ApiProcessId: {apiSelection.Process!.ProcessId}");
        Console.WriteLine($"ApiProcessPath: {apiSelection.Process.ExecutablePath}");

        var tokenReceipt =
            await AuthenticatedHttpWorkflowRunner
                .IssueFixtureStudentAccessTokenAsync(
                    configuration,
                    _context,
                    cancellationToken);

        Console.WriteLine($"AccessTokenReceived: {tokenReceipt.HasAccessToken}");
        Console.WriteLine($"AccessTokenLength: {tokenReceipt.TokenLength}");
        Console.WriteLine($"AuthenticatedStudentId: {tokenReceipt.StudentId}");
        Console.WriteLine($"AuthenticatedRole: {tokenReceipt.Role}");
        Console.WriteLine($"TokenLifetimePresent: {tokenReceipt.LifetimePresent}");

        using var httpClient =
            AuthenticatedHttpWorkflowRunner.CreateHttpClient(
                tokenReceipt.AccessToken!);

        var profile = await AuthenticatedHttpWorkflowRunner
            .ReadProfileAsync(
                httpClient,
                cancellationToken);

        if (profile?.Data is null)
        {
            Console.WriteLine("Authenticated profile lookup failed.");
            return 1;
        }

        var creationResult =
            await AuthenticatedHttpWorkflowRunner
                .CreateSubmissionAsync(
                    httpClient,
                    AuthenticatedHttpWorkflowRunner
                        .SerializeJavaCompilationErrorSubmissionPayload(),
                    cancellationToken);

        Console.WriteLine(
            $"SubmissionHttpStatus: {(int)creationResult.StatusCode}");

        if (creationResult.StatusCode != HttpStatusCode.Accepted ||
            creationResult.Response?.Success != true ||
            creationResult.Response.Data is null)
        {
            Console.WriteLine(
                $"SubmissionCreationSucceeded: False; ResponseBodyLength={creationResult.ResponseBodyLength}");
            return 1;
        }

        var submissionId =
            creationResult.Response.Data.SubmissionId;

        Console.WriteLine(
            $"CreatedSubmissionId: {submissionId}");

        var observedStatuses =
            new List<SubmissionProcessingStatus>();

        PESubmissionDetailDto? httpTerminalSubmission = null;
        PE_Submission? mongoTerminalSubmission = null;

        var deadlineUtc = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadlineUtc)
        {
            var pollResult =
                await AuthenticatedHttpWorkflowRunner
                    .GetSubmissionAsync(
                        httpClient,
                        submissionId,
                        cancellationToken);

            if (pollResult.StatusCode == HttpStatusCode.OK &&
                pollResult.Response?.Success == true &&
                pollResult.Response.Data is not null)
            {
                httpTerminalSubmission =
                    pollResult.Response.Data;

                observedStatuses.Add(
                    httpTerminalSubmission.Status);

                if (httpTerminalSubmission.Status is
                    SubmissionProcessingStatus.Completed or
                    SubmissionProcessingStatus.Failed)
                {
                    break;
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);
        }

        mongoTerminalSubmission = await _context.PE_Submissions
            .Find(item => item.Id == submissionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (httpTerminalSubmission is null ||
            mongoTerminalSubmission is null)
        {
            Console.WriteLine("Terminal submission verification failed because one or both submission views were unavailable.");
            return 1;
        }

        var existingCSubmission =
            (await GetCFixtureSubmissionsAsync(cancellationToken))
            .OrderByDescending(item => item.SubmittedAt)
            .FirstOrDefault();

        var noTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithoutTokenAsync(
                    submissionId,
                    cancellationToken);

        var malformedTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithMalformedTokenAsync(
                    submissionId,
                    cancellationToken);

        var verification =
            AuthenticatedHttpCompilationErrorResultVerifier.Validate(
                httpTerminalSubmission,
                mongoTerminalSubmission,
                attemptOneSubmission,
                attemptTwoSubmission,
                existingCSubmission,
                creationResult.StatusCode,
                observedStatuses,
                noTokenStatus,
                malformedTokenStatus);

        var compileOutputLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.CompileOutput?.Length ?? 0)
            .ToArray();

        Console.WriteLine(
            $"ObservedHttpStatuses: {string.Join(" -> ", observedStatuses.Distinct())}");
        Console.WriteLine(
            $"TerminalHttpStatus: {httpTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalMongoStatus: {mongoTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalHttpVerdict: {httpTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"TerminalMongoVerdict: {mongoTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"MongoAttemptCount: {mongoTerminalSubmission.AttemptCount}");
        Console.WriteLine(
            $"MongoMaxScore: {mongoTerminalSubmission.MaxScore}");
        Console.WriteLine(
            $"Judge0BatchExecutionCount: {mongoTerminalSubmission.Execution.Cases.Count}");
        Console.WriteLine(
            $"Judge0ExecutionAttemptCount: {mongoTerminalSubmission.Execution.AttemptCount}");
        Console.WriteLine("Judge0CompilationStatusId: 6");
        Console.WriteLine("Judge0CompilationStatusDescription: Compilation Error");
        Console.WriteLine(
            $"CompileOutputPresent: {mongoTerminalSubmission.TestResultItems.All(item => !string.IsNullOrWhiteSpace(item.CompileOutput))}");
        Console.WriteLine(
            $"CompileOutputLengths: {string.Join(",", compileOutputLengths)}");
        Console.WriteLine(
            $"ProcessingErrorPresent: {!string.IsNullOrWhiteSpace(mongoTerminalSubmission.ProcessingError)}");
        Console.WriteLine(
            $"NoTokenResultStatus: {(int)noTokenStatus}");
        Console.WriteLine(
            $"MalformedTokenResultStatus: {(int)malformedTokenStatus}");
        Console.WriteLine(
            $"VerificationSuccess: {verification.IsSuccess}");

        foreach (var failure in verification.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }

        return verification.IsSuccess ? 0 : 1;
    }

    public async Task<int> RunJavaHttpWrongAnswerE2EAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var javaSubmissions =
            await GetJavaFixtureSubmissionsAsync(cancellationToken);

        var attemptOneSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 1);

        var attemptTwoSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 2);

        var attemptThreeSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 3);

        var attemptFourSubmissions = javaSubmissions
            .Where(item => item.AttemptCount == 4)
            .OrderBy(item => item.SubmittedAt)
            .ToList();

        var apiProcesses =
            AuthenticatedHttpWorkflowRunner
                .DiscoverApiProcesses(workspaceRoot);

        var apiSelection =
            AuthenticatedHttpWorkflowRunner
                .SelectSingleApiProcess(apiProcesses);

        if (!apiSelection.IsSuccess)
        {
            Console.WriteLine(apiSelection.Error);
            return 1;
        }

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("AuthenticationPath: Development-only production JWT generator fallback");
        Console.WriteLine("GoogleOAuthExercised: False");
        Console.WriteLine($"ApiProcessId: {apiSelection.Process!.ProcessId}");
        Console.WriteLine($"ApiProcessPath: {apiSelection.Process.ExecutablePath}");

        var tokenReceipt =
            await AuthenticatedHttpWorkflowRunner
                .IssueFixtureStudentAccessTokenAsync(
                    configuration,
                    _context,
                    cancellationToken);

        Console.WriteLine($"AccessTokenReceived: {tokenReceipt.HasAccessToken}");
        Console.WriteLine($"AccessTokenLength: {tokenReceipt.TokenLength}");
        Console.WriteLine($"AuthenticatedStudentId: {tokenReceipt.StudentId}");
        Console.WriteLine($"AuthenticatedRole: {tokenReceipt.Role}");
        Console.WriteLine($"TokenLifetimePresent: {tokenReceipt.LifetimePresent}");

        using var httpClient =
            AuthenticatedHttpWorkflowRunner.CreateHttpClient(
                tokenReceipt.AccessToken!);

        var profile = await AuthenticatedHttpWorkflowRunner
            .ReadProfileAsync(
                httpClient,
                cancellationToken);

        if (profile?.Data is null)
        {
            Console.WriteLine("Authenticated profile lookup failed.");
            return 1;
        }

        HttpStatusCode submitStatusCode;
        string submissionId;

        if (attemptFourSubmissions.Count > 1)
        {
            Console.WriteLine("Java HTTP attempt 4 already exists more than once. Refusing to verify an ambiguous scenario.");

            foreach (var submission in attemptFourSubmissions)
            {
                Console.WriteLine(
                    $"ExistingSubmission: Id={submission.Id}; Status={submission.Status}; Verdict={submission.FinalVerdict?.ToString() ?? "null"}; Score={submission.QuestionScore}; Passed={submission.TestCasesPassed}/{submission.TotalTestCases}; AttemptCount={submission.AttemptCount}; SubmittedAt={submission.SubmittedAt:o}; CompletedAt={(submission.CompletedAt.HasValue ? submission.CompletedAt.Value.ToString("o") : "null")}");
            }

            return 1;
        }

        if (attemptFourSubmissions.Count == 1)
        {
            submitStatusCode = HttpStatusCode.Accepted;
            submissionId = attemptFourSubmissions[0].Id;

            Console.WriteLine("Attempt4Reuse: True");
            Console.WriteLine(
                $"SubmissionHttpStatus: {(int)submitStatusCode}");
            Console.WriteLine(
                $"CreatedSubmissionId: {submissionId}");
        }
        else
        {
            var creationResult =
                await AuthenticatedHttpWorkflowRunner
                    .CreateSubmissionAsync(
                        httpClient,
                        AuthenticatedHttpWorkflowRunner
                            .SerializeJavaWrongAnswerSubmissionPayload(),
                        cancellationToken);

            submitStatusCode = creationResult.StatusCode;

            Console.WriteLine(
                $"SubmissionHttpStatus: {(int)submitStatusCode}");

            if (creationResult.StatusCode != HttpStatusCode.Accepted ||
                creationResult.Response?.Success != true ||
                creationResult.Response.Data is null)
            {
                Console.WriteLine(
                    $"SubmissionCreationSucceeded: False; ResponseBodyLength={creationResult.ResponseBodyLength}");
                return 1;
            }

            submissionId =
                creationResult.Response.Data.SubmissionId;

            Console.WriteLine("Attempt4Reuse: False");
            Console.WriteLine(
                $"CreatedSubmissionId: {submissionId}");
        }

        var observedStatuses =
            new List<SubmissionProcessingStatus>();

        PESubmissionDetailDto? httpTerminalSubmission = null;
        PE_Submission? mongoTerminalSubmission = null;

        var deadlineUtc = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadlineUtc)
        {
            var pollResult =
                await AuthenticatedHttpWorkflowRunner
                    .GetSubmissionAsync(
                        httpClient,
                        submissionId,
                        cancellationToken);

            if (pollResult.StatusCode == HttpStatusCode.OK &&
                pollResult.Response?.Success == true &&
                pollResult.Response.Data is not null)
            {
                httpTerminalSubmission =
                    pollResult.Response.Data;

                observedStatuses.Add(
                    httpTerminalSubmission.Status);

                if (httpTerminalSubmission.Status is
                    SubmissionProcessingStatus.Completed or
                    SubmissionProcessingStatus.Failed)
                {
                    break;
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);
        }

        mongoTerminalSubmission = await _context.PE_Submissions
            .Find(item => item.Id == submissionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (httpTerminalSubmission is null ||
            mongoTerminalSubmission is null)
        {
            Console.WriteLine("Terminal submission verification failed because one or both submission views were unavailable.");
            return 1;
        }

        var existingCSubmission =
            (await GetCFixtureSubmissionsAsync(cancellationToken))
            .OrderByDescending(item => item.SubmittedAt)
            .FirstOrDefault();

        var noTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithoutTokenAsync(
                    submissionId,
                    cancellationToken);

        var malformedTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithMalformedTokenAsync(
                    submissionId,
                    cancellationToken);

        var verification =
            AuthenticatedHttpWrongAnswerResultVerifier.Validate(
                httpTerminalSubmission,
                mongoTerminalSubmission,
                attemptOneSubmission,
                attemptTwoSubmission,
                attemptThreeSubmission,
                existingCSubmission,
                submitStatusCode,
                observedStatuses,
                noTokenStatus,
                malformedTokenStatus);

        var stdoutLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.ActualOutput?.Length ?? 0)
            .ToArray();

        var stderrLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.StandardError?.Length ?? 0)
            .ToArray();

        var compileOutputLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.CompileOutput?.Length ?? 0)
            .ToArray();

        var runtimeValues = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.RuntimeMs)
            .ToArray();

        var memoryValues = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.MemoryKb)
            .ToArray();

        Console.WriteLine(
            $"ObservedHttpStatuses: {string.Join(" -> ", observedStatuses.Distinct())}");
        Console.WriteLine(
            $"TerminalHttpStatus: {httpTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalMongoStatus: {mongoTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalHttpVerdict: {httpTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"TerminalMongoVerdict: {mongoTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"MongoAttemptCount: {mongoTerminalSubmission.AttemptCount}");
        Console.WriteLine(
            $"MongoMaxScore: {mongoTerminalSubmission.MaxScore}");
        Console.WriteLine("Judge0BatchExecutionCount: 1");
        Console.WriteLine("Judge0BatchSize: 3");
        Console.WriteLine(
            $"Judge0ExecutionAttemptCount: {mongoTerminalSubmission.Execution.AttemptCount}");
        Console.WriteLine("Judge0ExecutionSucceededStatusIds: 3,4");
        Console.WriteLine("Judge0ExecutionSucceededStatusDescriptions: Accepted,Wrong Answer");
        Console.WriteLine(
            $"StdoutPresent: {mongoTerminalSubmission.TestResultItems.Any(item => item.ActualOutput is not null)}");
        Console.WriteLine(
            $"StdoutLengths: {string.Join(",", stdoutLengths)}");
        Console.WriteLine(
            $"StandardErrorPresent: {mongoTerminalSubmission.TestResultItems.Any(item => !string.IsNullOrWhiteSpace(item.StandardError))}");
        Console.WriteLine(
            $"StandardErrorLengths: {string.Join(",", stderrLengths)}");
        Console.WriteLine(
            $"CompileOutputPresent: {mongoTerminalSubmission.TestResultItems.Any(item => !string.IsNullOrWhiteSpace(item.CompileOutput))}");
        Console.WriteLine(
            $"CompileOutputLengths: {string.Join(",", compileOutputLengths)}");
        Console.WriteLine(
            $"RuntimeMsValues: {string.Join(",", runtimeValues)}");
        Console.WriteLine(
            $"MemoryKbValues: {string.Join(",", memoryValues)}");
        Console.WriteLine(
            $"ProcessingErrorPresent: {!string.IsNullOrWhiteSpace(mongoTerminalSubmission.ProcessingError)}");
        Console.WriteLine(
            $"NoTokenResultStatus: {(int)noTokenStatus}");
        Console.WriteLine(
            $"MalformedTokenResultStatus: {(int)malformedTokenStatus}");
        Console.WriteLine(
            $"VerificationSuccess: {verification.IsSuccess}");

        foreach (var failure in verification.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }

        return verification.IsSuccess ? 0 : 1;
    }

    public async Task<int> RunJavaHttpRuntimeErrorE2EAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var javaSubmissions =
            await GetJavaFixtureSubmissionsAsync(cancellationToken);

        var attemptOneSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 1);

        var attemptTwoSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 2);

        var attemptThreeSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 3);

        var attemptFourSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 4);

        var attemptFiveSubmissions = javaSubmissions
            .Where(item => item.AttemptCount == 5)
            .OrderBy(item => item.SubmittedAt)
            .ToList();

        var apiProcesses =
            AuthenticatedHttpWorkflowRunner
                .DiscoverApiProcesses(workspaceRoot);

        var apiSelection =
            AuthenticatedHttpWorkflowRunner
                .SelectSingleApiProcess(apiProcesses);

        if (!apiSelection.IsSuccess)
        {
            Console.WriteLine(apiSelection.Error);
            return 1;
        }

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("AuthenticationPath: Development-only production JWT generator fallback");
        Console.WriteLine("GoogleOAuthExercised: False");
        Console.WriteLine($"ApiProcessId: {apiSelection.Process!.ProcessId}");
        Console.WriteLine($"ApiProcessPath: {apiSelection.Process.ExecutablePath}");

        var tokenReceipt =
            await AuthenticatedHttpWorkflowRunner
                .IssueFixtureStudentAccessTokenAsync(
                    configuration,
                    _context,
                    cancellationToken);

        Console.WriteLine($"AccessTokenReceived: {tokenReceipt.HasAccessToken}");
        Console.WriteLine($"AccessTokenLength: {tokenReceipt.TokenLength}");
        Console.WriteLine($"AuthenticatedStudentId: {tokenReceipt.StudentId}");
        Console.WriteLine($"AuthenticatedRole: {tokenReceipt.Role}");
        Console.WriteLine($"TokenLifetimePresent: {tokenReceipt.LifetimePresent}");

        using var httpClient =
            AuthenticatedHttpWorkflowRunner.CreateHttpClient(
                tokenReceipt.AccessToken!);

        var profile = await AuthenticatedHttpWorkflowRunner
            .ReadProfileAsync(
                httpClient,
                cancellationToken);

        if (profile?.Data is null)
        {
            Console.WriteLine("Authenticated profile lookup failed.");
            return 1;
        }

        HttpStatusCode submitStatusCode;
        string submissionId;

        if (attemptFiveSubmissions.Count > 1)
        {
            Console.WriteLine("Java HTTP attempt 5 already exists more than once. Refusing to verify an ambiguous scenario.");

            foreach (var submission in attemptFiveSubmissions)
            {
                Console.WriteLine(
                    $"ExistingSubmission: Id={submission.Id}; Status={submission.Status}; Verdict={submission.FinalVerdict?.ToString() ?? "null"}; Score={submission.QuestionScore}; Passed={submission.TestCasesPassed}/{submission.TotalTestCases}; AttemptCount={submission.AttemptCount}; SubmittedAt={submission.SubmittedAt:o}; CompletedAt={(submission.CompletedAt.HasValue ? submission.CompletedAt.Value.ToString("o") : "null")}");
            }

            return 1;
        }

        if (attemptFiveSubmissions.Count == 1)
        {
            submitStatusCode = HttpStatusCode.Accepted;
            submissionId = attemptFiveSubmissions[0].Id;

            Console.WriteLine("Attempt5Reuse: True");
            Console.WriteLine(
                $"SubmissionHttpStatus: {(int)submitStatusCode}");
            Console.WriteLine(
                $"CreatedSubmissionId: {submissionId}");
        }
        else
        {
            var creationResult =
                await AuthenticatedHttpWorkflowRunner
                    .CreateSubmissionAsync(
                        httpClient,
                        AuthenticatedHttpWorkflowRunner
                            .SerializeJavaRuntimeErrorSubmissionPayload(),
                        cancellationToken);

            submitStatusCode = creationResult.StatusCode;

            Console.WriteLine(
                $"SubmissionHttpStatus: {(int)submitStatusCode}");

            if (creationResult.StatusCode != HttpStatusCode.Accepted ||
                creationResult.Response?.Success != true ||
                creationResult.Response.Data is null)
            {
                Console.WriteLine(
                    $"SubmissionCreationSucceeded: False; ResponseBodyLength={creationResult.ResponseBodyLength}");
                return 1;
            }

            submissionId =
                creationResult.Response.Data.SubmissionId;

            Console.WriteLine("Attempt5Reuse: False");
            Console.WriteLine(
                $"CreatedSubmissionId: {submissionId}");
        }

        var observedStatuses =
            new List<SubmissionProcessingStatus>();

        PESubmissionDetailDto? httpTerminalSubmission = null;
        PE_Submission? mongoTerminalSubmission = null;

        var deadlineUtc = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadlineUtc)
        {
            var pollResult =
                await AuthenticatedHttpWorkflowRunner
                    .GetSubmissionAsync(
                        httpClient,
                        submissionId,
                        cancellationToken);

            if (pollResult.StatusCode == HttpStatusCode.OK &&
                pollResult.Response?.Success == true &&
                pollResult.Response.Data is not null)
            {
                httpTerminalSubmission =
                    pollResult.Response.Data;

                observedStatuses.Add(
                    httpTerminalSubmission.Status);

                if (httpTerminalSubmission.Status is
                    SubmissionProcessingStatus.Completed or
                    SubmissionProcessingStatus.Failed)
                {
                    break;
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);
        }

        mongoTerminalSubmission = await _context.PE_Submissions
            .Find(item => item.Id == submissionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (httpTerminalSubmission is null ||
            mongoTerminalSubmission is null)
        {
            Console.WriteLine("Terminal submission verification failed because one or both submission views were unavailable.");
            return 1;
        }

        var existingCSubmission =
            (await GetCFixtureSubmissionsAsync(cancellationToken))
            .OrderByDescending(item => item.SubmittedAt)
            .FirstOrDefault();

        var noTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithoutTokenAsync(
                    submissionId,
                    cancellationToken);

        var malformedTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithMalformedTokenAsync(
                    submissionId,
                    cancellationToken);

        var verification =
            AuthenticatedHttpRuntimeErrorResultVerifier.Validate(
                httpTerminalSubmission,
                mongoTerminalSubmission,
                attemptOneSubmission,
                attemptTwoSubmission,
                attemptThreeSubmission,
                attemptFourSubmission,
                existingCSubmission,
                submitStatusCode,
                observedStatuses,
                noTokenStatus,
                malformedTokenStatus);

        var stdoutLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.ActualOutput?.Length ?? 0)
            .ToArray();

        var stderrLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.StandardError?.Length ?? 0)
            .ToArray();

        var compileOutputLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.CompileOutput?.Length ?? 0)
            .ToArray();

        var runtimeValues = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.RuntimeMs)
            .ToArray();

        var memoryValues = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.MemoryKb)
            .ToArray();

        Console.WriteLine(
            $"ObservedHttpStatuses: {string.Join(" -> ", observedStatuses.Distinct())}");
        Console.WriteLine(
            $"TerminalHttpStatus: {httpTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalMongoStatus: {mongoTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalHttpVerdict: {httpTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"TerminalMongoVerdict: {mongoTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"MongoAttemptCount: {mongoTerminalSubmission.AttemptCount}");
        Console.WriteLine(
            $"MongoMaxScore: {mongoTerminalSubmission.MaxScore}");
        Console.WriteLine("Judge0BatchExecutionCount: 1");
        Console.WriteLine("Judge0BatchSize: 3");
        Console.WriteLine(
            $"Judge0ExecutionAttemptCount: {mongoTerminalSubmission.Execution.AttemptCount}");
        Console.WriteLine("Judge0RuntimeMappedStatusIds: 7,9,10,11,12,14");
        Console.WriteLine("Judge0RuntimeMappedStatusDescriptions: not persisted by current application");
        Console.WriteLine(
            $"StdoutPresent: {mongoTerminalSubmission.TestResultItems.Any(item => item.ActualOutput is not null)}");
        Console.WriteLine(
            $"StdoutLengths: {string.Join(",", stdoutLengths)}");
        Console.WriteLine(
            $"StandardErrorPresent: {mongoTerminalSubmission.TestResultItems.Any(item => !string.IsNullOrWhiteSpace(item.StandardError))}");
        Console.WriteLine(
            $"StandardErrorLengths: {string.Join(",", stderrLengths)}");
        Console.WriteLine(
            $"CompileOutputPresent: {mongoTerminalSubmission.TestResultItems.Any(item => !string.IsNullOrWhiteSpace(item.CompileOutput))}");
        Console.WriteLine(
            $"CompileOutputLengths: {string.Join(",", compileOutputLengths)}");
        Console.WriteLine("ProviderMessagePresent: False");
        Console.WriteLine("ProviderMessageLengths: 0,0,0");
        Console.WriteLine(
            $"RuntimeMsValues: {string.Join(",", runtimeValues)}");
        Console.WriteLine(
            $"MemoryKbValues: {string.Join(",", memoryValues)}");
        Console.WriteLine(
            $"ProcessingErrorPresent: {!string.IsNullOrWhiteSpace(mongoTerminalSubmission.ProcessingError)}");
        Console.WriteLine(
            $"NoTokenResultStatus: {(int)noTokenStatus}");
        Console.WriteLine(
            $"MalformedTokenResultStatus: {(int)malformedTokenStatus}");
        Console.WriteLine(
            $"VerificationSuccess: {verification.IsSuccess}");

        foreach (var failure in verification.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }

        return verification.IsSuccess ? 0 : 1;
    }

    public async Task<int> RunJavaHttpTimeLimitE2EAsync(
        BootstrapConfiguration configuration,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var javaSubmissions =
            await GetJavaFixtureSubmissionsAsync(cancellationToken);

        var attemptOneSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 1);

        var attemptTwoSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 2);

        var attemptThreeSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 3);

        var attemptFourSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 4);

        var attemptFiveSubmission = javaSubmissions
            .SingleOrDefault(item => item.AttemptCount == 5);

        var attemptSixSubmissions = javaSubmissions
            .Where(item => item.AttemptCount == 6)
            .OrderBy(item => item.SubmittedAt)
            .ToList();

        var apiProcesses =
            AuthenticatedHttpWorkflowRunner
                .DiscoverApiProcesses(workspaceRoot);

        var apiSelection =
            AuthenticatedHttpWorkflowRunner
                .SelectSingleApiProcess(apiProcesses);

        if (!apiSelection.IsSuccess)
        {
            Console.WriteLine(apiSelection.Error);
            return 1;
        }

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine("AuthenticationPath: Development-only production JWT generator fallback");
        Console.WriteLine("GoogleOAuthExercised: False");
        Console.WriteLine($"ApiProcessId: {apiSelection.Process!.ProcessId}");
        Console.WriteLine($"ApiProcessPath: {apiSelection.Process.ExecutablePath}");

        var tokenReceipt =
            await AuthenticatedHttpWorkflowRunner
                .IssueFixtureStudentAccessTokenAsync(
                    configuration,
                    _context,
                    cancellationToken);

        Console.WriteLine($"AccessTokenReceived: {tokenReceipt.HasAccessToken}");
        Console.WriteLine($"AccessTokenLength: {tokenReceipt.TokenLength}");
        Console.WriteLine($"AuthenticatedStudentId: {tokenReceipt.StudentId}");
        Console.WriteLine($"AuthenticatedRole: {tokenReceipt.Role}");
        Console.WriteLine($"TokenLifetimePresent: {tokenReceipt.LifetimePresent}");

        using var httpClient =
            AuthenticatedHttpWorkflowRunner.CreateHttpClient(
                tokenReceipt.AccessToken!);

        var profile = await AuthenticatedHttpWorkflowRunner
            .ReadProfileAsync(
                httpClient,
                cancellationToken);

        if (profile?.Data is null)
        {
            Console.WriteLine("Authenticated profile lookup failed.");
            return 1;
        }

        HttpStatusCode submitStatusCode;
        string submissionId;

        if (attemptSixSubmissions.Count > 1)
        {
            Console.WriteLine("Java HTTP attempt 6 already exists more than once. Refusing to verify an ambiguous scenario.");

            foreach (var submission in attemptSixSubmissions)
            {
                Console.WriteLine(
                    $"ExistingSubmission: Id={submission.Id}; Status={submission.Status}; Verdict={submission.FinalVerdict?.ToString() ?? "null"}; Score={submission.QuestionScore}; Passed={submission.TestCasesPassed}/{submission.TotalTestCases}; AttemptCount={submission.AttemptCount}; SubmittedAt={submission.SubmittedAt:o}; CompletedAt={(submission.CompletedAt.HasValue ? submission.CompletedAt.Value.ToString("o") : "null")}");
            }

            return 1;
        }

        if (attemptSixSubmissions.Count == 1)
        {
            submitStatusCode = HttpStatusCode.Accepted;
            submissionId = attemptSixSubmissions[0].Id;

            Console.WriteLine("Attempt6Reuse: True");
            Console.WriteLine(
                $"SubmissionHttpStatus: {(int)submitStatusCode}");
            Console.WriteLine(
                $"CreatedSubmissionId: {submissionId}");
        }
        else
        {
            var creationResult =
                await AuthenticatedHttpWorkflowRunner
                    .CreateSubmissionAsync(
                        httpClient,
                        AuthenticatedHttpWorkflowRunner
                            .SerializeJavaTimeLimitSubmissionPayload(),
                        cancellationToken);

            submitStatusCode = creationResult.StatusCode;

            Console.WriteLine(
                $"SubmissionHttpStatus: {(int)submitStatusCode}");

            if (creationResult.StatusCode != HttpStatusCode.Accepted ||
                creationResult.Response?.Success != true ||
                creationResult.Response.Data is null)
            {
                Console.WriteLine(
                    $"SubmissionCreationSucceeded: False; ResponseBodyLength={creationResult.ResponseBodyLength}");
                return 1;
            }

            submissionId =
                creationResult.Response.Data.SubmissionId;

            Console.WriteLine("Attempt6Reuse: False");
            Console.WriteLine(
                $"CreatedSubmissionId: {submissionId}");
        }

        var observedStatuses =
            new List<SubmissionProcessingStatus>();

        PESubmissionDetailDto? httpTerminalSubmission = null;
        PE_Submission? mongoTerminalSubmission = null;

        var deadlineUtc = DateTime.UtcNow.AddSeconds(120);

        while (DateTime.UtcNow < deadlineUtc)
        {
            var pollResult =
                await AuthenticatedHttpWorkflowRunner
                    .GetSubmissionAsync(
                        httpClient,
                        submissionId,
                        cancellationToken);

            if (pollResult.StatusCode == HttpStatusCode.OK &&
                pollResult.Response?.Success == true &&
                pollResult.Response.Data is not null)
            {
                httpTerminalSubmission =
                    pollResult.Response.Data;

                observedStatuses.Add(
                    httpTerminalSubmission.Status);

                if (httpTerminalSubmission.Status is
                    SubmissionProcessingStatus.Completed or
                    SubmissionProcessingStatus.Failed)
                {
                    break;
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);
        }

        mongoTerminalSubmission = await _context.PE_Submissions
            .Find(item => item.Id == submissionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (httpTerminalSubmission is null ||
            mongoTerminalSubmission is null)
        {
            Console.WriteLine("Terminal submission verification failed because one or both submission views were unavailable.");
            return 1;
        }

        var existingCSubmission =
            (await GetCFixtureSubmissionsAsync(cancellationToken))
            .OrderByDescending(item => item.SubmittedAt)
            .FirstOrDefault();

        var noTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithoutTokenAsync(
                    submissionId,
                    cancellationToken);

        var malformedTokenStatus =
            await AuthenticatedHttpWorkflowRunner
                .GetResultWithMalformedTokenAsync(
                    submissionId,
                    cancellationToken);

        var verification =
            AuthenticatedHttpTimeLimitResultVerifier.Validate(
                httpTerminalSubmission,
                mongoTerminalSubmission,
                attemptOneSubmission,
                attemptTwoSubmission,
                attemptThreeSubmission,
                attemptFourSubmission,
                attemptFiveSubmission,
                existingCSubmission,
                submitStatusCode,
                observedStatuses,
                noTokenStatus,
                malformedTokenStatus);

        var stdoutLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.ActualOutput?.Length ?? 0)
            .ToArray();

        var stderrLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.StandardError?.Length ?? 0)
            .ToArray();

        var compileOutputLengths = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.CompileOutput?.Length ?? 0)
            .ToArray();

        var runtimeValues = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.RuntimeMs)
            .ToArray();

        var memoryValues = mongoTerminalSubmission
            .TestResultItems
            .OrderBy(item => item.TestCaseIndex)
            .Select(item => item.MemoryKb)
            .ToArray();

        Console.WriteLine(
            $"ObservedHttpStatuses: {string.Join(" -> ", observedStatuses.Distinct())}");
        Console.WriteLine(
            $"TerminalHttpStatus: {httpTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalMongoStatus: {mongoTerminalSubmission.Status}");
        Console.WriteLine(
            $"TerminalHttpVerdict: {httpTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"TerminalMongoVerdict: {mongoTerminalSubmission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine(
            $"MongoAttemptCount: {mongoTerminalSubmission.AttemptCount}");
        Console.WriteLine(
            $"MongoMaxScore: {mongoTerminalSubmission.MaxScore}");
        Console.WriteLine("Judge0BatchExecutionCount: 1");
        Console.WriteLine("Judge0BatchSize: 3");
        Console.WriteLine(
            $"Judge0ExecutionAttemptCount: {mongoTerminalSubmission.Execution.AttemptCount}");
        Console.WriteLine("Judge0TimeLimitStatusIds: 5");
        Console.WriteLine("Judge0TimeLimitStatusDescriptions: Time Limit Exceeded");
        Console.WriteLine(
            $"StdoutPresent: {mongoTerminalSubmission.TestResultItems.Any(item => item.ActualOutput is not null)}");
        Console.WriteLine(
            $"StdoutLengths: {string.Join(",", stdoutLengths)}");
        Console.WriteLine(
            $"StandardErrorPresent: {mongoTerminalSubmission.TestResultItems.Any(item => !string.IsNullOrWhiteSpace(item.StandardError))}");
        Console.WriteLine(
            $"StandardErrorLengths: {string.Join(",", stderrLengths)}");
        Console.WriteLine(
            $"CompileOutputPresent: {mongoTerminalSubmission.TestResultItems.Any(item => !string.IsNullOrWhiteSpace(item.CompileOutput))}");
        Console.WriteLine(
            $"CompileOutputLengths: {string.Join(",", compileOutputLengths)}");
        Console.WriteLine("ProviderMessagePresent: False");
        Console.WriteLine("ProviderMessageLengths: 0,0,0");
        Console.WriteLine(
            $"RuntimeMsValues: {string.Join(",", runtimeValues)}");
        Console.WriteLine(
            $"MemoryKbValues: {string.Join(",", memoryValues)}");
        Console.WriteLine(
            $"ProcessingErrorPresent: {!string.IsNullOrWhiteSpace(mongoTerminalSubmission.ProcessingError)}");
        Console.WriteLine(
            $"NoTokenResultStatus: {(int)noTokenStatus}");
        Console.WriteLine(
            $"MalformedTokenResultStatus: {(int)malformedTokenStatus}");
        Console.WriteLine(
            $"VerificationSuccess: {verification.IsSuccess}");

        foreach (var failure in verification.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }

        return verification.IsSuccess ? 0 : 1;
    }

    public async Task<int> CleanWorkerSubmissionsAsync(
        CancellationToken cancellationToken)
    {
        var result = await _context.PE_Submissions.DeleteManyAsync(
            Builders<PE_Submission>.Filter.And(
                Builders<PE_Submission>.Filter.In(
                    submission => submission.SessionId,
                    new[]
                    {
                        BootstrapManifest.CSessionId,
                        BootstrapManifest.JavaSessionId
                    }),
                Builders<PE_Submission>.Filter.In(
                    submission => submission.QuestionId,
                    new[]
                    {
                        BootstrapManifest.CQuestionId,
                        BootstrapManifest.JavaQuestionId
                    })),
            cancellationToken);

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {_context.Database.DatabaseNamespace.DatabaseName}");
        Console.WriteLine($"Deleted fixture submissions: {result.DeletedCount}");
        return 0;
    }

    private async Task<InspectionReport> BuildInspectionReportAsync(
        CancellationToken cancellationToken)
    {
        var existingCollections = await GetExistingCollectionNamesAsync(cancellationToken);
        var indexMap = await BootstrapIntrospection.ReadIndexesAsync(
            _context,
            _collections,
            cancellationToken);

        var collectionReports = new List<CollectionInspectionReport>();

        foreach (var collection in _collections)
        {
            var bsonCollection =
                _context.Database.GetCollection<BsonDocument>(collection.CollectionName);

            var exists = existingCollections.Contains(collection.CollectionName);
            var documentCount = exists
                ? await bsonCollection.CountDocumentsAsync(
                    FilterDefinition<BsonDocument>.Empty,
                    cancellationToken: cancellationToken)
                : 0;

            collectionReports.Add(
                new CollectionInspectionReport(
                    collection.CollectionName,
                    exists,
                    documentCount,
                    indexMap[collection.CollectionName].Count));
        }

        var fixtureDocumentCount =
            await _context.Users.CountDocumentsAsync(
                Builders<User>.Filter.In(
                    user => user.Id,
                    new[] { BootstrapManifest.AdminUserId, BootstrapManifest.StudentUserId }),
                cancellationToken: cancellationToken) +
            await _context.Courses.CountDocumentsAsync(
                Builders<Course>.Filter.In(
                    course => course.Id,
                    new[] { BootstrapManifest.CCourseId, BootstrapManifest.JavaCourseId }),
                cancellationToken: cancellationToken) +
            await _context.PEQuestions.CountDocumentsAsync(
                Builders<PEQuestion>.Filter.In(
                    question => question.Id,
                    new[] { BootstrapManifest.CQuestionId, BootstrapManifest.JavaQuestionId }),
                cancellationToken: cancellationToken) +
            await _context.Exams.CountDocumentsAsync(
                Builders<Exam>.Filter.In(
                    exam => exam.Id,
                    new[] { BootstrapManifest.CExamId, BootstrapManifest.JavaExamId }),
                cancellationToken: cancellationToken) +
            await _context.PracticeSessions.CountDocumentsAsync(
                Builders<PracticeSession>.Filter.In(
                    session => session.Id,
                    new[] { BootstrapManifest.CSessionId, BootstrapManifest.JavaSessionId }),
                cancellationToken: cancellationToken);

        var fixtureSubmissionCount = await CountFixtureSubmissionsAsync(
            Builders<PE_Submission>.Filter.Empty,
            cancellationToken);

        var pendingFixtureSubmissions = await CountFixtureSubmissionsAsync(
            Builders<PE_Submission>.Filter.Eq(
                submission => submission.Status,
                SubmissionProcessingStatus.Pending),
            cancellationToken);

        var processingFixtureSubmissions = await CountFixtureSubmissionsAsync(
            Builders<PE_Submission>.Filter.Eq(
                submission => submission.Status,
                SubmissionProcessingStatus.Processing),
            cancellationToken);

        var indexReport = await BuildPESubmissionIndexReportAsync(cancellationToken);

        return new InspectionReport(
            _context.Database.DatabaseNamespace.DatabaseName,
            collectionReports,
            indexReport.IndexNames,
            indexReport.AllRequiredIndexesPresent,
            fixtureDocumentCount,
            fixtureSubmissionCount,
            pendingFixtureSubmissions,
            processingFixtureSubmissions);
    }

    private async Task<VerificationReport> BuildVerificationReportAsync(
        CancellationToken cancellationToken)
    {
        var fixture = BootstrapManifest.CreateFixtureBundle();
        var inspection = await BuildInspectionReportAsync(cancellationToken);

        var admin = await _context.Users.Find(user => user.Id == BootstrapManifest.AdminUserId)
            .FirstOrDefaultAsync(cancellationToken);
        var student = await _context.Users.Find(user => user.Id == BootstrapManifest.StudentUserId)
            .FirstOrDefaultAsync(cancellationToken);
        var cCourse = await _context.Courses.Find(course => course.Id == BootstrapManifest.CCourseId)
            .FirstOrDefaultAsync(cancellationToken);
        var javaCourse = await _context.Courses.Find(course => course.Id == BootstrapManifest.JavaCourseId)
            .FirstOrDefaultAsync(cancellationToken);
        var cQuestion = await _context.PEQuestions.Find(question => question.Id == BootstrapManifest.CQuestionId)
            .FirstOrDefaultAsync(cancellationToken);
        var javaQuestion = await _context.PEQuestions.Find(question => question.Id == BootstrapManifest.JavaQuestionId)
            .FirstOrDefaultAsync(cancellationToken);
        var cExam = await _context.Exams.Find(exam => exam.Id == BootstrapManifest.CExamId)
            .FirstOrDefaultAsync(cancellationToken);
        var javaExam = await _context.Exams.Find(exam => exam.Id == BootstrapManifest.JavaExamId)
            .FirstOrDefaultAsync(cancellationToken);
        var cSession = await _context.PracticeSessions.Find(session => session.Id == BootstrapManifest.CSessionId)
            .FirstOrDefaultAsync(cancellationToken);
        var javaSession = await _context.PracticeSessions.Find(session => session.Id == BootstrapManifest.JavaSessionId)
            .FirstOrDefaultAsync(cancellationToken);

        var failures = new List<string>();

        if (!inspection.RequiredPESubmissionIndexesPresent)
        {
            failures.Add("Required PESubmissions indexes are missing.");
        }

        if (inspection.Collections.Any(item => !item.Exists))
        {
            failures.Add("Not all production collections exist.");
        }

        ValidateUser(admin, BootstrapManifest.AdminUserId, "Admin", failures);
        ValidateUser(student, BootstrapManifest.StudentUserId, "Student", failures);
        ValidateCourse(cCourse, BootstrapManifest.CCourseId, failures);
        ValidateCourse(javaCourse, BootstrapManifest.JavaCourseId, failures);
        ValidateQuestion(cQuestion, BootstrapManifest.CQuestionId, BootstrapManifest.CLanguageId, failures);
        ValidateQuestion(javaQuestion, BootstrapManifest.JavaQuestionId, BootstrapManifest.JavaLanguageId, failures);
        ValidateExam(cExam, BootstrapManifest.CExamId, BootstrapManifest.CCourseId, BootstrapManifest.CQuestionId, failures);
        ValidateExam(javaExam, BootstrapManifest.JavaExamId, BootstrapManifest.JavaCourseId, BootstrapManifest.JavaQuestionId, failures);
        ValidateSession(cSession, BootstrapManifest.CSessionId, BootstrapManifest.CExamId, failures);
        ValidateSession(javaSession, BootstrapManifest.JavaSessionId, BootstrapManifest.JavaExamId, failures);

        var cRelationshipOk =
            student is not null &&
            cSession?.StudentId == student.Id &&
            cSession.ExamId == cExam?.Id &&
            cExam.CourseId == cCourse?.Id &&
            cExam.PeExamQuestions.Count == 1 &&
            cExam.PeExamQuestions[0].PeQuestionId == cQuestion?.Id &&
            cQuestion.TestCases.Count >= 1;

        if (!cRelationshipOk)
        {
            failures.Add("C relationship graph verification failed.");
        }

        var javaRelationshipOk =
            student is not null &&
            javaSession?.StudentId == student.Id &&
            javaSession.ExamId == javaExam?.Id &&
            javaExam.CourseId == javaCourse?.Id &&
            javaExam.PeExamQuestions.Count == 1 &&
            javaExam.PeExamQuestions[0].PeQuestionId == javaQuestion?.Id &&
            javaQuestion.TestCases.Count >= 1;

        if (!javaRelationshipOk)
        {
            failures.Add("Java relationship graph verification failed.");
        }

        return new VerificationReport(
            failures.Count == 0,
            failures,
            inspection,
            fixture,
            cQuestion?.TestCases.Count ?? 0,
            cQuestion?.TestCases.Count(item => item.IsHidden) ?? 0,
            javaQuestion?.TestCases.Count ?? 0,
            javaQuestion?.TestCases.Count(item => item.IsHidden) ?? 0,
            cSession?.Status,
            javaSession?.Status,
            cRelationshipOk,
            javaRelationshipOk);
    }

    private async Task<JavaWorkerResultReport> BuildJavaWorkerResultReportAsync(
        CancellationToken cancellationToken)
    {
        var submissions = await GetJavaFixtureSubmissionsAsync(cancellationToken);
        var submission = submissions
            .OrderByDescending(item => item.SubmittedAt)
            .FirstOrDefault();

        var session = await _context.PracticeSessions
            .Find(item => item.Id == BootstrapManifest.JavaSessionId)
            .FirstOrDefaultAsync(cancellationToken);
        var exam = await _context.Exams
            .Find(item => item.Id == BootstrapManifest.JavaExamId)
            .FirstOrDefaultAsync(cancellationToken);
        var question = await _context.PEQuestions
            .Find(item => item.Id == BootstrapManifest.JavaQuestionId)
            .FirstOrDefaultAsync(cancellationToken);
        var student = await _context.Users
            .Find(item => item.Id == BootstrapManifest.StudentUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return JavaWorkerSubmissionVerifier.Validate(
            submission,
            Math.Max(0, submissions.Count - 1),
            student,
            session,
            exam,
            question);
    }

    private async Task<CWorkerResultReport> BuildCWorkerResultReportAsync(
        CancellationToken cancellationToken)
    {
        var submissions = await GetCFixtureSubmissionsAsync(cancellationToken);
        var submission = submissions
            .OrderByDescending(item => item.SubmittedAt)
            .FirstOrDefault();

        var session = await _context.PracticeSessions
            .Find(item => item.Id == BootstrapManifest.CSessionId)
            .FirstOrDefaultAsync(cancellationToken);
        var exam = await _context.Exams
            .Find(item => item.Id == BootstrapManifest.CExamId)
            .FirstOrDefaultAsync(cancellationToken);
        var question = await _context.PEQuestions
            .Find(item => item.Id == BootstrapManifest.CQuestionId)
            .FirstOrDefaultAsync(cancellationToken);
        var student = await _context.Users
            .Find(item => item.Id == BootstrapManifest.StudentUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return CWorkerSubmissionVerifier.Validate(
            submission,
            Math.Max(0, submissions.Count - 1),
            student,
            session,
            exam,
            question);
    }

    private static void ValidateUser(
        User? user,
        string expectedId,
        string expectedRole,
        ICollection<string> failures)
    {
        if (user is null)
        {
            failures.Add($"{expectedRole} fixture user is missing.");
            return;
        }

        if (!ObjectId.TryParse(user.Id, out _) || user.Id != expectedId)
        {
            failures.Add($"{expectedRole} fixture user id is invalid.");
        }

        if (!string.Equals(user.Status, "Active", StringComparison.Ordinal))
        {
            failures.Add($"{expectedRole} fixture user is not Active.");
        }

        if (!string.Equals(user.Role, expectedRole, StringComparison.Ordinal))
        {
            failures.Add($"{expectedRole} fixture user role is invalid.");
        }
    }

    private static void ValidateCourse(
        Course? course,
        string expectedId,
        ICollection<string> failures)
    {
        if (course is null)
        {
            failures.Add($"Course '{expectedId}' is missing.");
            return;
        }

        if (!ObjectId.TryParse(course.Id, out _) || course.Id != expectedId)
        {
            failures.Add($"Course '{expectedId}' has an invalid id.");
        }
    }

    private static void ValidateQuestion(
        PEQuestion? question,
        string expectedId,
        int expectedLanguageId,
        ICollection<string> failures)
    {
        if (question is null)
        {
            failures.Add($"PEQuestion '{expectedId}' is missing.");
            return;
        }

        if (!ObjectId.TryParse(question.Id, out _) || question.Id != expectedId)
        {
            failures.Add($"PEQuestion '{expectedId}' has an invalid id.");
        }

        if (!string.Equals(question.Status, "Active", StringComparison.Ordinal))
        {
            failures.Add($"PEQuestion '{expectedId}' is not Active.");
        }

        if (question.AllowedLanguageIds.Count != 1 ||
            question.AllowedLanguageIds[0] != expectedLanguageId ||
            question.DefaultLanguageId != expectedLanguageId)
        {
            failures.Add($"PEQuestion '{expectedId}' language configuration is invalid.");
        }

        if (question.AllowedLanguageIds.Contains(52))
        {
            failures.Add($"PEQuestion '{expectedId}' unexpectedly includes C++ language id 52.");
        }

        if (!question.TestCases.Any(item => !item.IsHidden && item.IsSample))
        {
            failures.Add($"PEQuestion '{expectedId}' does not contain a sample testcase.");
        }

        if (!question.TestCases.Any(item => item.IsHidden))
        {
            failures.Add($"PEQuestion '{expectedId}' does not contain a hidden testcase.");
        }

        if (question.TestCases.Any(item =>
                string.IsNullOrWhiteSpace(item.ExpectedOutput) ||
                item.TimeLimitMs <= 0 ||
                item.MemoryLimitKb <= 0))
        {
            failures.Add($"PEQuestion '{expectedId}' contains invalid testcase data.");
        }
    }

    private static void ValidateExam(
        Exam? exam,
        string expectedId,
        string expectedCourseId,
        string expectedQuestionId,
        ICollection<string> failures)
    {
        if (exam is null)
        {
            failures.Add($"Exam '{expectedId}' is missing.");
            return;
        }

        if (!ObjectId.TryParse(exam.Id, out _) || exam.Id != expectedId)
        {
            failures.Add($"Exam '{expectedId}' has an invalid id.");
        }

        if (exam.CourseId != expectedCourseId)
        {
            failures.Add($"Exam '{expectedId}' course reference is invalid.");
        }

        if (exam.CreatedBy != BootstrapManifest.AdminUserId)
        {
            failures.Add($"Exam '{expectedId}' CreatedBy does not resolve to the fixture admin.");
        }

        if (exam.PeExamQuestions.Count != 1 ||
            exam.PeExamQuestions[0].PeQuestionId != expectedQuestionId ||
            exam.PeExamQuestions[0].AssignedPoints <= 0)
        {
            failures.Add($"Exam '{expectedId}' PE question linkage is invalid.");
        }
    }

    private static void ValidateSession(
        PracticeSession? session,
        string expectedId,
        string expectedExamId,
        ICollection<string> failures)
    {
        if (session is null)
        {
            failures.Add($"PracticeSession '{expectedId}' is missing.");
            return;
        }

        if (!ObjectId.TryParse(session.Id, out _) || session.Id != expectedId)
        {
            failures.Add($"PracticeSession '{expectedId}' has an invalid id.");
        }

        if (session.StudentId != BootstrapManifest.StudentUserId)
        {
            failures.Add($"PracticeSession '{expectedId}' StudentId is invalid.");
        }

        if (session.ExamId != expectedExamId)
        {
            failures.Add($"PracticeSession '{expectedId}' ExamId is invalid.");
        }

        if (session.Status != SessionStatus.InProgress)
        {
            failures.Add($"PracticeSession '{expectedId}' is not InProgress.");
        }

        if (session.StartTime == default)
        {
            failures.Add($"PracticeSession '{expectedId}' StartTime is invalid.");
        }
    }

    private async Task DeleteIfFixtureAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string id,
        Func<TDocument, bool> fixturePredicate,
        CancellationToken cancellationToken)
    {
        var existing = await collection
            .Find(BuildIdEqualsFilter<TDocument>(id))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return;
        }

        if (!fixturePredicate(existing))
        {
            throw new InvalidOperationException(
                $"Refusing to delete non-fixture document '{id}' from collection '{collection.CollectionNamespace.CollectionName}'.");
        }

        await collection.DeleteOneAsync(
            Builders<TDocument>.Filter.Eq("_id", id),
            cancellationToken);
    }

    private async Task UpsertFixtureAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        TDocument fixtureDocument,
        Func<TDocument, bool> fixturePredicate,
        Func<TDocument, string> identitySelector,
        CancellationToken cancellationToken)
    {
        var id = (string)typeof(TDocument).GetProperty("Id")!
            .GetValue(fixtureDocument)!;

        var existingById = await collection
            .Find(BuildIdEqualsFilter<TDocument>(id))
            .FirstOrDefaultAsync(cancellationToken);

        var action = BootstrapPlanner.DecideAction(
            existingById is not null,
            existingById is not null && fixturePredicate(existingById));

        if (action == FixtureDocumentAction.RejectCollision)
        {
            throw new InvalidOperationException(
                $"Deterministic fixture id '{id}' collides with a non-fixture document in '{collection.CollectionNamespace.CollectionName}'.");
        }

        var naturalKey = identitySelector(fixtureDocument);
        var existingWithSameIdentity = await collection
            .Find(BuildIdNotEqualsFilter<TDocument>(id))
            .ToListAsync(cancellationToken);

        var conflictingNaturalIdentity = existingWithSameIdentity
            .FirstOrDefault(item =>
                string.Equals(
                    identitySelector(item),
                    naturalKey,
                    StringComparison.Ordinal));

        if (conflictingNaturalIdentity is not null &&
            !fixturePredicate(conflictingNaturalIdentity))
        {
            throw new InvalidOperationException(
                $"Fixture identity '{naturalKey}' collides with a non-fixture document in '{collection.CollectionNamespace.CollectionName}'.");
        }

        if (action == FixtureDocumentAction.Insert)
        {
            await collection.InsertOneAsync(
                fixtureDocument,
                cancellationToken: cancellationToken);
            return;
        }

        await collection.ReplaceOneAsync(
            BuildIdEqualsFilter<TDocument>(id),
            fixtureDocument,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
    }

    private static FilterDefinition<TDocument> BuildIdEqualsFilter<TDocument>(
        string id)
    {
        var parameter = Expression.Parameter(typeof(TDocument), "item");
        var property = Expression.Property(parameter, "Id");
        var body = Expression.Equal(property, Expression.Constant(id));

        return Builders<TDocument>.Filter.Where(
            Expression.Lambda<Func<TDocument, bool>>(body, parameter));
    }

    private static FilterDefinition<TDocument> BuildIdNotEqualsFilter<TDocument>(
        string id)
    {
        var parameter = Expression.Parameter(typeof(TDocument), "item");
        var property = Expression.Property(parameter, "Id");
        var body = Expression.NotEqual(property, Expression.Constant(id));

        return Builders<TDocument>.Filter.Where(
            Expression.Lambda<Func<TDocument, bool>>(body, parameter));
    }

    private async Task<HashSet<string>> GetExistingCollectionNamesAsync(
        CancellationToken cancellationToken)
    {
        var names = await _context.Database
            .ListCollectionNames()
            .ToListAsync(cancellationToken);

        return names.ToHashSet(StringComparer.Ordinal);
    }

    private async Task<long> CountFixtureSubmissionsAsync(
        FilterDefinition<PE_Submission> additionalFilter,
        CancellationToken cancellationToken)
    {
        var filter = Builders<PE_Submission>.Filter.And(
            Builders<PE_Submission>.Filter.In(
                submission => submission.SessionId,
                new[]
                {
                    BootstrapManifest.CSessionId,
                    BootstrapManifest.JavaSessionId
                }),
            Builders<PE_Submission>.Filter.In(
                submission => submission.QuestionId,
                new[]
                {
                    BootstrapManifest.CQuestionId,
                    BootstrapManifest.JavaQuestionId
                }),
            additionalFilter);

        return await _context.PE_Submissions.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken);
    }

    private async Task<List<PE_Submission>> GetJavaFixtureSubmissionsAsync(
        CancellationToken cancellationToken)
    {
        return await _context.PE_Submissions
            .Find(submission =>
                submission.SessionId == BootstrapManifest.JavaSessionId &&
                submission.QuestionId == BootstrapManifest.JavaQuestionId)
            .SortByDescending(submission => submission.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<PE_Submission>> GetCFixtureSubmissionsAsync(
        CancellationToken cancellationToken)
    {
        return await _context.PE_Submissions
            .Find(submission =>
                submission.SessionId == BootstrapManifest.CSessionId &&
                submission.QuestionId == BootstrapManifest.CQuestionId &&
                submission.LanguageId == BootstrapManifest.CLanguageId)
            .SortByDescending(submission => submission.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    private static PreWorkerSubmissionVerification VerifyPreWorkerSubmission(
        PE_Submission submission)
    {
        return VerifyPreWorkerSubmission(
            submission,
            BootstrapManifest.JavaSessionId,
            BootstrapManifest.JavaQuestionId,
            BootstrapManifest.JavaLanguageId,
            BootstrapManifest.JavaSourceFilename);
    }

    private static PreWorkerSubmissionVerification VerifyPreWorkerSubmission(
        PE_Submission submission,
        string expectedSessionId,
        string expectedQuestionId,
        int expectedLanguageId,
        string expectedFilename)
    {
        var failures = new List<string>();

        if (submission.SessionId != expectedSessionId)
        {
            failures.Add("SessionId mismatch.");
        }

        if (submission.QuestionId != expectedQuestionId)
        {
            failures.Add("QuestionId mismatch.");
        }

        if (submission.LanguageId != expectedLanguageId)
        {
            failures.Add("LanguageId mismatch.");
        }

        if (submission.SubmittedCode.Count != 1)
        {
            failures.Add("SubmittedCode file count is not 1.");
        }

        if (submission.SubmittedCode.Count == 1 &&
            !string.Equals(
                submission.SubmittedCode.First().Filename,
                expectedFilename,
                StringComparison.Ordinal))
        {
            failures.Add($"SubmittedCode filename is not {expectedFilename}.");
        }

        if (submission.AttemptCount <= 0)
        {
            failures.Add("AttemptCount is not positive.");
        }

        if (submission.MaxScore <= 0)
        {
            failures.Add("MaxScore is not positive.");
        }

        if (submission.SubmittedAt.Kind != DateTimeKind.Utc)
        {
            failures.Add("SubmittedAt is not UTC.");
        }

        if (submission.Status != SubmissionProcessingStatus.Pending)
        {
            failures.Add($"Initial status is {submission.Status}.");
        }

        if (submission.LeaseOwner is not null ||
            submission.LeaseAcquiredAt is not null ||
            submission.LeaseExpiresAt is not null)
        {
            failures.Add("Initial lease state is not empty.");
        }

        return new PreWorkerSubmissionVerification(
            failures.Count == 0,
            failures);
    }

    private static ServiceProvider BuildSubmissionCreationServiceProvider(
        BootstrapConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddSingleton(configuration.RootConfiguration);
        services.AddApplication(configuration.RootConfiguration);
        services.AddSingleton(new DbContext(
            configuration.ConnectionString,
            configuration.DatabaseName));

        services.AddScoped<IPESubmissionRepository, PESubmissionRepository>();
        services.AddScoped<IPracticeSessionRepository, PracticeSessionRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IPEQuestionRepository, PEQuestionRepository>();
        services.AddScoped<IIdGenerator, MongoObjectIdGenerator>();
        services.AddScoped<PESubmissionService>();
        services.AddScoped<IPESubmissionService>(
            provider => provider.GetRequiredService<PESubmissionService>());

        return services.BuildServiceProvider();
    }

    private async Task<PESubmissionIndexReport> BuildPESubmissionIndexReportAsync(
        CancellationToken cancellationToken)
    {
        var indexes = await BootstrapIntrospection.ListIndexesAsync(
            _context.Database.GetCollection<BsonDocument>("PESubmissions"),
            cancellationToken);

        var names = indexes
            .Select(index => index["name"].AsString)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var allRequiredIndexesPresent =
            RequiredPESubmissionIndexNames.All(names.Contains);

        return new PESubmissionIndexReport(names, allRequiredIndexesPresent);
    }

    private static string GetEnvironmentName() =>
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? "Production";

    private static void PrintInspectionReport(InspectionReport report)
    {
        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {report.DatabaseName}");

        foreach (var collection in report.Collections)
        {
            Console.WriteLine(
                $"Collection: {collection.CollectionName}; Exists: {collection.Exists}; DocumentCount: {collection.DocumentCount}; IndexCount: {collection.IndexCount}");
        }

        Console.WriteLine(
            $"Required PESubmission indexes present: {report.RequiredPESubmissionIndexesPresent}");

        foreach (var indexName in report.PESubmissionIndexNames)
        {
            Console.WriteLine($"PESubmissions index: {indexName}");
        }

        Console.WriteLine($"Fixture documents present: {report.FixtureDocumentsPresent}");
        Console.WriteLine($"Pending fixture submissions: {report.PendingFixtureSubmissions}");
        Console.WriteLine($"Processing fixture submissions: {report.ProcessingFixtureSubmissions}");
    }

    private static void PrintVerificationReport(VerificationReport report)
    {
        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {report.Inspection.DatabaseName}");
        Console.WriteLine($"Fixture version: {BootstrapManifest.FixtureVersion}");
        Console.WriteLine($"Verification success: {report.IsSuccess}");
        Console.WriteLine($"C testcase count: {report.CTestCaseCount}");
        Console.WriteLine($"C hidden testcase count: {report.CHiddenTestCaseCount}");
        Console.WriteLine($"Java testcase count: {report.JavaTestCaseCount}");
        Console.WriteLine($"Java hidden testcase count: {report.JavaHiddenTestCaseCount}");
        Console.WriteLine($"ExpectedOutput present: {report.ExpectedOutputPresent}");
        Console.WriteLine($"C session status: {report.CSessionStatus}");
        Console.WriteLine($"Java session status: {report.JavaSessionStatus}");
        Console.WriteLine($"C relationship graph: {report.CRelationshipOk}");
        Console.WriteLine($"Java relationship graph: {report.JavaRelationshipOk}");
        Console.WriteLine($"Fixture submission count: {report.Inspection.FixtureSubmissionCount}");
        Console.WriteLine($"Fixture Pending count: {report.Inspection.PendingFixtureSubmissions}");
        Console.WriteLine($"Fixture Processing count: {report.Inspection.ProcessingFixtureSubmissions}");

        foreach (var failure in report.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }
    }

    private static void PrintCWorkerResultReport(
        string databaseName,
        CWorkerResultReport report)
    {
        if (report.Submission is null)
        {
            Console.WriteLine($"Environment: {GetEnvironmentName()}");
            Console.WriteLine($"Database: {databaseName}");
            Console.WriteLine("CSubmissionFound: False");

            foreach (var failure in report.Failures)
            {
                Console.WriteLine($"Failure: {failure}");
            }

            return;
        }

        var submission = report.Submission;

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {databaseName}");
        Console.WriteLine($"CSubmissionFound: True");
        Console.WriteLine($"SubmissionId: {submission.Id}");
        Console.WriteLine($"Status: {submission.Status}");
        Console.WriteLine($"FinalVerdict: {submission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine($"AttemptCount: {submission.AttemptCount}");
        Console.WriteLine($"ExecutionAttemptCount: {submission.Execution.AttemptCount}");
        Console.WriteLine($"LanguageId: {submission.LanguageId}");
        Console.WriteLine($"SubmittedCodeFileCount: {submission.SubmittedCode.Count}");
        Console.WriteLine($"SubmittedCodeFilename: {submission.SubmittedCode.First().Filename}");
        Console.WriteLine($"TestCasesPassed: {submission.TestCasesPassed}");
        Console.WriteLine($"TotalTestCases: {submission.TotalTestCases}");
        Console.WriteLine($"QuestionScore: {submission.QuestionScore}");
        Console.WriteLine($"MaxScore: {submission.MaxScore}");
        Console.WriteLine($"ProcessingStartedAtPresent: {submission.ProcessingStartedAt is not null}");
        Console.WriteLine($"CompletedAtPresent: {submission.CompletedAt is not null}");
        Console.WriteLine($"LeaseOwnerPresent: {submission.LeaseOwner is not null}");
        Console.WriteLine($"LeaseAcquiredAtPresent: {submission.LeaseAcquiredAt is not null}");
        Console.WriteLine($"LeaseExpiresAtPresent: {submission.LeaseExpiresAt is not null}");
        Console.WriteLine($"DuplicateSubmissionCount: {report.DuplicateSubmissionCount}");
        Console.WriteLine($"StudentRelationshipOk: {report.StudentRelationshipOk}");
        Console.WriteLine($"SessionRelationshipOk: {report.SessionRelationshipOk}");
        Console.WriteLine($"ExamQuestionRelationshipOk: {report.ExamQuestionRelationshipOk}");
        Console.WriteLine($"QuestionActiveOk: {report.QuestionActiveOk}");
        Console.WriteLine($"AllowedLanguageOk: {report.AllowedLanguageOk}");
        Console.WriteLine($"CppExcludedOk: {report.CppExcludedOk}");
        Console.WriteLine($"JavaExcludedOk: {report.JavaExcludedOk}");
        Console.WriteLine($"ExpectedOutputLocalOnlyOk: {report.ExpectedOutputLocalOnlyOk}");
        Console.WriteLine($"VerificationSuccess: {report.IsSuccess}");

        foreach (var failure in report.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }
    }

    private static void PrintPreWorkerSubmissionReport(
        string databaseName,
        PE_Submission submission,
        PreWorkerSubmissionVerification verification)
    {
        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {databaseName}");
        Console.WriteLine($"CreatedSubmissionId: {submission.Id}");
        Console.WriteLine($"SessionId: {submission.SessionId}");
        Console.WriteLine($"QuestionId: {submission.QuestionId}");
        Console.WriteLine($"LanguageId: {submission.LanguageId}");
        Console.WriteLine($"SubmittedCodeFileCount: {submission.SubmittedCode.Count}");
        Console.WriteLine($"SubmittedCodeFilename: {submission.SubmittedCode.First().Filename}");
        Console.WriteLine($"AttemptCount: {submission.AttemptCount}");
        Console.WriteLine($"MaxScore: {submission.MaxScore}");
        Console.WriteLine($"SubmittedAtUtc: {submission.SubmittedAt:o}");
        Console.WriteLine($"Status: {submission.Status}");
        Console.WriteLine($"LeaseOwnerIsNull: {submission.LeaseOwner is null}");
        Console.WriteLine($"LeaseAcquiredAtIsNull: {submission.LeaseAcquiredAt is null}");
        Console.WriteLine($"LeaseExpiresAtIsNull: {submission.LeaseExpiresAt is null}");
        Console.WriteLine($"PreWorkerVerificationSuccess: {verification.IsSuccess}");

        foreach (var failure in verification.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }
    }

    private static void PrintJavaWorkerResultReport(
        string databaseName,
        JavaWorkerResultReport report)
    {
        if (report.Submission is null)
        {
            Console.WriteLine($"Environment: {GetEnvironmentName()}");
            Console.WriteLine($"Database: {databaseName}");
            Console.WriteLine("JavaSubmissionFound: False");

            foreach (var failure in report.Failures)
            {
                Console.WriteLine($"Failure: {failure}");
            }

            return;
        }

        var submission = report.Submission;

        Console.WriteLine($"Environment: {GetEnvironmentName()}");
        Console.WriteLine($"Database: {databaseName}");
        Console.WriteLine($"JavaSubmissionFound: True");
        Console.WriteLine($"SubmissionId: {submission.Id}");
        Console.WriteLine($"Status: {submission.Status}");
        Console.WriteLine($"FinalVerdict: {submission.FinalVerdict?.ToString() ?? "null"}");
        Console.WriteLine($"AttemptCount: {submission.AttemptCount}");
        Console.WriteLine($"ExecutionAttemptCount: {submission.Execution.AttemptCount}");
        Console.WriteLine($"LanguageId: {submission.LanguageId}");
        Console.WriteLine($"SubmittedCodeFileCount: {submission.SubmittedCode.Count}");
        Console.WriteLine($"SubmittedCodeFilename: {submission.SubmittedCode.First().Filename}");
        Console.WriteLine($"TestCasesPassed: {submission.TestCasesPassed}");
        Console.WriteLine($"TotalTestCases: {submission.TotalTestCases}");
        Console.WriteLine($"QuestionScore: {submission.QuestionScore}");
        Console.WriteLine($"MaxScore: {submission.MaxScore}");
        Console.WriteLine($"ProcessingStartedAtPresent: {submission.ProcessingStartedAt is not null}");
        Console.WriteLine($"CompletedAtPresent: {submission.CompletedAt is not null}");
        Console.WriteLine($"LeaseOwnerPresent: {submission.LeaseOwner is not null}");
        Console.WriteLine($"LeaseAcquiredAtPresent: {submission.LeaseAcquiredAt is not null}");
        Console.WriteLine($"LeaseExpiresAtPresent: {submission.LeaseExpiresAt is not null}");
        Console.WriteLine($"DuplicateSubmissionCount: {report.DuplicateSubmissionCount}");
        Console.WriteLine($"StudentRelationshipOk: {report.StudentRelationshipOk}");
        Console.WriteLine($"SessionRelationshipOk: {report.SessionRelationshipOk}");
        Console.WriteLine($"ExamQuestionRelationshipOk: {report.ExamQuestionRelationshipOk}");
        Console.WriteLine($"QuestionActiveOk: {report.QuestionActiveOk}");
        Console.WriteLine($"AllowedLanguageOk: {report.AllowedLanguageOk}");
        Console.WriteLine($"CppExcludedOk: {report.CppExcludedOk}");
        Console.WriteLine($"ExpectedOutputLocalOnlyOk: {report.ExpectedOutputLocalOnlyOk}");
        Console.WriteLine($"VerificationSuccess: {report.IsSuccess}");

        foreach (var failure in report.Failures)
        {
            Console.WriteLine($"Failure: {failure}");
        }
    }
}

internal sealed record CollectionInspectionReport(
    string CollectionName,
    bool Exists,
    long DocumentCount,
    int IndexCount);

internal sealed record PESubmissionIndexReport(
    IReadOnlyList<string> IndexNames,
    bool AllRequiredIndexesPresent);

internal sealed record InspectionReport(
    string DatabaseName,
    IReadOnlyList<CollectionInspectionReport> Collections,
    IReadOnlyList<string> PESubmissionIndexNames,
    bool RequiredPESubmissionIndexesPresent,
    long FixtureDocumentCount,
    long FixtureSubmissionCount,
    long PendingFixtureSubmissions,
    long ProcessingFixtureSubmissions)
{
    public bool FixtureDocumentsPresent => FixtureDocumentCount > 0;
}

internal sealed record VerificationReport(
    bool IsSuccess,
    IReadOnlyList<string> Failures,
    InspectionReport Inspection,
    FixtureBundle Fixture,
    int CTestCaseCount,
    int CHiddenTestCaseCount,
    int JavaTestCaseCount,
    int JavaHiddenTestCaseCount,
    SessionStatus? CSessionStatus,
    SessionStatus? JavaSessionStatus,
    bool CRelationshipOk,
    bool JavaRelationshipOk)
{
    public bool ExpectedOutputPresent =>
        Fixture.CQuestion.TestCases.All(item => !string.IsNullOrWhiteSpace(item.ExpectedOutput)) &&
        Fixture.JavaQuestion.TestCases.All(item => !string.IsNullOrWhiteSpace(item.ExpectedOutput));
}

internal sealed record PreWorkerSubmissionVerification(
    bool IsSuccess,
    IReadOnlyList<string> Failures);

public sealed record JavaWorkerResultReport(
    bool IsSuccess,
    PE_Submission? Submission,
    int DuplicateSubmissionCount,
    IReadOnlyList<string> Failures,
    bool StudentRelationshipOk,
    bool SessionRelationshipOk,
    bool ExamQuestionRelationshipOk,
    bool QuestionActiveOk,
    bool AllowedLanguageOk,
    bool CppExcludedOk,
    bool ExpectedOutputLocalOnlyOk,
    bool ActiveLeaseRetained,
    bool TerminalCompleted);

internal sealed class MockHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "APE_BE";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
