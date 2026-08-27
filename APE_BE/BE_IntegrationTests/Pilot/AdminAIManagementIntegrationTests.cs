using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using Domain.Enums;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class AdminAIManagementIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task API_ADMIN_AI_001_AdminCanReadAndUpdateBillingConfig()
    {
        using var client = CreateClient(TestIdentity.Admin);

        using var getResponse = await client.GetAsync("/api/admin/ai/billing-config");
        HttpResponseAssertions.AssertStatusCode(getResponse, HttpStatusCode.OK);

        using var putResponse = await client.PutAsJsonAsync(
            "/api/admin/ai/billing-config",
            new AIVndBillingConfigUpsertRequestDto
            {
                UsdToVndRate = 27500,
                MinBalanceVnd = 2500,
                ChargeMultiplier = 1.2m
            });
        HttpResponseAssertions.AssertStatusCode(putResponse, HttpStatusCode.OK);

        using var confirmResponse = await client.GetAsync("/api/admin/ai/billing-config");
        HttpResponseAssertions.AssertStatusCode(confirmResponse, HttpStatusCode.OK);

        var confirmPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<AIVndBillingConfigDto>>(confirmResponse);
        Assert.IsNotNull(confirmPayload.Data);
        Assert.AreEqual(27500m, confirmPayload.Data.UsdToVndRate);
        Assert.AreEqual(2500, confirmPayload.Data.MinBalanceVnd);
        Assert.AreEqual(1.2m, confirmPayload.Data.ChargeMultiplier);

        var setting = await DbContext.SystemSettings.Find(item => item.SettingName == "AI_VND_BILLING").FirstOrDefaultAsync();
        Assert.IsNotNull(setting);
    }

    [TestMethod]
    public async Task API_ADMIN_AI_002_AdminAiReportingEndpoints_ReturnSeededData()
    {
        await SeedAdminAiReportingDataAsync();
        using var client = CreateClient(TestIdentity.Admin);

        await AssertOkAsync(client.GetAsync("/api/admin/ai/billing-summary?take=20"));
        await AssertOkAsync(client.GetAsync("/api/admin/ai/credit-transactions?page=1&limit=20"));
        await AssertOkAsync(client.GetAsync("/api/admin/ai/credit-summary?take=20"));
        await AssertOkAsync(client.GetAsync("/api/admin/ai/usage-logs?page=1&limit=20"));
        await AssertOkAsync(client.GetAsync("/api/admin/ai/usage-summary?take=20"));
        await AssertOkAsync(client.GetAsync("/api/admin/ai/usage-summary/artifacts?take=20"));
        await AssertOkAsync(client.GetAsync("/api/admin/ai/usage-summary/users?take=20"));

        using var mentorResponse = await client.GetAsync("/api/admin/ai/mentor-feedbacks?page=1&limit=20");
        HttpResponseAssertions.AssertStatusCode(mentorResponse, HttpStatusCode.OK);
        var mentorPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PaginatedResult<AIMentorFeedbackAdminDto>>>(mentorResponse);
        Assert.IsNotNull(mentorPayload.Data);
        Assert.AreEqual(1, mentorPayload.Data.Total);
        Assert.AreEqual("needs_fix", mentorPayload.Data.Items.Single().Verdict);
    }

    [TestMethod]
    public async Task API_ADMIN_AI_004_AdminCanManageProviderCredentials_AndReadControlledModelCatalog()
    {
        using var client = CreateClient(TestIdentity.Admin);

        using var listResponse = await client.GetAsync("/api/admin/ai/provider-credentials");
        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/ai/provider-credentials",
            new CreateAIProviderCredentialRequestDto
            {
                Provider = "OpenAI",
                Enabled = true,
                ApiKey = "integration-openai-key",
                BaseUrl = "https://api.openai.test",
                TimeoutSeconds = 30
            });
        HttpResponseAssertions.AssertStatusCode(createResponse, HttpStatusCode.OK);

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/admin/ai/provider-credentials",
            new UpdateAIProviderCredentialRequestDto
            {
                Provider = "OpenAI",
                Enabled = true,
                ApiVersion = "2026-08-09",
                Headers = new Dictionary<string, string> { ["x-test"] = "one" }
            });
        HttpResponseAssertions.AssertStatusCode(updateResponse, HttpStatusCode.OK);

        using var updateRouteResponse = await client.PutAsJsonAsync(
            "/api/admin/ai/provider-credentials/OpenAI",
            new UpdateAIProviderCredentialRequestDto
            {
                Enabled = false,
                Headers = new Dictionary<string, string> { ["x-test"] = "two" }
            });
        HttpResponseAssertions.AssertStatusCode(updateRouteResponse, HttpStatusCode.OK);

        using var batchResponse = await client.PutAsJsonAsync(
            "/api/admin/ai/provider-credentials/batch",
            new AIProviderCredentialsUpsertRequestDto
            {
                DeepSeek = new AIProviderCredentialInputDto
                {
                    Enabled = true,
                    ApiKey = "integration-deepseek-key",
                    BaseUrl = "https://api.deepseek.test",
                    TimeoutSeconds = 45
                }
            });
        HttpResponseAssertions.AssertStatusCode(batchResponse, HttpStatusCode.OK);

        using var modelsResponse = await client.GetAsync("/api/admin/ai/providers/OpenAI/models");
        HttpResponseAssertions.AssertStatusCode(modelsResponse, HttpStatusCode.OK);
        var modelsPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<AIProviderModelOptionDto>>>(modelsResponse);
        Assert.IsNotNull(modelsPayload.Data);
        Assert.IsTrue(modelsPayload.Data.Any(item => item.Id == "gpt-4o-mini"));

        using var deleteResponse = await client.DeleteAsync("/api/admin/ai/provider-credentials/OpenAI");
        HttpResponseAssertions.AssertStatusCode(deleteResponse, HttpStatusCode.OK);

        var setting = await DbContext.SystemSettings.Find(item => item.SettingName == "AI_PROVIDER_CREDENTIALS").FirstOrDefaultAsync();
        Assert.IsNotNull(setting);
    }

    [TestMethod]
    public async Task API_ADMIN_AI_005_AdminCanListReadAndUpdateAgentConfiguration()
    {
        await DbContext.AIAgents.InsertOneAsync(new AIAgent
        {
            Id = "64b000000000000000000505",
            AgentRole = AIAgentRole.Mentor,
            Provider = AIProvider.OpenAI,
            ModelName = "gpt-4o-mini",
            MaxTokens = 1024,
            Temperature = 0.2,
            CreditCost = 0,
            IsEnabled = true,
            UpdatedBy = TestIdentity.Admin.UserId,
            UpdatedAt = DateTime.UtcNow
        });

        using var client = CreateClient(TestIdentity.Admin);

        using var listResponse = await client.GetAsync("/api/admin/ai/agents");
        using var detailResponse = await client.GetAsync("/api/admin/ai/agents/Mentor");

        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/admin/ai/agents/Mentor",
            new UpsertAIAgentRequestDto
            {
                Provider = "DeepSeek",
                ModelName = "deepseek-chat",
                MaxTokens = 2048,
                Temperature = 0.1,
                CreditCost = 0,
                IsEnabled = true,
                Notes = "integration update"
            });
        HttpResponseAssertions.AssertStatusCode(updateResponse, HttpStatusCode.OK);

        var storedAgent = await DbContext.AIAgents.Find(item => item.AgentRole == AIAgentRole.Mentor).FirstOrDefaultAsync();
        Assert.IsNotNull(storedAgent);
        Assert.AreEqual(AIProvider.DeepSeek, storedAgent.Provider);
        Assert.AreEqual("deepseek-chat", storedAgent.ModelName);
    }

    [TestMethod]
    public async Task API_ADMIN_AI_006_AdminCanManageRuleArtifactLifecycle()
    {
        using var client = CreateClient(TestIdentity.Admin);

        using var listResponse = await client.GetAsync("/api/admin/ai/rule-artifacts");
        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/ai/rule-artifacts",
            new CreateAIRuleArtifactRequestDto
            {
                ArtifactType = "prompt",
                ArtifactKey = "integration-artifact",
                Description = "integration prompt",
                Summary = "integration summary",
                ContentJson = "{\"template\":\"v1\"}",
                ActivateNow = true
            });
        HttpResponseAssertions.AssertStatusCode(createResponse, HttpStatusCode.OK);
        var createPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<AIRuleArtifactAdminDto>>(createResponse);
        Assert.IsNotNull(createPayload.Data);

        using var versionResponse = await client.PostAsJsonAsync(
            $"/api/admin/ai/rule-artifacts/{createPayload.Data.Id}/versions",
            new CreateAIRuleArtifactVersionRequestDto
            {
                ContentJson = "{\"template\":\"v2\"}",
                ChangeReason = "integration update",
                ActivateNow = false
            });
        HttpResponseAssertions.AssertStatusCode(versionResponse, HttpStatusCode.OK);

        using var activateResponse = await client.PostAsync(
            $"/api/admin/ai/rule-artifacts/{createPayload.Data.Id}/versions/v2/activate",
            null);
        HttpResponseAssertions.AssertStatusCode(activateResponse, HttpStatusCode.OK);

        using var deleteVersionResponse = await client.DeleteAsync(
            $"/api/admin/ai/rule-artifacts/{createPayload.Data.Id}/versions/v1");
        HttpResponseAssertions.AssertStatusCode(deleteVersionResponse, HttpStatusCode.OK);

        using var deleteArtifactResponse = await client.DeleteAsync($"/api/admin/ai/rule-artifacts/{createPayload.Data.Id}");
        HttpResponseAssertions.AssertStatusCode(deleteArtifactResponse, HttpStatusCode.OK);

        var remainingCount = await DbContext.AIRuleArtifacts.CountDocumentsAsync(FilterDefinition<AIRuleArtifact>.Empty);
        Assert.AreEqual(0, remainingCount);
    }

    private async Task SeedAdminAiReportingDataAsync()
    {
        await CreateSeedFactory().SeedUsersAsync();

        await DbContext.AIAgents.InsertOneAsync(new AIAgent
        {
            Id = "64b000000000000000000506",
            AgentRole = AIAgentRole.Mentor,
            Provider = AIProvider.OpenAI,
            ModelName = "gpt-4o-mini",
            IsEnabled = true,
            UpdatedBy = TestIdentity.Admin.UserId,
            UpdatedAt = DateTime.UtcNow
        });

        await DbContext.AIVndBillingTransactions.InsertOneAsync(new AIVndBillingTransaction
        {
            Id = "64b000000000000000000507",
            UserId = TestIdentity.StudentA.UserId,
            FeatureKey = "mentor",
            SourceEntityType = "Submission",
            SourceEntityId = "64b000000000000000000508",
            Status = "charged",
            ReportedCostUsd = 1.25m,
            UsdToVndRate = 26000m,
            ChargeMultiplier = 1.5m,
            MinimumBalanceVnd = 1000,
            ActualCostVnd = 32500,
            ChargedVnd = 48750,
            ActualDeductedVnd = 48750,
            BalanceBeforeVnd = 100000,
            BalanceAfterVnd = 51250,
            UsageLogIds = ["64b000000000000000000509"],
            PolicySettingName = "AI_VND_BILLING",
            PolicyVersion = "v1",
            CreatedBy = TestIdentity.Admin.UserId
        });

        await DbContext.AICreditTransactions.InsertOneAsync(new AICreditTransaction
        {
            Id = "64b00000000000000000050a",
            UserId = TestIdentity.StudentA.UserId,
            FeatureKey = "question_generation_review",
            SourceEntityType = "Submission",
            SourceEntityId = "64b00000000000000000050b",
            Status = "charged",
            ChargedCredits = 12,
            PolicySettingName = "AI_CREDIT_POLICY",
            PolicyVersion = "legacy",
            CreatedBy = TestIdentity.Admin.UserId
        });

        await DbContext.APIUsageLogs.InsertOneAsync(new AIUsageLog
        {
            Id = "64b000000000000000000509",
            TriggeredBy = TestIdentity.StudentA.UserId,
            AgentId = "64b000000000000000000506",
            TokensUsed = 321,
            CostUsd = 1.25m,
            PayloadData = new Dictionary<string, object?>
            {
                ["provider"] = "OpenAI",
                ["effective_model"] = "gpt-4o-mini",
                ["normalized_model_key"] = "gpt-4o-mini",
                ["feature"] = "mentor",
                ["step"] = "review",
                ["prompt_key"] = "mentor_prompt",
                ["prompt_version"] = "v1",
                ["policy_key"] = "mentor_policy",
                ["policy_version"] = "v1",
                ["rubric_key"] = "mentor_rubric",
                ["rubric_version"] = "v1",
                ["fallback_used"] = false
            }
        });

        await DbContext.AIMentorFeedbacks.InsertOneAsync(new AIMentorFeedback
        {
            Id = "64b00000000000000000050c",
            AgentId = "64b000000000000000000506",
            SubmissionId = "64b00000000000000000050d",
            Verdict = "needs_fix",
            FeedbackText = "integration feedback",
            ModelName = "gpt-4o-mini",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static async Task AssertOkAsync(Task<HttpResponseMessage> responseTask)
    {
        using var response = await responseTask;
        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);
    }
}
