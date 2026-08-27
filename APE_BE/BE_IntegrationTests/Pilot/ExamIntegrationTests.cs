using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class ExamIntegrationTests
    : IntegrationTestBase
{
    [TestMethod]
    public async Task API_EXAM_001_AdminPracticeSetup_BrowseQuestionPoolAndDetail_ReturnExpectedData()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.Admin);

        using var listResponse = await client.GetAsync($"/api/admin/practice-setup?courseId={scenario.Course.Id}&page=1&limit=20");
        using var poolResponse = await client.GetAsync($"/api/admin/practice-setup/question-pool?courseId={scenario.Course.Id}&examType=FE");
        using var detailResponse = await client.GetAsync($"/api/admin/practice-setup/{scenario.AdminPracticeSetupExam.Id}");

        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(poolResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PagedEnvelope<ExamDto>>>(listResponse);
        var poolPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamConfigQuestionPoolDto>>(poolResponse);
        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamDetailDto>>(detailResponse);

        Assert.IsTrue(listPayload.Success);
        Assert.IsNotNull(listPayload.Data);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.AdminPracticeSetupExam.Id);

        Assert.IsTrue(poolPayload.Success);
        Assert.IsNotNull(poolPayload.Data);
        Assert.AreEqual(2, poolPayload.Data.AvailableCount);
        CollectionAssert.AreEquivalent(
            new[] { scenario.SystemFeEasy.Id, scenario.SystemFeMedium.Id },
            poolPayload.Data.Items.Select(item => item.Id).ToArray());

        Assert.IsTrue(detailPayload.Success);
        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual(scenario.AdminPracticeSetupExam.Id, detailPayload.Data.Id);
        Assert.AreEqual(2, detailPayload.Data.FEQuestionCount);
    }

    [TestMethod]
    public async Task API_EXAM_002_AdminPracticeSetup_CreateAndUpdate_PersistsConfiguration()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.Admin);

        var createRequest = new SaveAdminPracticeSetupRequestDto
        {
            Title = "Created Practice Setup",
            CourseId = scenario.Course.Id,
            ExamType = "FE",
            Mode = "Practice",
            FeDifficultyCounts =
            [
                new DifficultyCountDto { Difficulty = "Easy", Count = 1 },
                new DifficultyCountDto { Difficulty = "Medium", Count = 1 }
            ]
        };

        using var createResponse = await client.PostAsJsonAsync("/api/admin/practice-setup", createRequest);
        HttpResponseAssertions.AssertStatusCode(createResponse, HttpStatusCode.OK);

        var createdPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamDetailDto>>(createResponse);
        Assert.IsTrue(createdPayload.Success);
        Assert.IsNotNull(createdPayload.Data);
        Assert.AreEqual("Created Practice Setup", createdPayload.Data.Title);
        Assert.AreEqual(2, createdPayload.Data.FEQuestionCount);

        var updateRequest = new SaveAdminPracticeSetupRequestDto
        {
            Title = "Updated Practice Setup",
            CourseId = scenario.Course.Id,
            ExamType = "FE",
            Mode = "Practice",
            Difficulties = ["Easy"],
            FeDifficultyCounts =
            [
                new DifficultyCountDto { Difficulty = "Easy", Count = 1 }
            ]
        };

        using var updateResponse = await client.PutAsJsonAsync($"/api/admin/practice-setup/{createdPayload.Data.Id}", updateRequest);
        using var detailResponse = await client.GetAsync($"/api/admin/practice-setup/{createdPayload.Data.Id}");

        HttpResponseAssertions.AssertStatusCode(updateResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        var updatedPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamDetailDto>>(updateResponse);
        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamDetailDto>>(detailResponse);

        Assert.IsTrue(updatedPayload.Success);
        Assert.IsNotNull(updatedPayload.Data);
        Assert.AreEqual("Updated Practice Setup", updatedPayload.Data.Title);
        Assert.AreEqual(1, updatedPayload.Data.FEQuestionCount);
        Assert.AreEqual(scenario.SystemFeEasy.Id, updatedPayload.Data.FEQuestions.Single().Id);

        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual("Updated Practice Setup", detailPayload.Data.Title);
        Assert.AreEqual(1, detailPayload.Data.FEQuestionCount);

        var storedExam = await DbContext.Exams
            .Find(item => item.Id == createdPayload.Data.Id)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedExam);
        Assert.AreEqual(TestIdentity.Admin.UserId, storedExam.CreatedBy);
        CollectionAssert.AreEquivalent(new[] { scenario.SystemFeEasy.Id }, storedExam.FeExamQuestions);
    }

    [TestMethod]
    public async Task API_EXAM_004_StudentExamDiscovery_SeparatesPublicAndOwnedInventory()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var listResponse = await client.GetAsync($"/api/student/exams?courseId={scenario.Course.Id}&page=1&limit=20");
        using var detailResponse = await client.GetAsync($"/api/student/exams/{scenario.PrivateOwnedExam.Id}");
        using var libraryResponse = await client.GetAsync("/api/student/exams/library?page=1&limit=20");
        using var systemLibraryResponse = await client.GetAsync("/api/student/exams/system-library?page=1&limit=20");

        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(libraryResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(systemLibraryResponse, HttpStatusCode.OK);

        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PagedEnvelope<ExamDto>>>(listResponse);
        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamDetailDto>>(detailResponse);
        var libraryPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PagedEnvelope<ExamDto>>>(libraryResponse);
        var systemLibraryPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PagedEnvelope<ExamDto>>>(systemLibraryResponse);

        Assert.IsNotNull(listPayload.Data);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.PublicExam.Id);
        CollectionAssert.DoesNotContain(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.PrivateOwnedExam.Id);

        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual(scenario.PrivateOwnedExam.Id, detailPayload.Data.Id);
        Assert.AreEqual(1, detailPayload.Data.FEQuestionCount);

        Assert.IsNotNull(libraryPayload.Data);
        CollectionAssert.Contains(libraryPayload.Data.Items.Select(item => item.Id).ToList(), scenario.PrivateOwnedExam.Id);

        Assert.IsNotNull(systemLibraryPayload.Data);
        CollectionAssert.Contains(systemLibraryPayload.Data.Items.Select(item => item.Id).ToList(), scenario.PublicExam.Id);
        CollectionAssert.Contains(systemLibraryPayload.Data.Items.Select(item => item.Id).ToList(), scenario.AdminPracticeSetupExam.Id);
        CollectionAssert.DoesNotContain(systemLibraryPayload.Data.Items.Select(item => item.Id).ToList(), scenario.PrivateOwnedExam.Id);
    }

    [TestMethod]
    public async Task API_EXAM_005_StudentExamConfigDocuments_FiltersBySourceRules()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var systemResponse = await client.GetAsync($"/api/student/exam-config/courses/{scenario.Course.Id}/documents?source=System");
        using var byosResponse = await client.GetAsync($"/api/student/exam-config/courses/{scenario.Course.Id}/documents?source=BYOS");

        HttpResponseAssertions.AssertStatusCode(systemResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(byosResponse, HttpStatusCode.OK);

        var systemPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<ExamConfigDocumentDto>>>(systemResponse);
        var byosPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<ExamConfigDocumentDto>>>(byosResponse);

        Assert.IsNotNull(systemPayload.Data);
        Assert.AreEqual(0, systemPayload.Data.Count);

        Assert.IsNotNull(byosPayload.Data);
        Assert.AreEqual(1, byosPayload.Data.Count);
        Assert.AreEqual(scenario.ByosDocument.Id, byosPayload.Data.Single().Id);
        Assert.AreEqual("BYOS", byosPayload.Data.Single().Source);
        Assert.AreEqual(1, byosPayload.Data.Single().ChunkCount);
    }

    [TestMethod]
    public async Task API_EXAM_006_StudentExamConfigQuestionPool_ReturnsOnlyEligibleOwnedByosQuestions()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var response = await client.GetAsync(
            $"/api/student/exam-config/question-pool?courseId={scenario.Course.Id}&examType=FE&source=BYOS&documentIds={scenario.ByosDocument.Id}");

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamConfigQuestionPoolDto>>(response);
        Assert.IsTrue(payload.Success);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual("BYOS", payload.Data.Source);
        Assert.AreEqual("FE", payload.Data.ExamType);
        Assert.AreEqual(1, payload.Data.AvailableCount);
        CollectionAssert.AreEquivalent(new[] { scenario.ByosDocument.Id }, payload.Data.DocumentIds);
        Assert.AreEqual(scenario.PrivateByosFe.Id, payload.Data.Items.Single().Id);

        var examCount = await DbContext.Exams.CountDocumentsAsync(FilterDefinition<Exam>.Empty);
        Assert.AreEqual(4, examCount);
    }

    [TestMethod]
    public async Task API_EXAM_007_StartConfiguredExam_CreatesPrivateExamAndPracticeSession()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedExamCatalogScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        var request = new StartConfiguredExamRequestDto
        {
            CourseId = scenario.Course.Id,
            Source = "BYOS",
            DocumentIds = [scenario.ByosDocument.Id],
            ExamType = "FE",
            Mode = "Practice",
            TopicTags = ["BYOS"],
            Difficulties = ["Easy"],
            FeDifficultyCounts =
            [
                new DifficultyCountDto
                {
                    Difficulty = "Easy",
                    Count = 1
                }
            ]
        };

        using var startResponse = await client.PostAsJsonAsync("/api/student/exam-config/start", request);
        HttpResponseAssertions.AssertStatusCode(startResponse, HttpStatusCode.OK);

        var startPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StartConfiguredExamResultDto>>(startResponse);
        Assert.IsTrue(startPayload.Success);
        Assert.IsNotNull(startPayload.Data);
        Assert.IsNotNull(startPayload.Data.Exam);
        Assert.AreEqual("FE", startPayload.Data.ExamType);
        Assert.AreEqual(1, startPayload.Data.QuestionCount);
        Assert.AreEqual("Private", startPayload.Data.Exam.Visibility);

        using var sessionResponse = await client.GetAsync($"/api/student/practice/sessions/{startPayload.Data.SessionId}");
        using var examResponse = await client.GetAsync($"/api/student/exams/{startPayload.Data.ExamId}");

        HttpResponseAssertions.AssertStatusCode(sessionResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(examResponse, HttpStatusCode.OK);

        var sessionPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PracticeSessionDto>>(sessionResponse);
        var examPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExamDetailDto>>(examResponse);

        Assert.IsNotNull(sessionPayload.Data);
        Assert.AreEqual(TestIdentity.StudentA.UserId, sessionPayload.Data.StudentId);
        Assert.AreEqual(startPayload.Data.ExamId, sessionPayload.Data.ExamId);

        Assert.IsNotNull(examPayload.Data);
        Assert.AreEqual(startPayload.Data.ExamId, examPayload.Data.Id);
        Assert.AreEqual(1, examPayload.Data.FEQuestionCount);

        var storedExam = await DbContext.Exams
            .Find(item => item.Id == startPayload.Data.ExamId)
            .FirstOrDefaultAsync();
        var storedSession = await DbContext.PracticeSessions
            .Find(item => item.Id == startPayload.Data.SessionId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedExam);
        Assert.AreEqual(TestIdentity.StudentA.UserId, storedExam.CreatedBy);
        Assert.AreEqual("Private", storedExam.Visibility.ToString());

        Assert.IsNotNull(storedSession);
        Assert.AreEqual(TestIdentity.StudentA.UserId, storedSession.StudentId);
        Assert.AreEqual(startPayload.Data.ExamId, storedSession.ExamId);
    }

    private sealed class PagedEnvelope<T>
    {
        public List<T> Items { get; set; } = new();
        public long Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public long TotalPages { get; set; }
    }
}
