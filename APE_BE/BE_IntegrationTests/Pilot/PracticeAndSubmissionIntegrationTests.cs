using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using Application.Models;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using Domain.Enums;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class PracticeAndSubmissionIntegrationTests
    : IntegrationTestBase
{
    [TestMethod]
    public async Task API_PRACTICE_001_StartSession_ThenGetAndResolveActiveSession()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var startResponse = await client.PostAsJsonAsync(
            "/api/student/practice/start",
            new StartSessionDto
            {
                ExamId = scenario.PublicExam.Id
            });

        HttpResponseAssertions.AssertStatusCode(startResponse, HttpStatusCode.OK);

        var startPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StartSessionResultDto>>(startResponse);
        Assert.IsNotNull(startPayload.Data);

        using var sessionResponse = await client.GetAsync($"/api/student/practice/sessions/{startPayload.Data.SessionId}");
        using var activeResponse = await client.GetAsync($"/api/student/practice/exams/{scenario.PublicExam.Id}/active-session");

        HttpResponseAssertions.AssertStatusCode(sessionResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(activeResponse, HttpStatusCode.OK);

        var sessionPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PracticeSessionDto>>(sessionResponse);
        var activePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ActivePracticeSessionSummaryDto?>>(activeResponse);

        Assert.IsNotNull(sessionPayload.Data);
        Assert.AreEqual(TestIdentity.StudentA.UserId, sessionPayload.Data.StudentId);
        Assert.AreEqual(scenario.PublicExam.Id, sessionPayload.Data.ExamId);
        Assert.AreEqual(SessionStatus.InProgress, sessionPayload.Data.Status);

        Assert.IsNotNull(activePayload.Data);
        Assert.AreEqual(startPayload.Data.SessionId, activePayload.Data.SessionId);
        Assert.AreEqual(scenario.PublicExam.Id, activePayload.Data.ExamId);

        var storedSession = await DbContext.PracticeSessions
            .Find(item => item.Id == startPayload.Data.SessionId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedSession);
        Assert.AreEqual(TestIdentity.StudentA.UserId, storedSession.StudentId);
        Assert.AreEqual(scenario.PublicExam.Id, storedSession.ExamId);
        Assert.AreEqual(SessionStatus.InProgress, storedSession.Status);
    }

    [TestMethod]
    public async Task API_PRACTICE_002_SaveDraft_ThenPauseResume_UpdatesLifecycleState()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        var draftRequest = new SaveDraftCodeDto
        {
            QuestionId = scenario.Question.Id,
            SelectedQuestionId = scenario.Question.Id,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = "main.c",
                    Content = "int main(){return 0;}"
                }
            ]
        };

        using var draftResponse = await client.PutAsJsonAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}/draft-code",
            draftRequest);
        using var pauseResponse = await client.PostAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}/pause",
            content: null);
        using var resumeResponse = await client.PostAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}/resume",
            content: null);
        using var sessionResponse = await client.GetAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}");

        HttpResponseAssertions.AssertStatusCode(draftResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(pauseResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(resumeResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(sessionResponse, HttpStatusCode.OK);

        var resumedPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PracticeSessionDto>>(resumeResponse);
        var sessionPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PracticeSessionDto>>(sessionResponse);

        Assert.IsNotNull(resumedPayload.Data);
        Assert.IsFalse(resumedPayload.Data.IsPaused);
        Assert.AreEqual(scenario.Question.Id, resumedPayload.Data.SelectedQuestionId);
        Assert.AreEqual(1, resumedPayload.Data.DraftCodes.Count);

        Assert.IsNotNull(sessionPayload.Data);
        Assert.AreEqual(1, sessionPayload.Data.DraftCodes.Count);
        Assert.AreEqual("main.c", sessionPayload.Data.DraftCodes.Single().Files.Single().Filename);

        var storedSession = await DbContext.PracticeSessions
            .Find(item => item.Id == scenario.StudentASession.Id)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedSession);
        Assert.IsFalse(storedSession.IsPaused);
        Assert.AreEqual(scenario.Question.Id, storedSession.SelectedQuestionId);
        Assert.AreEqual(1, storedSession.DraftCodes.Count);
    }

    [TestMethod]
    public async Task API_PRACTICE_003_EndSession_ThenHistory_ReflectsCompletedScoredSession()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var startResponse = await client.PostAsJsonAsync(
            "/api/student/practice/start",
            new StartSessionDto
            {
                ExamId = scenario.PublicExam.Id
            });

        var startPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StartSessionResultDto>>(startResponse);
        Assert.IsNotNull(startPayload.Data);

        using var submitResponse = await client.PostAsJsonAsync(
            "/api/student/submissions/fe",
            new FESubmissionInputDto
            {
                SessionId = startPayload.Data.SessionId,
                QuestionId = scenario.SystemFeEasy.Id,
                UserAnswer = ["A"]
            });
        using var endResponse = await client.PostAsync(
            $"/api/student/practice/sessions/{startPayload.Data.SessionId}/end",
            content: null);
        using var historyResponse = await client.GetAsync("/api/student/practice/history?page=1&limit=20");

        HttpResponseAssertions.AssertStatusCode(submitResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(endResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(historyResponse, HttpStatusCode.OK);

        var historyPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PaginatedResult<PracticeHistoryItemDto>>>(historyResponse);
        Assert.IsNotNull(historyPayload.Data);

        var historyItem = historyPayload.Data.Items.Single(item => item.SessionId == startPayload.Data.SessionId);
        Assert.AreEqual("Completed", historyItem.Status);
        Assert.AreEqual(1d, historyItem.TotalScore);

        var storedSession = await DbContext.PracticeSessions
            .Find(item => item.Id == startPayload.Data.SessionId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedSession);
        Assert.AreEqual(SessionStatus.Submitted, storedSession.Status);
        Assert.AreEqual(1d, storedSession.TotalScore);
    }

    [TestMethod]
    public async Task API_PRACTICE_004_GetSession_ForAnotherStudentsSession_IsRejected()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentB);

        using var getResponse = await client.GetAsync($"/api/student/practice/sessions/{scenario.StudentASession.Id}");
        using var draftResponse = await client.PutAsJsonAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}/draft-code",
            new SaveDraftCodeDto
            {
                QuestionId = scenario.Question.Id,
                Files =
                [
                    new SubmittedCodeFileDto
                    {
                        Filename = "main.c",
                        Content = "unauthorized draft"
                    }
                ]
            });
        using var pauseResponse = await client.PostAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}/pause",
            content: null);
        using var resumeResponse = await client.PostAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}/resume",
            content: null);
        using var endResponse = await client.PostAsync(
            $"/api/student/practice/sessions/{scenario.StudentASession.Id}/end",
            content: null);

        HttpResponseAssertions.AssertStatusCode(getResponse, HttpStatusCode.NotFound);
        HttpResponseAssertions.AssertStatusCode(draftResponse, HttpStatusCode.BadRequest);
        HttpResponseAssertions.AssertStatusCode(pauseResponse, HttpStatusCode.BadRequest);
        HttpResponseAssertions.AssertStatusCode(resumeResponse, HttpStatusCode.BadRequest);
        HttpResponseAssertions.AssertStatusCode(endResponse, HttpStatusCode.BadRequest);

        var getPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PracticeSessionDto>>(getResponse);
        var draftPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<bool>>(draftResponse);
        var pausePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<bool>>(pauseResponse);
        var resumePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PracticeSessionDto>>(resumeResponse);
        var endPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<bool>>(endResponse);

        Assert.IsFalse(getPayload.Success);
        Assert.AreEqual("You do not have permission to view this session", getPayload.Error);
        Assert.IsFalse(draftPayload.Success);
        Assert.AreEqual("You do not have permission to update this session.", draftPayload.Error);
        Assert.IsFalse(pausePayload.Success);
        Assert.AreEqual("You do not have permission to access this session.", pausePayload.Error);
        Assert.IsFalse(resumePayload.Success);
        Assert.AreEqual("You do not have permission to access this session.", resumePayload.Error);
        Assert.IsFalse(endPayload.Success);
        Assert.AreEqual("You do not have permission to access this session.", endPayload.Error);

        var storedSession = await DbContext.PracticeSessions
            .Find(item => item.Id == scenario.StudentASession.Id)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedSession);
        Assert.AreEqual(TestIdentity.StudentA.UserId, storedSession.StudentId);
        Assert.AreEqual(SessionStatus.InProgress, storedSession.Status);
        Assert.AreEqual(0, storedSession.DraftCodes.Count);
    }

    [TestMethod]
    public async Task API_PE_SUB_001_RunProgrammingSubmission_UsesControlledJudge0ThroughRealHttpPipeline()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();
        var peSubmissionCountBefore = await DbContext.PE_Submissions
            .CountDocumentsAsync(FilterDefinition<PE_Submission>.Empty);

        using var client = CreateClient(TestIdentity.StudentA);

        var request = new PECodeRunInputDto
        {
            QuestionId = scenario.Question.Id,
            Files =
            [
                new CodeFileDto
                {
                    Filename = "main.c",
                    Content = "#include <stdio.h>\nint main(){return 0;}"
                }
            ]
        };

        using var response = await client.PostAsJsonAsync("/api/student/submissions/pe/run", request);

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PECodeRunResultDto>>(response);
        Assert.IsTrue(payload.Success);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual(1, payload.Data.PassedSampleCases);
        Assert.AreEqual(1, payload.Data.TotalSampleCases);
        Assert.AreEqual("Accepted", payload.Data.SampleResults.Single().Status);

        var peSubmissionCountAfter = await DbContext.PE_Submissions
            .CountDocumentsAsync(FilterDefinition<PE_Submission>.Empty);

        Assert.AreEqual(peSubmissionCountBefore, peSubmissionCountAfter);
    }

    [TestMethod]
    public async Task API_PE_SUB_002_SubmitProgrammingSubmission_PersistsAcceptedPendingRecord()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        var request = new PESubmissionInputDto
        {
            SessionId = scenario.StudentASession.Id,
            QuestionId = scenario.Question.Id,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = "main.c",
                    Content = "#include <stdio.h>\nint main(){return 0;}"
                }
            ]
        };

        using var response = await client.PostAsJsonAsync("/api/student/submissions/pe", request);

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);

        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionAcceptedDto>>(response);
        Assert.IsTrue(payload.Success);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual(SubmissionProcessingStatus.Pending, payload.Data.Status);

        var storedSubmission = await DbContext.PE_Submissions
            .Find(item => item.Id == payload.Data.SubmissionId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedSubmission);
        Assert.AreEqual(scenario.StudentASession.Id, storedSubmission.SessionId);
        Assert.AreEqual(SubmissionProcessingStatus.Pending, storedSubmission.Status);
    }

    [TestMethod]
    public async Task API_PE_SUB_003_GetProgrammingSubmission_EventuallyReturnsCompletedResultFromWorker()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        var submitRequest = new PESubmissionInputDto
        {
            SessionId = scenario.StudentASession.Id,
            QuestionId = scenario.Question.Id,
            Files =
            [
                new SubmittedCodeFileDto
                {
                    Filename = "main.c",
                    Content = "#include <stdio.h>\nint main(){return 0;}"
                }
            ]
        };

        using var submitResponse = await client.PostAsJsonAsync("/api/student/submissions/pe", submitRequest);
        Assert.AreEqual(HttpStatusCode.Accepted, submitResponse.StatusCode);

        var accepted = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionAcceptedDto>>(submitResponse);
        Assert.IsNotNull(accepted.Data);

        var detail = await PollingHelper.WaitAsync(
            async () =>
            {
                using var getResponse = await client.GetAsync($"/api/student/submissions/pe/{accepted.Data.SubmissionId}");
                HttpResponseAssertions.AssertStatusCode(getResponse, HttpStatusCode.OK);
                var getPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PESubmissionDetailDto>>(getResponse);
                Assert.IsNotNull(getPayload.Data);
                return getPayload.Data;
            },
            data => data.Status == SubmissionProcessingStatus.Completed,
            timeout: TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromMilliseconds(500),
            failureMessage: "PE submission did not reach Completed state.");

        Assert.AreEqual(SubmissionProcessingStatus.Completed, detail.Status);
        Assert.AreEqual(SubmissionVerdict.Accepted, detail.FinalVerdict);
        Assert.AreEqual(2, detail.TotalTestCases);
        Assert.AreEqual(2, detail.TestCasesPassed);
        Assert.AreEqual(10d, detail.QuestionScore);

        var storedSubmission = await DbContext.PE_Submissions
            .Find(item => item.Id == accepted.Data.SubmissionId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedSubmission);
        Assert.AreEqual(SubmissionProcessingStatus.Completed, storedSubmission.Status);
        Assert.AreEqual(SubmissionVerdict.Accepted, storedSubmission.FinalVerdict);
        Assert.AreEqual(2, storedSubmission.TestCasesPassed);
    }

    [TestMethod]
    public async Task API_FE_SUB_001_SubmitFeAnswer_ThenGetSessionAggregate_PersistsResult()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var startResponse = await client.PostAsJsonAsync(
            "/api/student/practice/start",
            new StartSessionDto
            {
                ExamId = scenario.PublicExam.Id
            });

        var startPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StartSessionResultDto>>(startResponse);
        Assert.IsNotNull(startPayload.Data);

        using var submitResponse = await client.PostAsJsonAsync(
            "/api/student/submissions/fe",
            new FESubmissionInputDto
            {
                SessionId = startPayload.Data.SessionId,
                QuestionId = scenario.SystemFeEasy.Id,
                UserAnswer = ["A"]
            });
        using var aggregateResponse = await client.GetAsync(
            $"/api/student/submissions/session/{startPayload.Data.SessionId}");

        HttpResponseAssertions.AssertStatusCode(submitResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(aggregateResponse, HttpStatusCode.OK);

        var submitPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<FESubmissionResultDto>>(submitResponse);
        var aggregatePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionSessionResultDto>>(aggregateResponse);

        Assert.IsNotNull(submitPayload.Data);
        Assert.IsTrue(submitPayload.Data.IsCorrect);
        Assert.AreEqual(1d, submitPayload.Data.EarnedPoints);

        Assert.IsNotNull(aggregatePayload.Data);
        Assert.AreEqual(1, aggregatePayload.Data.FESubmissionCount);
        Assert.AreEqual(1, aggregatePayload.Data.TotalSubmissionCount);
        Assert.AreEqual(submitPayload.Data.Id, aggregatePayload.Data.FE.Single().Id);

        var storedSubmission = await DbContext.FESubmissions
            .Find(item => item.Id == submitPayload.Data.Id)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedSubmission);
        Assert.AreEqual(startPayload.Data.SessionId, storedSubmission.SessionId);
        Assert.IsTrue(storedSubmission.IsCorrect);
    }

    [TestMethod]
    public async Task API_FE_SUB_002_InvalidFeSubmission_IsRejected_WithoutPersistence()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var startResponse = await client.PostAsJsonAsync(
            "/api/student/practice/start",
            new StartSessionDto
            {
                ExamId = scenario.PublicExam.Id
            });

        var startPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StartSessionResultDto>>(startResponse);
        Assert.IsNotNull(startPayload.Data);

        using var response = await client.PostAsJsonAsync(
            "/api/student/submissions/fe",
            new FESubmissionInputDto
            {
                SessionId = startPayload.Data.SessionId,
                QuestionId = scenario.PrivateByosFe.Id,
                UserAnswer = ["A"]
            });

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.BadRequest);

        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<FESubmissionResultDto>>(response);
        Assert.IsFalse(payload.Success);
        Assert.AreEqual("Question does not belong to this exam", payload.Error);

        var storedCount = await DbContext.FESubmissions.CountDocumentsAsync(FilterDefinition<FE_Submission>.Empty);
        Assert.AreEqual(0, storedCount);
    }

    [TestMethod]
    public async Task API_PE_SUB_004_GetSessionAggregate_ReturnsMixedFeAndPeResults()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var startResponse = await client.PostAsJsonAsync(
            "/api/student/practice/start",
            new StartSessionDto
            {
                ExamId = scenario.MixedExam.Id
            });

        var startPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StartSessionResultDto>>(startResponse);
        Assert.IsNotNull(startPayload.Data);

        using var feResponse = await client.PostAsJsonAsync(
            "/api/student/submissions/fe",
            new FESubmissionInputDto
            {
                SessionId = startPayload.Data.SessionId,
                QuestionId = scenario.SystemFeEasy.Id,
                UserAnswer = ["A"]
            });
        using var peSubmitResponse = await client.PostAsJsonAsync(
            "/api/student/submissions/pe",
            new PESubmissionInputDto
            {
                SessionId = startPayload.Data.SessionId,
                QuestionId = scenario.SystemPe.Id,
                Files =
                [
                    new SubmittedCodeFileDto
                    {
                        Filename = "main.c",
                        Content = "#include <stdio.h>\nint main(){return 0;}"
                    }
                ]
            });

        var accepted = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionAcceptedDto>>(peSubmitResponse);
        Assert.IsNotNull(accepted.Data);

        var detail = await PollUntilCompletedAsync(client, accepted.Data.SubmissionId);

        using var aggregateResponse = await client.GetAsync($"/api/student/submissions/session/{startPayload.Data.SessionId}");
        using var peSessionResponse = await client.GetAsync($"/api/student/submissions/pe/session/{startPayload.Data.SessionId}");

        HttpResponseAssertions.AssertStatusCode(feResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(peSubmitResponse, HttpStatusCode.Accepted);
        HttpResponseAssertions.AssertStatusCode(aggregateResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(peSessionResponse, HttpStatusCode.OK);

        var aggregatePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionSessionResultDto>>(aggregateResponse);
        var peSessionPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<IReadOnlyCollection<PESubmissionSummaryDto>>>(peSessionResponse);

        Assert.IsNotNull(aggregatePayload.Data);
        Assert.AreEqual(1, aggregatePayload.Data.FESubmissionCount);
        Assert.AreEqual(1, aggregatePayload.Data.PESubmissionCount);
        Assert.AreEqual(2, aggregatePayload.Data.TotalSubmissionCount);
        Assert.AreEqual(detail.Id, aggregatePayload.Data.PE.Single().Id);

        Assert.IsNotNull(peSessionPayload.Data);
        Assert.AreEqual(1, peSessionPayload.Data.Count);
        Assert.AreEqual(detail.Id, peSessionPayload.Data.Single().Id);
    }

    [TestMethod]
    public async Task API_PE_SUB_005_InvalidProgrammingRequests_AreRejected_WithoutPersistence()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var submitResponse = await client.PostAsJsonAsync(
            "/api/student/submissions/pe",
            new PESubmissionInputDto
            {
                SessionId = scenario.StudentASession.Id,
                QuestionId = "64b00000000000000000ffff",
                Files =
                [
                    new SubmittedCodeFileDto
                    {
                        Filename = "main.c",
                        Content = "int main(){return 0;}"
                    }
                ]
            });
        using var runResponse = await client.PostAsJsonAsync(
            "/api/student/submissions/pe/run",
            new PECodeRunInputDto
            {
                QuestionId = scenario.Question.Id,
                Files = []
            });

        HttpResponseAssertions.AssertStatusCode(submitResponse, HttpStatusCode.BadRequest);
        HttpResponseAssertions.AssertStatusCode(runResponse, HttpStatusCode.BadRequest);

        var submitPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionAcceptedDto>>(submitResponse);
        Assert.IsFalse(submitPayload.Success);
        Assert.AreEqual("Question does not belong to this exam.", submitPayload.Error);

        var peCount = await DbContext.PE_Submissions.CountDocumentsAsync(FilterDefinition<PE_Submission>.Empty);
        Assert.AreEqual(0, peCount);
    }

    [TestMethod]
    public async Task API_PE_SUB_006_OtherStudent_CannotReadPeSubmissionOrSessionAggregates()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();

        using var ownerClient = CreateClient(TestIdentity.StudentA);

        using var submitResponse = await ownerClient.PostAsJsonAsync(
            "/api/student/submissions/pe",
            new PESubmissionInputDto
            {
                SessionId = scenario.StudentASession.Id,
                QuestionId = scenario.Question.Id,
                Files =
                [
                    new SubmittedCodeFileDto
                    {
                        Filename = "main.c",
                        Content = "#include <stdio.h>\nint main(){return 0;}"
                    }
                ]
            });

        var accepted = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionAcceptedDto>>(submitResponse);
        Assert.IsNotNull(accepted.Data);
        await PollUntilCompletedAsync(ownerClient, accepted.Data.SubmissionId);

        using var otherClient = CreateClient(TestIdentity.StudentB);

        using var detailResponse = await otherClient.GetAsync($"/api/student/submissions/pe/{accepted.Data.SubmissionId}");
        using var peSessionResponse = await otherClient.GetAsync($"/api/student/submissions/pe/session/{scenario.StudentASession.Id}");
        using var aggregateResponse = await otherClient.GetAsync($"/api/student/submissions/session/{scenario.StudentASession.Id}");

        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.NotFound);
        HttpResponseAssertions.AssertStatusCode(peSessionResponse, HttpStatusCode.BadRequest);
        HttpResponseAssertions.AssertStatusCode(aggregateResponse, HttpStatusCode.NotFound);

        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PESubmissionDetailDto>>(detailResponse);
        var peSessionPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<IReadOnlyCollection<PESubmissionSummaryDto>>>(peSessionResponse);
        var aggregatePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionSessionResultDto>>(aggregateResponse);

        Assert.IsFalse(detailPayload.Success);
        Assert.AreEqual("You do not have permission to view this submission.", detailPayload.Error);
        Assert.IsFalse(peSessionPayload.Success);
        Assert.AreEqual("You do not have permission to view this session.", peSessionPayload.Error);
        Assert.IsFalse(aggregatePayload.Success);
        Assert.AreEqual("Practice session not found.", aggregatePayload.Error);
    }

    [TestMethod]
    public async Task API_PE_SUB_007_RequestMentor_ThenReadLatestAndHistory_PersistsArtifacts()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedProgrammingPracticeScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var submitResponse = await client.PostAsJsonAsync(
            "/api/student/submissions/pe",
            new PESubmissionInputDto
            {
                SessionId = scenario.StudentASession.Id,
                QuestionId = scenario.Question.Id,
                Files =
                [
                    new SubmittedCodeFileDto
                    {
                        Filename = "main.c",
                        Content = "#include <stdio.h>\nint main(){return 0;}"
                    }
                ]
            });

        var accepted = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<SubmissionAcceptedDto>>(submitResponse);
        Assert.IsNotNull(accepted.Data);

        var detail = await PollUntilCompletedAsync(client, accepted.Data.SubmissionId);
        Assert.AreEqual(SubmissionProcessingStatus.Completed, detail.Status);

        using var mentorResponse = await client.PostAsJsonAsync(
            $"/api/student/submissions/{accepted.Data.SubmissionId}/mentor",
            new CodeMentorRequestDto());
        using var latestResponse = await client.GetAsync(
            $"/api/student/submissions/{accepted.Data.SubmissionId}/mentor-feedback");
        using var historyResponse = await client.GetAsync(
            $"/api/student/submissions/{accepted.Data.SubmissionId}/mentor-feedbacks?take=20");

        HttpResponseAssertions.AssertStatusCode(mentorResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(latestResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(historyResponse, HttpStatusCode.OK);

        var mentorPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<CodeMentorResultDto>>(mentorResponse);
        var latestPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<MentorFeedbackDto>>(latestResponse);
        var historyPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<MentorFeedbackDto>>>(historyResponse);

        Assert.IsNotNull(mentorPayload.Data);
        Assert.AreEqual(accepted.Data.SubmissionId, mentorPayload.Data.SubmissionId);
        Assert.AreEqual("ControlledAI", mentorPayload.Data.Provider);
        Assert.AreEqual("mentor-v1", mentorPayload.Data.EffectiveModel);
        Assert.AreEqual("needs_fix", mentorPayload.Data.Feedback.Verdict);

        Assert.IsNotNull(latestPayload.Data);
        Assert.AreEqual("needs_fix", latestPayload.Data.Verdict);
        Assert.AreEqual("PE", latestPayload.Data.QuestionType);

        Assert.IsNotNull(historyPayload.Data);
        Assert.AreEqual(1, historyPayload.Data.Count);
        Assert.AreEqual("needs_fix", historyPayload.Data.Single().Verdict);

        var storedFeedback = await DbContext.AIMentorFeedbacks
            .Find(item => item.SubmissionId == accepted.Data.SubmissionId)
            .FirstOrDefaultAsync();
        var storedUsageLog = await DbContext.APIUsageLogs
            .Find(item => item.TriggeredBy == TestIdentity.StudentA.UserId)
            .FirstOrDefaultAsync();
        var storedBilling = await DbContext.AIVndBillingTransactions
            .Find(item => item.SourceEntityId == accepted.Data.SubmissionId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedFeedback);
        Assert.AreEqual("needs_fix", storedFeedback.Verdict);
        Assert.IsNotNull(storedUsageLog);
        Assert.IsTrue(storedUsageLog.TokensUsed > 0);
        Assert.IsNotNull(storedBilling);
        Assert.AreEqual("Submission", storedBilling.SourceEntityType);
        Assert.AreEqual(accepted.Data.SubmissionId, storedBilling.SourceEntityId);
    }

    private static async Task<PESubmissionDetailDto> PollUntilCompletedAsync(
        HttpClient client,
        string submissionId)
    {
        return await PollingHelper.WaitAsync(
            async () =>
            {
                using var getResponse = await client.GetAsync($"/api/student/submissions/pe/{submissionId}");
                HttpResponseAssertions.AssertStatusCode(getResponse, HttpStatusCode.OK);
                var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PESubmissionDetailDto>>(getResponse);
                Assert.IsNotNull(payload.Data);
                return payload.Data;
            },
            data => data.Status == SubmissionProcessingStatus.Completed,
            timeout: TimeSpan.FromSeconds(20),
            interval: TimeSpan.FromMilliseconds(500),
            failureMessage: "PE submission did not reach Completed state.");
    }
}
