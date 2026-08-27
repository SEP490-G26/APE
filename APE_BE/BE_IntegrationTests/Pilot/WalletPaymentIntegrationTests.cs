using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using Domain.Enums;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class WalletPaymentIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task API_WALLET_001_ReadWalletConstraints_AndAiTransactionHistory()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        await DbContext.AIVndBillingTransactions.InsertOneAsync(new AIVndBillingTransaction
        {
            Id = "64b000000000000000000401",
            UserId = TestIdentity.StudentA.UserId,
            FeatureKey = "question_generation",
            SourceEntityType = "Submission",
            SourceEntityId = "64b000000000000000000402",
            Status = "charged",
            ReportedCostUsd = 1.2m,
            UsdToVndRate = 26000m,
            ChargeMultiplier = 1.5m,
            MinimumBalanceVnd = 1000,
            ActualCostVnd = 31200,
            ChargedVnd = 46800,
            ActualDeductedVnd = 46800,
            BalanceBeforeVnd = 200000,
            BalanceAfterVnd = 153200,
            PolicySettingName = "AI_VND_BILLING",
            PolicyVersion = "v1",
            CreatedBy = TestIdentity.StudentA.UserId
        });

        using var client = CreateClient(TestIdentity.StudentA);

        using var constraintsResponse = await client.GetAsync("/api/student/wallet/topups/constraints");
        using var transactionsResponse = await client.GetAsync("/api/student/wallet/ai-transactions?page=1&limit=20");

        HttpResponseAssertions.AssertStatusCode(constraintsResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(transactionsResponse, HttpStatusCode.OK);

        var constraintsPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<WalletTopupConstraintsDto>>(constraintsResponse);
        var transactionsPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PaginatedResult<AIVndBillingTransactionDto>>>(transactionsResponse);

        Assert.IsNotNull(constraintsPayload.Data);
        Assert.AreEqual(WalletTopupService.MinTopupAmountVnd, constraintsPayload.Data.MinAmountVnd);
        Assert.AreEqual(WalletTopupService.MaxTopupAmountVnd, constraintsPayload.Data.MaxAmountVnd);

        Assert.IsNotNull(transactionsPayload.Data);
        Assert.AreEqual(1, transactionsPayload.Data.Total);
        Assert.AreEqual("question_generation", transactionsPayload.Data.Items.Single().FeatureKey);
    }

    [TestMethod]
    public async Task API_WALLET_002_CreateTopup_ThenReadPendingPaymentFromHistoryAndDetail()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        IntegrationTestRun.Factory.PayOS.CreatePaymentLinkResult = new PayOSCreatePaymentResult
        {
            CheckoutUrl = "https://payos.test/checkout/topup-002",
            QrCode = "wallet-002-qr",
            PaymentLinkId = "wallet-002-link"
        };

        using var client = CreateClient(TestIdentity.StudentA);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/student/wallet/topups/create",
            new CreateWalletTopupRequestDto { AmountVnd = 150_000 });
        HttpResponseAssertions.AssertStatusCode(createResponse, HttpStatusCode.OK);
        var createPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<WalletTopupSessionDto>>(createResponse);

        Assert.IsNotNull(createPayload.Data);

        using var historyResponse = await client.GetAsync("/api/student/wallet/topups/history");
        using var detailResponse = await client.GetAsync($"/api/student/wallet/topups/{createPayload.Data.PaymentId}");

        HttpResponseAssertions.AssertStatusCode(historyResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        var historyPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<WalletPaymentHistoryItemDto>>>(historyResponse);
        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<WalletPaymentHistoryItemDto>>(detailResponse);

        Assert.IsNotNull(historyPayload.Data);
        Assert.AreEqual(1, historyPayload.Data.Count);
        Assert.AreEqual(PaymentStatus.Pending, historyPayload.Data.Single().Status);

        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual(PaymentStatus.Pending, detailPayload.Data.Status);
        Assert.AreEqual(150_000, detailPayload.Data.AmountVnd);

        var storedPayment = await DbContext.Payments.Find(item => item.Id == createPayload.Data.PaymentId).FirstOrDefaultAsync();
        Assert.IsNotNull(storedPayment);
        Assert.AreEqual(TestIdentity.StudentA.UserId, storedPayment.UserId);
        Assert.AreEqual(PaymentStatus.Pending, storedPayment.Status);
        Assert.IsFalse(storedPayment.IsWalletCredited);
    }

    [TestMethod]
    public async Task API_WALLET_003_CreateTopup_WithAmountsOutsideConfiguredBounds_IsRejected()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var belowResponse = await client.PostAsJsonAsync(
            "/api/student/wallet/topups/create",
            new CreateWalletTopupRequestDto { AmountVnd = WalletTopupService.MinTopupAmountVnd - 1 });
        using var aboveResponse = await client.PostAsJsonAsync(
            "/api/student/wallet/topups/create",
            new CreateWalletTopupRequestDto { AmountVnd = WalletTopupService.MaxTopupAmountVnd + 1 });

        HttpResponseAssertions.AssertStatusCode(belowResponse, HttpStatusCode.BadRequest);
        HttpResponseAssertions.AssertStatusCode(aboveResponse, HttpStatusCode.BadRequest);

        var storedCount = await DbContext.Payments.CountDocumentsAsync(FilterDefinition<Payment>.Empty);
        Assert.AreEqual(0, storedCount);
    }

    [TestMethod]
    public async Task API_WALLET_004_NonOwnerCannotReadAnotherStudentsPaymentDetail()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        await DbContext.Payments.InsertOneAsync(CreatePendingPayment(
            "64b000000000000000000403",
            TestIdentity.StudentA.UserId,
            100_000,
            1200001));

        using var client = CreateClient(TestIdentity.StudentB);

        using var detailResponse = await client.GetAsync("/api/student/wallet/topups/64b000000000000000000403");
        using var historyResponse = await client.GetAsync("/api/student/wallet/topups/history");

        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.NotFound);
        HttpResponseAssertions.AssertStatusCode(historyResponse, HttpStatusCode.OK);

        var historyPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<WalletPaymentHistoryItemDto>>>(historyResponse);
        Assert.IsNotNull(historyPayload.Data);
        Assert.AreEqual(0, historyPayload.Data.Count);
    }

    [TestMethod]
    public async Task API_WALLET_005_CancelPendingPayment_PersistsCancelledState()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        await DbContext.Payments.InsertOneAsync(CreatePendingPayment(
            "64b000000000000000000404",
            TestIdentity.StudentA.UserId,
            120_000,
            1200002));

        using var client = CreateClient(TestIdentity.StudentA);

        using var cancelResponse = await client.PostAsync("/api/student/wallet/topups/64b000000000000000000404/cancel", null);
        using var detailResponse = await client.GetAsync("/api/student/wallet/topups/64b000000000000000000404");

        HttpResponseAssertions.AssertStatusCode(cancelResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);

        var cancelPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<WalletPaymentHistoryItemDto>>(cancelResponse);
        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<WalletPaymentHistoryItemDto>>(detailResponse);

        Assert.IsNotNull(cancelPayload.Data);
        Assert.AreEqual(PaymentStatus.Cancelled, cancelPayload.Data.Status);
        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual(PaymentStatus.Cancelled, detailPayload.Data.Status);

        var storedPayment = await DbContext.Payments.Find(item => item.Id == "64b000000000000000000404").FirstOrDefaultAsync();
        Assert.IsNotNull(storedPayment);
        Assert.AreEqual(PaymentStatus.Cancelled, storedPayment.Status);
        Assert.IsFalse(storedPayment.IsWalletCredited);
    }

    [TestMethod]
    public async Task API_WALLET_006_WebhookCompletion_CreditsWalletExactlyOnce_AndPersistsCompletedPayment()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        const string paymentId = "64b000000000000000000405";
        const long amount = 200_000;
        const long orderCode = 1200003;

        await DbContext.Payments.InsertOneAsync(CreatePendingPayment(paymentId, TestIdentity.StudentA.UserId, amount, orderCode));
        IntegrationTestRun.Factory.PayOS.VerifyWebhookResult = new PayOSWebhookVerificationResult
        {
            OrderCode = orderCode,
            AmountVnd = amount,
            Success = true,
            Code = "00",
            Description = "Success",
            Reference = "wallet-006-reference",
            PaymentLinkId = "wallet-006-link"
        };

        using var anonymousClient = CreateAnonymousClient();
        using var webhookResponse = await anonymousClient.PostAsync(
            "/api/payments/payos/webhook",
            JsonContent.Create(new { orderCode, amount }));
        using var replayResponse = await anonymousClient.PostAsync(
            "/api/payments/payos/webhook",
            JsonContent.Create(new { orderCode, amount }));

        HttpResponseAssertions.AssertStatusCode(webhookResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(replayResponse, HttpStatusCode.OK);

        using var studentClient = CreateClient(TestIdentity.StudentA);
        using var detailResponse = await studentClient.GetAsync($"/api/student/wallet/topups/{paymentId}");
        using var historyResponse = await studentClient.GetAsync("/api/student/wallet/topups/history");

        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(historyResponse, HttpStatusCode.OK);

        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<WalletPaymentHistoryItemDto>>(detailResponse);
        var historyPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<WalletPaymentHistoryItemDto>>>(historyResponse);

        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual(PaymentStatus.Completed, detailPayload.Data.Status);
        Assert.IsTrue(detailPayload.Data.IsWalletCredited);
        Assert.IsNotNull(historyPayload.Data);
        Assert.AreEqual(PaymentStatus.Completed, historyPayload.Data.Single().Status);

        var storedPayment = await DbContext.Payments.Find(item => item.Id == paymentId).FirstOrDefaultAsync();
        var storedUser = await DbContext.Users.Find(item => item.Id == TestIdentity.StudentA.UserId).FirstOrDefaultAsync();
        Assert.IsNotNull(storedPayment);
        Assert.IsNotNull(storedUser);
        Assert.AreEqual(PaymentStatus.Completed, storedPayment.Status);
        Assert.IsTrue(storedPayment.IsWalletCredited);
        Assert.AreEqual(700_000, storedUser.AiWalletBalanceVnd);
    }

    private static Payment CreatePendingPayment(string id, string userId, long amountVnd, long orderCode)
    {
        return new Payment
        {
            Id = id,
            UserId = userId,
            Provider = "PayOS",
            OrderCode = orderCode,
            AmountVnd = amountVnd,
            BalanceBeforeVnd = 500_000,
            BalanceAfterVnd = 500_000,
            Description = $"Wallet payment {amountVnd}",
            CheckoutUrl = "https://payos.test/checkout/pending",
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(3)
        };
    }
}
