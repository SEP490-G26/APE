using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Application.Common;
using Application.DTOs;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using Domain.Enums;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class DocumentRetrievalIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task API_DOC_001_AdminUploadSystemDocument_ThenList_PersistsDocumentDraftAndChunks()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.Admin);

        using var uploadResponse = await UploadDocumentAsync(
            client,
            "/api/admin/documents/upload",
            scenario.Course.Id,
            "admin-upload.txt",
            "System queue fundamentals for integration upload.");

        HttpResponseAssertions.AssertStatusCode(uploadResponse, HttpStatusCode.OK);
        var uploadPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<DocumentDto>>(uploadResponse);
        Assert.IsNotNull(uploadPayload.Data);
        Assert.AreEqual("SystemSyllabus", uploadPayload.Data.Source);
        Assert.IsTrue(uploadPayload.Data.HasExtractionDraft);
        Assert.IsTrue(uploadPayload.Data.HasEmbeddedChunks);

        using var listResponse = await client.GetAsync($"/api/admin/documents?page=1&limit=20&courseId={scenario.Course.Id}");
        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PaginatedResult<DocumentListItemDto>>>(listResponse);
        Assert.IsNotNull(listPayload.Data);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), uploadPayload.Data.Id);

        var storedDocument = await DbContext.Documents.Find(item => item.Id == uploadPayload.Data.Id).FirstOrDefaultAsync();
        var storedDraft = await DbContext.AIExtractionDrafts.Find(item => item.DocumentId == uploadPayload.Data.Id).FirstOrDefaultAsync();
        var storedChunks = await DbContext.KnowledgeChunks.Find(item => item.DocumentId == uploadPayload.Data.Id).ToListAsync();

        Assert.IsNotNull(storedDocument);
        Assert.IsTrue(storedDocument.IsActive);
        Assert.AreEqual(DocumentStatus.Completed, storedDocument.Status);
        Assert.IsNotNull(storedDraft);
        Assert.AreEqual(1, storedChunks.Count);
    }

    [TestMethod]
    public async Task API_DOC_002_AdminPreviewAndDownload_ReturnStorageBackedContent()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        PrimeStoredFile(scenario.SystemDocument.FilePath, "system queues");
        using var client = CreateClient(TestIdentity.Admin);

        using var previewResponse = await client.GetAsync($"/api/admin/documents/{scenario.SystemDocument.Id}/preview");
        using var downloadResponse = await client.GetAsync($"/api/admin/documents/{scenario.SystemDocument.Id}/download");

        HttpResponseAssertions.AssertStatusCode(previewResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(downloadResponse, HttpStatusCode.OK);

        var previewPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<DocumentPreviewResultDto>>(previewResponse);
        Assert.IsNotNull(previewPayload.Data);
        Assert.AreEqual(scenario.SystemDocument.Id, previewPayload.Data.DocumentId);
        StringAssert.Contains(previewPayload.Data.PreviewText, "Queue");

        Assert.IsNotNull(downloadResponse.Content.Headers.ContentDisposition);
        Assert.AreEqual("system-queues.txt", downloadResponse.Content.Headers.ContentDisposition!.FileNameStar ?? downloadResponse.Content.Headers.ContentDisposition.FileName?.Trim('"'));
    }

    [TestMethod]
    public async Task API_DOC_003_AdminEditAndGetDraft_UpdatesChunkContent()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.Admin);

        using var editResponse = await client.PutAsJsonAsync(
            $"/api/admin/documents/{scenario.SystemDocument.Id}/edit",
            new EditContentDto
            {
                Content = "Edited controlled content for admin document."
            });
        using var draftResponse = await client.GetAsync($"/api/admin/documents/{scenario.SystemDocument.Id}/extraction-draft");

        HttpResponseAssertions.AssertStatusCode(editResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(draftResponse, HttpStatusCode.OK);

        var draftPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ExtractionDraftDto>>(draftResponse);
        Assert.IsNotNull(draftPayload.Data);
        Assert.AreEqual(scenario.SystemDraft.Id, draftPayload.Data.DraftId);

        var updatedChunk = await DbContext.KnowledgeChunks.Find(item => item.DocumentId == scenario.SystemDocument.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(updatedChunk);
        Assert.AreEqual("Edited controlled content for admin document.", updatedChunk.RawText);
    }

    [TestMethod]
    public async Task API_DOC_004_AdminToggleAndDelete_MarksSystemDocumentDeletedAndRemovesArtifacts()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.Admin);

        using var toggleResponse = await client.PutAsync($"/api/admin/documents/{scenario.SystemDocument.Id}/toggle-active", content: null);
        using var deleteResponse = await client.DeleteAsync($"/api/admin/documents/{scenario.SystemDocument.Id}");

        HttpResponseAssertions.AssertStatusCode(toggleResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(deleteResponse, HttpStatusCode.OK);

        var storedDocument = await DbContext.Documents.Find(item => item.Id == scenario.SystemDocument.Id).FirstOrDefaultAsync();
        var chunkCount = await DbContext.KnowledgeChunks.CountDocumentsAsync(item => item.DocumentId == scenario.SystemDocument.Id);
        var draftCount = await DbContext.AIExtractionDrafts.CountDocumentsAsync(item => item.DocumentId == scenario.SystemDocument.Id);

        Assert.IsNotNull(storedDocument);
        Assert.AreEqual(DocumentStatus.Deleted, storedDocument.Status);
        Assert.IsFalse(storedDocument.IsActive);
        Assert.AreEqual(0, chunkCount);
        Assert.AreEqual(0, draftCount);
    }

    [TestMethod]
    public async Task API_DOC_005_StudentByosUpload_ThenList_PersistsOwnedDocumentAndBilling()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var uploadResponse = await UploadDocumentAsync(
            client,
            "/api/student/documents/byos",
            scenario.Course.Id,
            "student-a-upload.txt",
            "Student A BYOS queue notes for upload.");

        HttpResponseAssertions.AssertStatusCode(uploadResponse, HttpStatusCode.OK);
        var uploadPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<DocumentDto>>(uploadResponse);
        Assert.IsNotNull(uploadPayload.Data);
        Assert.AreEqual("BYOS", uploadPayload.Data.Source);
        Assert.IsTrue(uploadPayload.Data.ActualDeductedVnd >= 0);

        using var listResponse = await client.GetAsync("/api/student/documents?page=1&limit=20");
        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PaginatedResult<DocumentListItemDto>>>(listResponse);
        Assert.IsNotNull(listPayload.Data);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), uploadPayload.Data.Id);

        var storedDocument = await DbContext.Documents.Find(item => item.Id == uploadPayload.Data.Id).FirstOrDefaultAsync();
        var storedBilling = await DbContext.AIVndBillingTransactions.Find(item => item.SourceEntityId == uploadPayload.Data.Id).FirstOrDefaultAsync();

        Assert.IsNotNull(storedDocument);
        Assert.AreEqual(TestIdentity.StudentA.UserId, storedDocument.UserId);
        Assert.IsNotNull(storedBilling);
        Assert.AreEqual("Document", storedBilling.SourceEntityType);
    }

    [TestMethod]
    public async Task API_DOC_006_StudentPreviewAndDownload_EnforceOwnership()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        PrimeStoredFile(scenario.StudentADocument.FilePath, "student a byos");
        using var ownerClient = CreateClient(TestIdentity.StudentA);
        using var otherClient = CreateClient(TestIdentity.StudentB);

        using var ownerPreviewResponse = await ownerClient.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/preview");
        using var ownerDownloadResponse = await ownerClient.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/download");
        using var otherPreviewResponse = await otherClient.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/preview");
        using var otherDownloadResponse = await otherClient.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/download");

        HttpResponseAssertions.AssertStatusCode(ownerPreviewResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(ownerDownloadResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(otherPreviewResponse, HttpStatusCode.Unauthorized);
        HttpResponseAssertions.AssertStatusCode(otherDownloadResponse, HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task API_DOC_007_StudentRetrievalMetadataEndpoints_ReturnOwnedDraftTopicsAndChapters()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var draftResponse = await client.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/extraction-draft");
        using var topicsResponse = await client.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/topics");
        using var chaptersResponse = await client.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/chapters");
        using var chapterTopicsResponse = await client.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/chapters/chapter-1/topics");
        using var multiChapterTopicsResponse = await client.GetAsync($"/api/student/documents/{scenario.StudentADocument.Id}/chapter-topics?chapterKeys=chapter-1");

        HttpResponseAssertions.AssertStatusCode(draftResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(topicsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(chaptersResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(chapterTopicsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(multiChapterTopicsResponse, HttpStatusCode.OK);

        var topicsPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<TopicSummaryResultDto>>(topicsResponse);
        var chaptersPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ChapterSummaryResultDto>>(chaptersResponse);

        Assert.IsNotNull(topicsPayload.Data);
        Assert.IsTrue(topicsPayload.Data.Topics.Any(item => item.Tag == "queues"));
        Assert.IsNotNull(chaptersPayload.Data);
        Assert.AreEqual(1, chaptersPayload.Data.DistinctChapterCount);
    }

    [TestMethod]
    public async Task API_DOC_008_StudentDelete_OnlyOwnerCanDeleteByosDocument()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var ownerClient = CreateClient(TestIdentity.StudentA);
        using var otherClient = CreateClient(TestIdentity.StudentB);

        using var otherDeleteResponse = await otherClient.DeleteAsync($"/api/student/documents/{scenario.StudentADocument.Id}");
        using var ownerDeleteResponse = await ownerClient.DeleteAsync($"/api/student/documents/{scenario.StudentADocument.Id}");
        using var listResponse = await ownerClient.GetAsync("/api/student/documents?page=1&limit=20");

        HttpResponseAssertions.AssertStatusCode(otherDeleteResponse, HttpStatusCode.Unauthorized);
        HttpResponseAssertions.AssertStatusCode(ownerDeleteResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);

        var storedDocument = await DbContext.Documents.Find(item => item.Id == scenario.StudentADocument.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(storedDocument);
        Assert.AreEqual(DocumentStatus.Deleted, storedDocument.Status);
    }

    [TestMethod]
    public async Task API_DOC_009_SystemRetrievalBrowseAndPlan_CreatePersistedContextPack()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var topicsResponse = await client.GetAsync($"/api/ai/courses/{scenario.Course.Id}/topics");
        using var documentsResponse = await client.GetAsync($"/api/ai/courses/{scenario.Course.Id}/documents");
        using var chaptersResponse = await client.GetAsync($"/api/ai/courses/{scenario.Course.Id}/documents/{scenario.SystemDocument.Id}/chapters");
        using var chapterTopicsResponse = await client.GetAsync($"/api/ai/courses/{scenario.Course.Id}/documents/{scenario.SystemDocument.Id}/chapters/chapter-1/topics");
        using var multiTopicsResponse = await client.GetAsync($"/api/ai/courses/{scenario.Course.Id}/documents/{scenario.SystemDocument.Id}/chapter-topics?chapterKeys=chapter-1");
        using var planResponse = await client.PostAsJsonAsync(
            "/api/ai/retrieval/plan",
            new RetrievalPlanRequestDto
            {
                CourseId = scenario.Course.Id,
                DocumentId = scenario.SystemDocument.Id,
                Subject = "PRN212",
                QuestionType = "FE",
                Difficulty = "Easy",
                GenerationMode = "Single",
                TargetTopics = ["queues"],
                SourceScope = "SYSTEM",
                ChapterKeys = ["chapter-1"],
                RetrievalQuery = "queue basics",
                RequestedQuestionCount = 1
            });

        HttpResponseAssertions.AssertStatusCode(topicsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(documentsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(chaptersResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(chapterTopicsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(multiTopicsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(planResponse, HttpStatusCode.OK);

        var planPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<RetrievalPlanResultDto>>(planResponse);
        Assert.IsNotNull(planPayload.Data);
        Assert.IsFalse(string.IsNullOrWhiteSpace(planPayload.Data.PackId));

        var storedPack = await DbContext.AIContextPacks.Find(item => item.Id == planPayload.Data.PackId).FirstOrDefaultAsync();
        Assert.IsNotNull(storedPack);
        Assert.AreEqual("SYSTEM", storedPack.SourceScope);
    }

    [TestMethod]
    public async Task API_DOC_010_ContextPackListDetailAndMarkStale_WorkAgainstPersistedPack()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var planResponse = await client.PostAsJsonAsync(
            "/api/ai/retrieval/plan",
            new RetrievalPlanRequestDto
            {
                CourseId = scenario.Course.Id,
                DocumentId = scenario.SystemDocument.Id,
                Subject = "PRN212",
                QuestionType = "FE",
                Difficulty = "Easy",
                GenerationMode = "Single",
                TargetTopics = ["queues"],
                SourceScope = "SYSTEM",
                ChapterKeys = ["chapter-1"],
                RequestedQuestionCount = 1
            });
        var planPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<RetrievalPlanResultDto>>(planResponse);
        Assert.IsNotNull(planPayload.Data);

        using var listResponse = await client.GetAsync("/api/ai/context-packs?take=20");
        using var detailResponse = await client.GetAsync($"/api/ai/context-packs/{planPayload.Data.PackId}");
        using var staleResponse = await client.PutAsync($"/api/ai/context-packs/{planPayload.Data.PackId}/stale", content: null);

        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(staleResponse, HttpStatusCode.OK);

        var stalePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<ContextPackDto>>(staleResponse);
        Assert.IsNotNull(stalePayload.Data);
        Assert.AreEqual("stale", stalePayload.Data.PackStatus, ignoreCase: true);
    }

    [TestMethod]
    public async Task API_DOC_011_InvalidUploads_AreRejectedWithoutDocumentPersistence()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var studentClient = CreateClient(TestIdentity.StudentA);
        using var adminClient = CreateClient(TestIdentity.Admin);

        using var studentResponse = await UploadDocumentAsync(
            studentClient,
            "/api/student/documents/byos",
            scenario.Course.Id,
            "bad-upload.exe",
            "invalid");
        using var adminResponse = await UploadDocumentAsync(
            adminClient,
            "/api/admin/documents/upload",
            scenario.Course.Id,
            "bad-upload.exe",
            "invalid");

        HttpResponseAssertions.AssertStatusCode(studentResponse, HttpStatusCode.BadRequest);
        HttpResponseAssertions.AssertStatusCode(adminResponse, HttpStatusCode.BadRequest);

        var allDocuments = await DbContext.Documents.Find(FilterDefinition<Document>.Empty).ToListAsync();
        Assert.AreEqual(3, allDocuments.Count);
    }

    [TestMethod]
    public async Task API_DOC_012_ByosDuplicateReuse_AndDocumentCapBoundary_FollowCurrentRules()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var firstUploadResponse = await UploadDocumentAsync(
            client,
            "/api/student/documents/byos",
            scenario.Course.Id,
            "duplicate-a.txt",
            "Duplicate BYOS queue content.");
        var firstUploadPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<DocumentDto>>(firstUploadResponse);
        Assert.IsNotNull(firstUploadPayload.Data);

        using var secondUploadResponse = await UploadDocumentAsync(
            client,
            "/api/student/documents/byos",
            scenario.Course.Id,
            "duplicate-b.txt",
            "Duplicate BYOS queue content.");

        HttpResponseAssertions.AssertStatusCode(secondUploadResponse, HttpStatusCode.OK);
        var secondUploadPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<DocumentDto>>(secondUploadResponse);
        Assert.IsNotNull(secondUploadPayload.Data);
        Assert.IsTrue(secondUploadPayload.Data.DuplicateDetected);
        Assert.AreEqual(firstUploadPayload.Data.Id, secondUploadPayload.Data.DuplicateOfDocumentId);

        var extraDocuments = Enumerable.Range(0, 8)
            .Select(index => new Document
            {
                Id = $"64b0000000000000000002{index:D2}",
                CourseId = scenario.Course.Id,
                UserId = TestIdentity.StudentA.UserId,
                FileName = $"cap-{index}.txt",
                FilePath = $"integration-storage/cap-{index}.txt",
                FileType = ".txt",
                Source = "BYOS",
                Status = DocumentStatus.Completed,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-index - 1)
            })
            .ToList();
        await DbContext.Documents.InsertManyAsync(extraDocuments);

        using var limitResponse = await UploadDocumentAsync(
            client,
            "/api/student/documents/byos",
            scenario.Course.Id,
            "limit-hit.txt",
            "This upload should hit the BYOS cap.");

        HttpResponseAssertions.AssertStatusCode(limitResponse, HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> UploadDocumentAsync(
        HttpClient client,
        string route,
        string courseId,
        string fileName,
        string content)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(courseId), "CourseId");

        var bytes = Encoding.UTF8.GetBytes(content);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        form.Add(fileContent, "file", fileName);

        return await client.PostAsync(route, form);
    }

    private static void PrimeStoredFile(string path, string content)
    {
        IntegrationTestRun.Factory.FileStorage.Register(path, Encoding.UTF8.GetBytes(content));
    }
}
