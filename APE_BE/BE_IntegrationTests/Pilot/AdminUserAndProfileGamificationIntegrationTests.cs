using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using Domain.Enums;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class AdminUserAndProfileGamificationIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task API_ADMIN_USER_001_AdminCanListUsers_AndReadUserDetail()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        await DbContext.Users.UpdateOneAsync(
            item => item.Id == TestIdentity.StudentA.UserId,
            Builders<User>.Update
                .Set(item => item.ExpPoints, 200)
                .Set(item => item.Status, "Active"));
        await DbContext.Users.UpdateOneAsync(
            item => item.Id == TestIdentity.StudentB.UserId,
            Builders<User>.Update
                .Set(item => item.Status, "Banned")
                .Set(item => item.ExpPoints, 50));

        using var client = CreateClient(TestIdentity.Admin);

        using var listResponse = await client.GetAsync("/api/admin/users?role=Student&status=Active&page=1&limit=20");
        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<AdminUserListEnvelope>>(listResponse);

        Assert.IsTrue(listPayload.Success);
        Assert.IsNotNull(listPayload.Data);
        Assert.AreEqual(1, listPayload.Data.Items.Count);
        Assert.AreEqual(TestIdentity.StudentA.UserId, listPayload.Data.Items.Single().Id);

        using var detailResponse = await client.GetAsync($"/api/admin/users/{TestIdentity.StudentA.UserId}");
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);
        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<AdminUserDetailDto>>(detailResponse);

        Assert.IsTrue(detailPayload.Success);
        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual(TestIdentity.StudentA.Email, detailPayload.Data.Email);
        Assert.AreEqual("Active", detailPayload.Data.Status);
    }

    [TestMethod]
    public async Task API_ADMIN_USER_002_AdminCanUpdateUserStatus_AndPersistBan()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUserWithRefreshTokenAsync(TestIdentity.StudentB, "admin-user-refresh-token");
        await DbContext.Users.InsertOneAsync(new User
        {
            Id = TestIdentity.Admin.UserId,
            GoogleId = $"it-{TestIdentity.Admin.UserId}",
            Email = TestIdentity.Admin.Email,
            FullName = TestIdentity.Admin.FullName,
            Role = TestIdentity.Admin.Role,
            Status = "Active",
            AvatarUrl = "https://example.test/admin.png"
        });

        using var client = CreateClient(TestIdentity.Admin);

        using var patchResponse = await client.PatchAsJsonAsync(
            $"/api/admin/users/{TestIdentity.StudentB.UserId}/status",
            new UpdateUserStatusDto { Status = "Banned" });
        HttpResponseAssertions.AssertStatusCode(patchResponse, HttpStatusCode.OK);

        using var detailResponse = await client.GetAsync($"/api/admin/users/{TestIdentity.StudentB.UserId}");
        HttpResponseAssertions.AssertStatusCode(detailResponse, HttpStatusCode.OK);
        var detailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<AdminUserDetailDto>>(detailResponse);

        Assert.IsNotNull(detailPayload.Data);
        Assert.AreEqual("Banned", detailPayload.Data.Status);

        var storedUser = await DbContext.Users.Find(item => item.Id == TestIdentity.StudentB.UserId).FirstOrDefaultAsync();
        Assert.IsNotNull(storedUser);
        Assert.AreEqual("Banned", storedUser.Status);
        Assert.AreEqual(0, storedUser.RefreshTokens.Count);
    }

    [TestMethod]
    public async Task API_PROFILE_003_AnalyticsSummary_ReturnsAggregatedStudentProgress()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        var session = CreateSession("64b000000000000000000304", TestIdentity.StudentA.UserId, DateTime.UtcNow.AddDays(-1), 15, SessionStatus.Submitted, 9);
        await DbContext.PracticeSessions.InsertOneAsync(session);
        await DbContext.FESubmissions.InsertOneAsync(new FE_Submission
        {
            Id = "64b000000000000000000305",
            SessionId = session.Id,
            FeQuestionId = IntegrationSeedFactory.SystemFeQuestionEasyId,
            CourseId = IntegrationSeedFactory.CourseId,
            UserAnswer = ["A"],
            IsCorrect = true
        });
        await DbContext.PE_Submissions.InsertOneAsync(PE_Submission.Create(
            "64b000000000000000000306",
            IntegrationSeedFactory.PeQuestionId,
            session.Id,
            IntegrationSeedFactory.CourseId,
            SubmissionMode.Practice,
            ["arrays"],
            [CodeFile.Create("main.c", "int main(){return 0;}")],
            50,
            10,
            1,
            DateTime.UtcNow));

        using var client = CreateClient(TestIdentity.StudentA);

        using var response = await client.GetAsync("/api/student/analytics/summary");
        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StudentSummaryDto>>(response);

        Assert.IsTrue(payload.Success);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual(2, payload.Data.TotalProblemsSolved);
        Assert.AreEqual(9, payload.Data.AverageScore);
        Assert.AreEqual(15, payload.Data.TotalTimeSpentMinutes);
    }

    [TestMethod]
    public async Task API_PROFILE_004_GamificationEndpoints_ReturnExpectedStreakAndLeaderboard()
    {
        var seedFactory = CreateSeedFactory();
        await seedFactory.SeedUsersAsync();

        await DbContext.Users.UpdateOneAsync(
            item => item.Id == TestIdentity.StudentA.UserId,
            Builders<User>.Update
                .Set(item => item.Badges, new List<string> { "First Blood" })
                .Set(item => item.HighestStreak, 3)
                .Set(item => item.LastPracticeDate, DateTime.UtcNow.Date));

        await DbContext.PracticeSessions.InsertManyAsync(
        [
            CreateSession("64b000000000000000000307", TestIdentity.StudentA.UserId, DateTime.UtcNow.AddDays(-1), 8, SessionStatus.Submitted, 12),
            CreateSession("64b000000000000000000308", TestIdentity.StudentA.UserId, DateTime.UtcNow.AddDays(-2), 7, SessionStatus.Submitted, 11),
            CreateSession("64b000000000000000000309", TestIdentity.StudentB.UserId, DateTime.UtcNow.AddDays(-1), 10, SessionStatus.Submitted, 5)
        ]);

        using var client = CreateClient(TestIdentity.StudentA);

        using var streakResponse = await client.GetAsync("/api/student/streak");
        using var leaderboardResponse = await client.GetAsync("/api/student/leaderboard?scope=Global");

        HttpResponseAssertions.AssertStatusCode(streakResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(leaderboardResponse, HttpStatusCode.OK);

        var streakPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<StreakDto>>(streakResponse);
        var leaderboardPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<List<LeaderboardEntryDto>>>(leaderboardResponse);

        Assert.IsNotNull(streakPayload.Data);
        Assert.IsTrue(streakPayload.Data.CurrentStreak >= 2);
        Assert.IsNotNull(leaderboardPayload.Data);
        var studentEntry = leaderboardPayload.Data.Single(item => item.UserId == TestIdentity.StudentA.UserId);
        Assert.AreEqual(1, studentEntry.Rank);
    }

    private static PracticeSession CreateSession(string id, string studentId, DateTime endTimeUtc, int durationMinutes, SessionStatus status, double score)
    {
        var safeEnd = DateTime.SpecifyKind(endTimeUtc, DateTimeKind.Utc);
        return new PracticeSession
        {
            Id = id,
            StudentId = studentId,
            ExamId = IntegrationSeedFactory.ExamId,
            StartTime = safeEnd.AddMinutes(-durationMinutes),
            EndTime = safeEnd,
            Status = status,
            TotalScore = score
        };
    }

    private sealed class AdminUserListEnvelope
    {
        public List<AdminUserListDto> Items { get; set; } = new();
        public long Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public int TotalPages { get; set; }
    }
}
