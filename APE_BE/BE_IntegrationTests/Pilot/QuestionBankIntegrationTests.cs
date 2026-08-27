using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using BE_IntegrationTests.Infrastructure;
using ClosedXML.Excel;
using Domain.Entities;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class QuestionBankIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task API_QUESTION_001_AdminQuestions_ListAndDetail_ReturnAdminOwnedFeAndPe()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.Admin);

        using var listResponse = await client.GetAsync("/api/admin/questions?page=1&limit=20");
        using var feDetailResponse = await client.GetAsync($"/api/admin/questions/{scenario.AdminFeQuestion.Id}");
        using var peDetailResponse = await client.GetAsync($"/api/admin/questions/{scenario.AdminPeQuestion.Id}");

        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(feDetailResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(peDetailResponse, HttpStatusCode.OK);

        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<QuestionListEnvelope>>(listResponse);
        Assert.IsNotNull(listPayload.Data);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.AdminFeQuestion.Id);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.AdminPeQuestion.Id);
    }

    [TestMethod]
    public async Task API_QUESTION_002_AdminQuestionLifecycle_EditPublishRejectDisableDelete_PersistsTransitions()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.Admin);

        using var editResponse = await client.PutAsJsonAsync(
            $"/api/admin/questions/{scenario.AdminFeQuestion.Id}",
            new EditQuestionDto
            {
                Title = "Edited Admin FE",
                Description = "Edited description",
                Options = ["A", "B", "C", "D"],
                CorrectAnswer = ["B"],
                Difficulty = "Medium",
                TopicTags = ["queues", "edited"]
            });
        using var publishResponse = await client.PutAsync($"/api/admin/questions/{scenario.AdminFeQuestion.Id}/publish", content: null);
        using var rejectResponse = await client.PutAsJsonAsync(
            $"/api/admin/questions/{scenario.AdminFeQuestion.Id}/reject",
            new PublishQuestionDto { Reason = "Rejected in integration test" });
        using var disableResponse = await client.PutAsJsonAsync(
            $"/api/admin/questions/{scenario.AdminPeQuestion.Id}/disable",
            new DisableQuestionDto { Reason = "Temporarily disabled" });
        using var deleteResponse = await client.DeleteAsync($"/api/admin/questions/{scenario.AdminDraftFeQuestion.Id}");

        HttpResponseAssertions.AssertStatusCode(editResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(publishResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(rejectResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(disableResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(deleteResponse, HttpStatusCode.OK);

        var storedFe = await DbContext.FEQuestions.Find(item => item.Id == scenario.AdminFeQuestion.Id).FirstOrDefaultAsync();
        var storedPe = await DbContext.PEQuestions.Find(item => item.Id == scenario.AdminPeQuestion.Id).FirstOrDefaultAsync();
        var deletedDraft = await DbContext.FEQuestions.Find(item => item.Id == scenario.AdminDraftFeQuestion.Id).FirstOrDefaultAsync();

        Assert.IsNotNull(storedFe);
        Assert.AreEqual("Disabled", storedFe.Status);
        Assert.AreEqual("Rejected in integration test", storedFe.DisabledReason);
        Assert.IsNotNull(storedPe);
        Assert.AreEqual("Disabled", storedPe.Status);
        Assert.IsNull(deletedDraft);
    }

    [TestMethod]
    public async Task API_QUESTION_003_ModernImport_CreatesQueryableAdminQuestion()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.Admin);

        using var response = await ImportQuestionWorkbookAsync(client, "/api/admin/questions/import", scenario.Course.Code, "Imported Modern FE");

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);

        var imported = await DbContext.FEQuestions.Find(item => item.Title == "Imported Modern FE").FirstOrDefaultAsync();
        Assert.IsNotNull(imported);
        Assert.AreEqual("Admin", imported.Source);
    }

    [TestMethod]
    public async Task API_QUESTION_006_StudentQuestionBank_ListTopicTagsAndDetail_ReturnOwnedQuestionsOnly()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var listResponse = await client.GetAsync("/api/student/question-bank?page=1&limit=20");
        using var tagsResponse = await client.GetAsync("/api/student/question-bank/topic-tags");
        using var detailResponse = await client.GetAsync($"/api/student/question-bank/{scenario.StudentAFeQuestion.Id}");

        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(tagsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StudentQuestionBankListEnvelope>>(listResponse);
        var tagsPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<string>>>(tagsResponse);
        Assert.IsNotNull(listPayload.Data);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.StudentAFeQuestion.Id);
        Assert.IsNotNull(tagsPayload.Data);
        CollectionAssert.Contains(tagsPayload.Data, "queues");
    }

    [TestMethod]
    public async Task API_QUESTION_007_StudentPublishOwnedDraft_ChangesStatusToActive()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var publishResponse = await client.PutAsync($"/api/student/question-bank/{scenario.StudentADraftQuestion.Id}/publish", content: null);
        using var detailResponse = await client.GetAsync($"/api/student/question-bank/{scenario.StudentADraftQuestion.Id}");

        HttpResponseAssertions.AssertStatusCode(publishResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        var stored = await DbContext.FEQuestions.Find(item => item.Id == scenario.StudentADraftQuestion.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(stored);
        Assert.AreEqual("Active", stored.Status);
        Assert.IsFalse(stored.IsPublic);
    }

    [TestMethod]
    public async Task API_QUESTION_008_StudentDeleteOwnedQuestion_MarksItDeleted()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentA);

        using var deleteResponse = await client.DeleteAsync($"/api/student/question-bank/{scenario.StudentAFeQuestion.Id}");
        using var detailResponse = await client.GetAsync($"/api/student/question-bank/{scenario.StudentAFeQuestion.Id}");

        HttpResponseAssertions.AssertStatusCode(deleteResponse, HttpStatusCode.OK);
        Assert.AreEqual(HttpStatusCode.NotFound, detailResponse.StatusCode);

        var stored = await DbContext.FEQuestions.Find(item => item.Id == scenario.StudentAFeQuestion.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(stored);
        Assert.AreEqual("Deleted", stored.Status);
    }

    [TestMethod]
    public async Task API_QUESTION_009_StudentCannotManageOtherUsersQuestion()
    {
        var scenario = await CreateSeedFactory().SeedBatch2KnowledgeScenarioAsync();
        using var client = CreateClient(TestIdentity.StudentB);

        using var detailResponse = await client.GetAsync($"/api/student/question-bank/{scenario.StudentADraftQuestion.Id}");
        using var publishResponse = await client.PutAsync($"/api/student/question-bank/{scenario.StudentADraftQuestion.Id}/publish", content: null);
        using var deleteResponse = await client.DeleteAsync($"/api/student/question-bank/{scenario.StudentADraftQuestion.Id}");

        Assert.AreEqual(HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, publishResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var stored = await DbContext.FEQuestions.Find(item => item.Id == scenario.StudentADraftQuestion.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(stored);
        Assert.AreEqual("Draft", stored.Status);
        Assert.AreEqual(TestIdentity.StudentA.UserId, stored.OwnerUserId);
    }

    private static async Task<HttpResponseMessage> ImportQuestionWorkbookAsync(
        HttpClient client,
        string route,
        string courseCode,
        string title)
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Questions");
            worksheet.Cell(1, 1).Value = "Title";
            worksheet.Cell(1, 2).Value = "Description";
            worksheet.Cell(1, 3).Value = "Options";
            worksheet.Cell(1, 4).Value = "CorrectAnswer";
            worksheet.Cell(1, 5).Value = "Explanation";
            worksheet.Cell(1, 6).Value = "Difficulty";
            worksheet.Cell(1, 7).Value = "TopicTags";
            worksheet.Cell(1, 8).Value = "CourseCode";

            worksheet.Cell(2, 1).Value = title;
            worksheet.Cell(2, 2).Value = "Imported description";
            worksheet.Cell(2, 3).Value = "[\"A\",\"B\",\"C\",\"D\"]";
            worksheet.Cell(2, 4).Value = "[\"A\"]";
            worksheet.Cell(2, 5).Value = "Imported explanation";
            worksheet.Cell(2, 6).Value = "Easy";
            worksheet.Cell(2, 7).Value = "queues,stacks";
            worksheet.Cell(2, 8).Value = courseCode;

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(fileContent, "file", "questions.xlsx");
        return await client.PostAsync(route, form);
    }

    private sealed class QuestionListEnvelope
    {
        public List<QuestionSummaryDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    private sealed class StudentQuestionBankListEnvelope
    {
        public List<StudentQuestionBankListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }
}
