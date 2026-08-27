using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using BE_IntegrationTests.Infrastructure;
using Domain.Entities;
using MongoDB.Driver;

namespace BE_IntegrationTests.Pilot;

[TestClass]
public sealed class CourseIntegrationTests
    : IntegrationTestBase
{
    [TestMethod]
    public async Task API_COURSE_001_AdminCourseLifecycle_CreateReadUpdateStatusDelete_PersistsExactlyOnce()
    {
        await CreateSeedFactory().SeedUsersAsync();

        using var client = CreateClient(TestIdentity.Admin);

        var createRequest = new CreateCourseDto
        {
            Code = "int-course-lifecycle",
            Name = "Lifecycle Course",
            Description = "Created in integration lifecycle test",
            ExamMatrix =
            [
                new ExamMatrixItemDto
                {
                    Difficulty = "Easy",
                    Count = 10,
                    PointsPerQuestion = 1
                }
            ]
        };

        using var createResponse = await client.PostAsJsonAsync("/api/admin/courses", createRequest);
        HttpResponseAssertions.AssertStatusCode(createResponse, HttpStatusCode.OK);

        var createdPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<CourseDto>>(createResponse);
        Assert.IsNotNull(createdPayload.Data);
        Assert.AreEqual("INT-COURSE-LIFECYCLE", createdPayload.Data.Code);
        Assert.AreEqual("Inactive", createdPayload.Data.Status);

        using var getResponse = await client.GetAsync($"/api/admin/courses/{createdPayload.Data.Id}");
        HttpResponseAssertions.AssertStatusCode(getResponse, HttpStatusCode.OK);

        var updateRequest = new UpdateCourseDto
        {
            Name = "Lifecycle Course Updated",
            Description = "Updated description",
            ExamMatrix =
            [
                new ExamMatrixItemDto
                {
                    Difficulty = "Medium",
                    Count = 4,
                    PointsPerQuestion = 2
                }
            ]
        };

        using var updateResponse = await client.PutAsJsonAsync($"/api/admin/courses/{createdPayload.Data.Id}", updateRequest);
        HttpResponseAssertions.AssertStatusCode(updateResponse, HttpStatusCode.OK);

        using var statusResponse = await client.PatchAsJsonAsync(
            $"/api/admin/courses/{createdPayload.Data.Id}/status",
            new UpdateCourseStatusDto
            {
                Status = "Active"
            });
        HttpResponseAssertions.AssertStatusCode(statusResponse, HttpStatusCode.OK);

        var statusPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<CourseDto>>(statusResponse);
        Assert.IsNotNull(statusPayload.Data);
        Assert.AreEqual("Active", statusPayload.Data.Status);

        using var deleteResponse = await client.DeleteAsync($"/api/admin/courses/{createdPayload.Data.Id}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var storedCourse = await DbContext.Courses
            .Find(item => item.Id == createdPayload.Data.Id)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(storedCourse);
        Assert.AreEqual("Lifecycle Course Updated", storedCourse.Name);
        Assert.AreEqual("Deleted", storedCourse.Status);
        Assert.AreEqual(TestIdentity.Admin.UserId, storedCourse.LastModifiedBy);
        Assert.AreEqual(TestIdentity.Admin.UserId, storedCourse.DeletedBy);
        Assert.IsNotNull(storedCourse.DeletedAt);

        using var deletedGetResponse = await client.GetAsync($"/api/admin/courses/{createdPayload.Data.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, deletedGetResponse.StatusCode);
    }

    [TestMethod]
    public async Task API_COURSE_002_AdminCourseList_ReturnsFilteredPageMetadata()
    {
        var scenario = await CreateSeedFactory().SeedCourseScenarioAsync();

        using var client = CreateClient(TestIdentity.Admin);
        using var response = await client.GetAsync("/api/admin/courses?page=1&limit=20&keyword=ACTIVE&status=Active");

        HttpResponseAssertions.AssertStatusCode(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<CourseListResponseDto>>(response);
        Assert.IsNotNull(payload.Data);
        Assert.AreEqual(1, payload.Data.Total);
        Assert.AreEqual(1, payload.Data.Items.Count);
        Assert.AreEqual(scenario.ActiveCourse.Id, payload.Data.Items.Single().Id);
        Assert.AreEqual("Active", payload.Data.Items.Single().Status);
    }

    [TestMethod]
    public async Task API_COURSE_003_StudentCourseCatalog_OnlyReturnsActiveCourses()
    {
        var scenario = await CreateSeedFactory().SeedCourseScenarioAsync();

        using var client = CreateClient(TestIdentity.StudentA);

        using var listResponse = await client.GetAsync("/api/student/courses?page=1&limit=20");
        using var activeDetailResponse = await client.GetAsync($"/api/student/courses/{scenario.ActiveCourse.Id}");
        using var inactiveDetailResponse = await client.GetAsync($"/api/student/courses/{scenario.InactiveCourse.Id}");

        HttpResponseAssertions.AssertStatusCode(listResponse, HttpStatusCode.OK);
        HttpResponseAssertions.AssertStatusCode(activeDetailResponse, HttpStatusCode.OK);
        Assert.AreEqual(HttpStatusCode.NotFound, inactiveDetailResponse.StatusCode);

        var listPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<PagedCourses>>(listResponse);
        var activeDetailPayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse<Course>>(activeDetailResponse);
        var inactivePayload = await HttpResponseAssertions.ReadJsonAsync<ApiResponse>(inactiveDetailResponse);

        Assert.IsNotNull(listPayload.Data);
        CollectionAssert.Contains(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.ActiveCourse.Id);
        CollectionAssert.DoesNotContain(listPayload.Data.Items.Select(item => item.Id).ToList(), scenario.InactiveCourse.Id);

        Assert.IsNotNull(activeDetailPayload.Data);
        Assert.AreEqual(scenario.ActiveCourse.Id, activeDetailPayload.Data.Id);
        Assert.IsFalse(inactivePayload.Success);
        Assert.AreEqual("Course not found", inactivePayload.Error);
    }

    [TestMethod]
    public async Task API_COURSE_004_InvalidCreateAndUpdate_DoNotMutateExistingCourses()
    {
        var seedFactory = CreateSeedFactory();
        var scenario = await seedFactory.SeedCourseScenarioAsync();

        using var client = CreateClient(TestIdentity.Admin);

        using var duplicateCreateResponse = await client.PostAsJsonAsync(
            "/api/admin/courses",
            new CreateCourseDto
            {
                Code = scenario.ActiveCourse.Code,
                Name = "Duplicate Course"
            });

        HttpResponseAssertions.AssertStatusCode(duplicateCreateResponse, HttpStatusCode.BadRequest);

        using var invalidUpdateResponse = await client.PutAsJsonAsync(
            $"/api/admin/courses/{scenario.ActiveCourse.Id}",
            new UpdateCourseDto
            {
                ExamMatrix =
                [
                    new ExamMatrixItemDto
                    {
                        Difficulty = "Impossible",
                        Count = 0,
                        PointsPerQuestion = 0
                    }
                ]
            });

        Assert.AreEqual(HttpStatusCode.BadRequest, invalidUpdateResponse.StatusCode);

        var storedCourses = await DbContext.Courses
            .Find(FilterDefinition<Course>.Empty)
            .ToListAsync();

        Assert.AreEqual(2, storedCourses.Count);

        var activeCourse = storedCourses.Single(item => item.Id == scenario.ActiveCourse.Id);
        Assert.AreEqual("Active Integration Course", activeCourse.Name);
        Assert.AreEqual("Active", activeCourse.Status);
        Assert.AreEqual("Easy", activeCourse.ExamMatrix!.Single().Difficulty);
    }

    private sealed class PagedCourses
    {
        public List<CourseDto> Items { get; set; } = new();
        public long Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public long TotalPages { get; set; }
    }
}
