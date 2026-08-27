using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class AIQuestionGenerationIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task API_AIQG_001_AdminGenerateReview_PersistsGeneratedDraftsAndUsage()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.Admin);

        using var response = await client.PostAsJsonAsync(
            "/api/ai/questions/generate-review",
            BuildAdminGenerationRequest(scenario));

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<GenerationReviewResultDto>>(response);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual("needs_revision", payload.Data.Review.ReviewStatus, ignoreCase: true);
        Assert.AreEqual(10, payload.Data.Questions.Count);
        Assert.IsTrue(payload.Data.Persistence.Persisted);
        Assert.AreEqual(10, payload.Data.Persistence.PersistedCount);
        Assert.IsNotNull(payload.Data.ShortfallReport);
        Assert.AreEqual("review_rejected_low_groundedness", payload.Data.ShortfallReport.StopReason);

        var createdCount = await DbContext.FEQuestions.CountDocumentsAsync(item => item.CreatedBy == TestIdentity.Admin.UserId && item.Source == "Admin");
        var usageLog = await DbContext.APIUsageLogs.Find(item => item.TriggeredBy == TestIdentity.Admin.UserId).FirstOrDefaultAsync();
        Assert.IsTrue(createdCount >= 10);
        Assert.IsNotNull(usageLog);
    }

    [TestMethod]
    public async Task API_AIQG_002_SystemPoolGeneration_PersistsStudentGeneratedDrafts()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var response = await client.PostAsJsonAsync(
            "/api/ai/questions/generate-review/system",
            new SystemQuestionGenerationRequestDto
            {
                CourseId = scenario.Course.Id,
                DocumentId = scenario.SystemDocument.Id,
                Subject = "PRN212",
                ChapterKeys = ["chapter-1"],
                Difficulty = "Easy",
                QuestionType = "FE",
                Count = 10,
                PersistQuestions = true,
                IsPublic = false,
                TargetTopics = ["queues"]
            });

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<GenerationReviewResultDto>>(response);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual("SYSTEM", payload.Data.SourceScope);
        Assert.AreEqual(10, payload.Data.Persistence.PersistedCount);

        var created = await DbContext.FEQuestions.Find(item =>
                item.CreatedBy == TestIdentity.StudentA.UserId &&
                item.Source == "Student" &&
                item.Title.StartsWith("Controlled FE Queue Basics"))
            .ToListAsync();
        Assert.AreEqual(10, created.Count);
        Assert.IsTrue(created.All(item => item.Status == "Draft"));
    }

    [TestMethod]
    public async Task API_AIQG_003_ByosGeneration_PersistsOwnedStudentDrafts()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var response = await client.PostAsJsonAsync(
            "/api/ai/questions/generate-review/byos",
            new ByosQuestionGenerationRequestDto
            {
                DocumentId = scenario.StudentADocument.Id,
                Subject = "PRN212",
                ChapterKeys = ["chapter-1"],
                Difficulty = "Easy",
                QuestionType = "FE",
                Count = 10,
                PersistQuestions = true,
                IsPublic = false,
                TargetTopics = ["queues"]
            });

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<GenerationReviewResultDto>>(response);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual("BYOS", payload.Data.SourceScope);
        Assert.AreEqual(10, payload.Data.Persistence.PersistedCount);

        var created = await DbContext.FEQuestions.Find(item =>
                item.CreatedBy == TestIdentity.StudentA.UserId &&
                item.OwnerUserId == TestIdentity.StudentA.UserId &&
                item.Source == "Student")
            .ToListAsync();
        Assert.IsTrue(created.Count >= 10);
    }

    [TestMethod]
    public async Task API_AIQG_004_PublishGeneratedQuestion_ByOwner_MakesItActiveAndReadable()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        var generatedQuestionId = await GenerateOwnedDraftQuestionAsync(client, scenario);

        using var publishResponse = await client.PutAsync($"/api/ai/questions/generated/{generatedQuestionId}/publish", content: null);
        using var detailResponse = await client.GetAsync($"/api/student/question-bank/{generatedQuestionId}");

        HttpResponseAssertions.AssertStatusCode(publishResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        var stored = await DbContext.FEQuestions.Find(item => item.Id == generatedQuestionId).FirstOrDefaultAsync();
        Assert.IsNotNull(stored);
        Assert.AreEqual("Active", stored.Status);
        Assert.AreEqual(TestIdentity.StudentA.UserId, stored.OwnerUserId);
    }

    [TestMethod]
    public async Task API_AIQG_005_DeleteGeneratedDraft_ByOwner_RemovesQuestion()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        var generatedQuestionId = await GenerateOwnedDraftQuestionAsync(client, scenario);

        using var deleteResponse = await client.DeleteAsync($"/api/ai/questions/generated/{generatedQuestionId}");
        using var detailResponse = await client.GetAsync($"/api/student/question-bank/{generatedQuestionId}");

        HttpResponseAssertions.AssertStatusCode(deleteResponse, HttpStatusCode.OK);
        Assert.AreEqual(HttpStatusCode.NotFound, detailResponse.StatusCode);

        var stored = await DbContext.FEQuestions.Find(item => item.Id == generatedQuestionId).FirstOrDefaultAsync();
        Assert.IsNull(stored);
    }

    private static GenerationReviewRequestDto BuildAdminGenerationRequest(Batch2KnowledgeScenario scenario)
    {
        return new GenerationReviewRequestDto
        {
            UserId = TestIdentity.Admin.UserId,
            CourseId = scenario.Course.Id,
            DocumentId = scenario.SystemDocument.Id,
            Subject = "PRN212",
            Difficulty = "Easy",
            QuestionType = "FE",
            Count = 10,
            PersistQuestions = true,
            IsPublic = false,
            Retrieval = new RetrievalPlanRequestDto
            {
                CourseId = scenario.Course.Id,
                DocumentId = scenario.SystemDocument.Id,
                Subject = "PRN212",
                QuestionType = "FE",
                Difficulty = "Easy",
                RequestedQuestionCount = 10,
                GenerationMode = "Single",
                TargetTopics = ["queues"],
                SourceScope = "SYSTEM",
                ChapterKeys = ["chapter-1"]
            }
        };
    }

    private static async Task<string> GenerateOwnedDraftQuestionAsync(HttpClient client, Batch2KnowledgeScenario scenario)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/ai/questions/generate-review/byos",
            new ByosQuestionGenerationRequestDto
            {
                DocumentId = scenario.StudentADocument.Id,
                Subject = "PRN212",
                ChapterKeys = ["chapter-1"],
                Difficulty = "Easy",
                QuestionType = "FE",
                Count = 10,
                PersistQuestions = true,
                IsPublic = false,
                TargetTopics = ["queues"]
            });

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<GenerationReviewResultDto>>(response);
        Assert.IsNotNull(payload.Data);
        Assert.IsTrue(payload.Data.Persistence.PersistedQuestionIds.Count > 0);
        return payload.Data.Persistence.PersistedQuestionIds.First();
    }
}
