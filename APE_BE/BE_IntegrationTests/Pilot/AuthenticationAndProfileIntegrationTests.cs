using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Application.Common;
using Application.DTOs;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using Google.Apis.Auth;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class AuthenticationAndProfileIntegrationTests
    : IntegrationTestBase
{
    [TestMethod]
    public async Task API_AUTH_001_GoogleLogin_ReturnsTokens_AndPersistsUser()
    {
        const string idToken = "integration-google-login-001";
        const string googleSubject = "integration-google-subject-001";
        const string email = "integration.auth001@fpt.edu.vn";

        IntegrationTestRun.Factory.GoogleTokenValidator.Register(
            idToken,
            CreateGooglePayload(
                subject: googleSubject,
                email: email,
                name: "Integration Auth 001",
                picture: "https://example.test/avatars/auth001.png"));

        using var client = CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/google",
            new GoogleLoginRequest
            {
                IdToken = idToken
            });

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<AuthResponse>>(response);
        Assert.IsTrue(payload.Success);
        Assert.IsNotNull(payload.Data);
        Assert.IsFalse(string.IsNullOrWhiteSpace(payload.Data.AccessToken));
        Assert.IsFalse(string.IsNullOrWhiteSpace(payload.Data.RefreshToken));
        Assert.IsNotNull(payload.Data.User);
        Assert.AreEqual(email, payload.Data.User.Email);

        var storedUser = await DbContext.Users
            .Find(user => user.GoogleId == googleSubject)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedUser);
        Assert.AreEqual(email, storedUser.Email);
        Assert.AreEqual("Student", storedUser.Role);
        Assert.AreEqual(1, storedUser.RefreshTokens.Count);
        Assert.AreEqual(payload.Data.RefreshToken, storedUser.RefreshTokens.Single().Token);
        Assert.IsTrue(storedUser.CurrentStreak >= 1);
        Assert.IsTrue(storedUser.HighestStreak >= 1);
    }

    [TestMethod]
    public async Task API_AUTH_002_RefreshAndMe_Workflow_RotatesRefreshTokens()
    {
        const string oldRefreshToken = "integration-refresh-token-old";
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUserWithRefreshTokenAsync(TestIdentity.StudentA, oldRefreshToken);

        using var refreshClient = CreateAnonymousClient();
        using var refreshResponse = await refreshClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = oldRefreshToken
            });

        HttpResponseAssertions.AssertStatusCode(refreshResponse, HttpStatusCode.OK);

        var refreshPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<AuthResponse>>(refreshResponse);
        Assert.IsTrue(refreshPayload.Success);
        Assert.IsNotNull(refreshPayload.Data);
        Assert.AreEqual(TestIdentity.StudentA.Email, refreshPayload.Data.User.Email);
        Assert.AreNotEqual(oldRefreshToken, refreshPayload.Data.RefreshToken);

        var storedAfterRefresh = await DbContext.Users
            .Find(user => user.Id == TestIdentity.StudentA.UserId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedAfterRefresh);
        CollectionAssert.DoesNotContain(
            storedAfterRefresh.RefreshTokens.Select(token => token.Token).ToList(),
            oldRefreshToken);
        CollectionAssert.Contains(
            storedAfterRefresh.RefreshTokens.Select(token => token.Token).ToList(),
            refreshPayload.Data.RefreshToken);

        using var bearerClient = CreateAnonymousClient();
        bearerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshPayload.Data.AccessToken);

        using var meResponse = await bearerClient.GetAsync("/api/auth/me");
        HttpResponseAssertions.AssertStatusCode(meResponse, HttpStatusCode.OK);

        var mePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<UserProfileDto>>(meResponse);
        Assert.IsTrue(mePayload.Success);
        Assert.IsNotNull(mePayload.Data);
        Assert.AreEqual(TestIdentity.StudentA.UserId, mePayload.Data.Id);
        Assert.AreEqual(TestIdentity.StudentA.Email, mePayload.Data.Email);

        using var replayResponse = await refreshClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = oldRefreshToken
            });

        HttpResponseAssertions.AssertStatusCode(replayResponse, HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task API_AUTH_003_InvalidRefreshToken_IsRejected_WithoutMutatingUserState()
    {
        const string preservedRefreshToken = "integration-refresh-token-preserved";
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUserWithRefreshTokenAsync(TestIdentity.StudentA, preservedRefreshToken);

        using var client = CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest
            {
                RefreshToken = "integration-refresh-token-unknown"
            });

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.Unauthorized);

        var storedUser = await DbContext.Users
            .Find(user => user.Id == TestIdentity.StudentA.UserId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedUser);
        CollectionAssert.AreEquivalent(
            new[] { preservedRefreshToken },
            storedUser.RefreshTokens.Select(token => token.Token).ToArray());
    }

    [TestMethod]
    public async Task API_AUTH_004_MissingJwt_IsRejected_ForMe()
    {
        const string preservedRefreshToken = "integration-refresh-token-protected";
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUserWithRefreshTokenAsync(TestIdentity.StudentA, preservedRefreshToken);

        using var client = CreateAnonymousClient();
        using var meResponse = await client.GetAsync("/api/auth/me");
        HttpResponseAssertions.AssertStatusCode(meResponse, HttpStatusCode.Unauthorized);

        var storedUser = await DbContext.Users
            .Find(user => user.Id == TestIdentity.StudentA.UserId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedUser);
        CollectionAssert.AreEquivalent(
            new[] { preservedRefreshToken },
            storedUser.RefreshTokens.Select(token => token.Token).ToArray());
    }

    [TestMethod]
    public async Task API_PROFILE_001_UpdateProfile_PersistsChangesToMongo()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        var request = new UpdateProfileDto
        {
            FullName = "Student A Updated",
            AvatarUrl = "https://example.test/avatars/student-a-updated.png"
        };

        using var response = await client.PutAsJsonAsync("/api/student/profile", request);

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadJsonAsync<UserProfileDto>(response);
        Assert.AreEqual(request.FullName, payload.FullName);
        Assert.AreEqual(request.AvatarUrl, payload.AvatarUrl);

        var storedUser = await DbContext.Users
            .Find(user => user.Id == TestIdentity.StudentA.UserId)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedUser);
        Assert.AreEqual(request.FullName, storedUser.FullName);
        Assert.AreEqual(request.AvatarUrl, storedUser.AvatarUrl);
    }

    private static GoogleJsonWebSignature.Payload CreateGooglePayload(
        string subject,
        string email,
        string name,
        string picture)
    {
        return new GoogleJsonWebSignature.Payload
        {
            Subject = subject,
            Email = email,
            Name = name,
            Picture = picture,
            EmailVerified = true,
            Issuer = "https://accounts.google.com"
        };
    }
}
