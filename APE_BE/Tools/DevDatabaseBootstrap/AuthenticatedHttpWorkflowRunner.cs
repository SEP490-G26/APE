using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Auth;
using Infrastructure.Data;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace DevDatabaseBootstrap;

public static class AuthenticatedHttpWorkflowRunner
{
    public const string DefaultApiBaseUrl = "http://localhost:5292";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    static AuthenticatedHttpWorkflowRunner()
    {
        JsonOptions.Converters.Add(
            new JsonStringEnumConverter());
    }

    public static JavaHttpSubmissionPayload CreateJavaSubmissionPayload()
    {
        return CreateJavaSubmissionPayload(
            BootstrapManifest.CreateJavaWorkerSubmissionRequest());
    }

    public static JavaHttpSubmissionPayload CreateJavaCompilationErrorSubmissionPayload()
    {
        return CreateJavaSubmissionPayload(
            BootstrapManifest.CreateJavaCompilationErrorSubmissionRequest());
    }

    public static JavaHttpSubmissionPayload CreateJavaWrongAnswerSubmissionPayload()
    {
        return CreateJavaSubmissionPayload(
            BootstrapManifest.CreateJavaWrongAnswerSubmissionRequest());
    }

    public static JavaHttpSubmissionPayload CreateJavaRuntimeErrorSubmissionPayload()
    {
        return CreateJavaSubmissionPayload(
            BootstrapManifest.CreateJavaRuntimeErrorSubmissionRequest());
    }

    public static JavaHttpSubmissionPayload CreateJavaTimeLimitSubmissionPayload()
    {
        return CreateJavaSubmissionPayload(
            BootstrapManifest.CreateJavaTimeLimitSubmissionRequest());
    }

    public static JavaHttpSubmissionPayload CreateJavaSubmissionPayload(
        PESubmissionInputDto request)
    {
        return new JavaHttpSubmissionPayload(
            request.SessionId,
            request.QuestionId,
            request.RequestedLanguageId,
            request.Files
                .Select(file => new JavaHttpCodeFilePayload(
                    file.Filename,
                    file.Content))
                .ToList());
    }

    public static string SerializeJavaSubmissionPayload()
    {
        return JsonSerializer.Serialize(
            CreateJavaSubmissionPayload(),
            JsonOptions);
    }

    public static string SerializeJavaCompilationErrorSubmissionPayload()
    {
        return JsonSerializer.Serialize(
            CreateJavaCompilationErrorSubmissionPayload(),
            JsonOptions);
    }

    public static string SerializeJavaWrongAnswerSubmissionPayload()
    {
        return JsonSerializer.Serialize(
            CreateJavaWrongAnswerSubmissionPayload(),
            JsonOptions);
    }

    public static string SerializeJavaRuntimeErrorSubmissionPayload()
    {
        return JsonSerializer.Serialize(
            CreateJavaRuntimeErrorSubmissionPayload(),
            JsonOptions);
    }

    public static string SerializeJavaTimeLimitSubmissionPayload()
    {
        return JsonSerializer.Serialize(
            CreateJavaTimeLimitSubmissionPayload(),
            JsonOptions);
    }

    public static SafeAccessTokenReceipt CreateSafeAccessTokenReceipt(
        string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        var studentId =
            jwt.Claims.FirstOrDefault(
                claim => claim.Type == ClaimTypes.NameIdentifier)
            ?.Value;

        var role =
            jwt.Claims.FirstOrDefault(
                claim => claim.Type == ClaimTypes.Role)
            ?.Value;

        var lifetimePresent =
            jwt.Claims.Any(claim =>
                string.Equals(
                    claim.Type,
                    JwtRegisteredClaimNames.Exp,
                    StringComparison.Ordinal));

        return new SafeAccessTokenReceipt(
            HasAccessToken: true,
            TokenLength: accessToken.Length,
            StudentId: studentId,
            Role: role,
            LifetimePresent: lifetimePresent);
    }

    public static SingleApiProcessSelection SelectSingleApiProcess(
        IReadOnlyCollection<ApiProcessMetadata> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);

        if (processes.Count == 0)
        {
            return new SingleApiProcessSelection(
                false,
                null,
                "No APE API process is currently running.");
        }

        if (processes.Count > 1)
        {
            return new SingleApiProcessSelection(
                false,
                null,
                $"Expected exactly one APE API process but found {processes.Count}.");
        }

        return new SingleApiProcessSelection(
            true,
            processes.Single(),
            null);
    }

    public static IReadOnlyList<ApiProcessMetadata> DiscoverApiProcesses(
        string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var expectedPrefix =
            Path.Combine(workspaceRoot, "API")
                .TrimEnd(Path.DirectorySeparatorChar);

        return Process.GetProcessesByName("API")
            .Select(process =>
            {
                try
                {
                    var path = process.MainModule?.FileName;

                    if (string.IsNullOrWhiteSpace(path) ||
                        !path.StartsWith(
                            expectedPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    return new ApiProcessMetadata(
                        process.Id,
                        process.ProcessName,
                        path,
                        process.StartTime);
                }
                catch
                {
                    return null;
                }
            })
            .Where(item => item is not null)
            .Cast<ApiProcessMetadata>()
            .OrderBy(item => item.ProcessId)
            .ToList();
    }

    public static async Task WaitForApiPortAsync(
        Uri baseUri,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(baseUri, "/swagger/index.html"));

                using var response = await client.SendAsync(
                    request,
                    cancellationToken);

                return;
            }
            catch
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    cancellationToken);
            }
        }

        throw new TimeoutException(
            $"The API did not become reachable at '{baseUri}'.");
    }

    public static HttpClient CreateHttpClient(
        string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var client = new HttpClient
        {
            BaseAddress = new Uri(DefaultApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        return client;
    }

    internal static async Task<SafeAccessTokenReceipt> IssueFixtureStudentAccessTokenAsync(
        BootstrapConfiguration configuration,
        DbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(context);

        var student = await context.Users
            .Find(user => user.Id == BootstrapManifest.StudentUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            throw new InvalidOperationException(
                $"Fixture student '{BootstrapManifest.StudentUserId}' was not found.");
        }

        if (!string.Equals(
                student.Status,
                "Active",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Fixture student '{student.Id}' is not Active.");
        }

        using var provider = BuildTokenServiceProvider(configuration);

        var tokenGenerator =
            provider.GetRequiredService<IJwtTokenGenerator>();

        var accessToken =
            tokenGenerator.GenerateAccessToken(student);

        return CreateSafeAccessTokenReceipt(accessToken) with
        {
            AccessToken = accessToken
        };
    }

    public static async Task<ApiResponse<UserProfileDto>?> ReadProfileAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "/api/student/profile",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return DeserializeProfileResponse(body);
    }

    public static async Task<HttpSubmissionCreationResult> CreateSubmissionAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        return await CreateSubmissionAsync(
            httpClient,
            SerializeJavaSubmissionPayload(),
            cancellationToken);
    }

    public static async Task<HttpSubmissionCreationResult> CreateSubmissionAsync(
        HttpClient httpClient,
        string payload,
        CancellationToken cancellationToken)
    {
        using var content =
            new StringContent(
                payload,
                Encoding.UTF8,
                "application/json");

        using var response = await httpClient.PostAsync(
            "/api/student/submissions/pe",
            content,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(
            cancellationToken);

        ApiResponse<SubmissionAcceptedDto>? apiResponse = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            apiResponse = JsonSerializer.Deserialize<
                ApiResponse<SubmissionAcceptedDto>>(
                body,
                JsonOptions);
        }

        return new HttpSubmissionCreationResult(
            response.StatusCode,
            apiResponse,
            body.Length);
    }

    public static async Task<HttpSubmissionPollResult> GetSubmissionAsync(
        HttpClient httpClient,
        string submissionId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"/api/student/submissions/pe/{submissionId}",
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(
            cancellationToken);

        ApiResponse<PESubmissionDetailDto>? apiResponse = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            apiResponse = JsonSerializer.Deserialize<
                ApiResponse<PESubmissionDetailDto>>(
                body,
                JsonOptions);
        }

        return new HttpSubmissionPollResult(
            response.StatusCode,
            apiResponse,
            body.Length);
    }

    public static async Task<HttpStatusCode> GetResultWithoutTokenAsync(
        string submissionId,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(DefaultApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        using var response = await client.GetAsync(
            $"/api/student/submissions/pe/{submissionId}",
            cancellationToken);

        return response.StatusCode;
    }

    public static async Task<HttpStatusCode> GetResultWithMalformedTokenAsync(
        string submissionId,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(DefaultApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "malformed.token.value");

        using var response = await client.GetAsync(
            $"/api/student/submissions/pe/{submissionId}",
            cancellationToken);

        return response.StatusCode;
    }

    private static ServiceProvider BuildTokenServiceProvider(
        BootstrapConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddSingleton(configuration.RootConfiguration);
        services.Configure<JwtSettings>(
            configuration.RootConfiguration.GetSection(
                JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton(new DbContext(
            configuration.ConnectionString,
            configuration.DatabaseName));

        return services.BuildServiceProvider();
    }

    public static ApiResponse<UserProfileDto>? DeserializeProfileResponse(
        string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            (root.TryGetProperty("data", out _) ||
             root.TryGetProperty("success", out _)))
        {
            return JsonSerializer.Deserialize<
                ApiResponse<UserProfileDto>>(
                body,
                JsonOptions);
        }

        var profile = JsonSerializer.Deserialize<UserProfileDto>(
            body,
            JsonOptions);

        return profile is null
            ? null
            : ApiResponse.Ok(profile);
    }
}

public sealed record JavaHttpSubmissionPayload(
    string SessionId,
    string QuestionId,
    int? RequestedLanguageId,
    IReadOnlyList<JavaHttpCodeFilePayload> Files);

public sealed record JavaHttpCodeFilePayload(
    string Filename,
    string Content);

public sealed record ApiProcessMetadata(
    int ProcessId,
    string ProcessName,
    string ExecutablePath,
    DateTime StartTime);

public sealed record SingleApiProcessSelection(
    bool IsSuccess,
    ApiProcessMetadata? Process,
    string? Error);

public sealed record SafeAccessTokenReceipt(
    bool HasAccessToken,
    int TokenLength,
    string? StudentId,
    string? Role,
    bool LifetimePresent)
{
    public string? AccessToken { get; init; }
}

public sealed record HttpSubmissionCreationResult(
    HttpStatusCode StatusCode,
    ApiResponse<SubmissionAcceptedDto>? Response,
    int ResponseBodyLength);

public sealed record HttpSubmissionPollResult(
    HttpStatusCode StatusCode,
    ApiResponse<PESubmissionDetailDto>? Response,
    int ResponseBodyLength);
