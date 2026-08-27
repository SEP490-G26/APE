using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Exceptions;
using Application.Interfaces;
using Application.Models;
using Infrastructure.Judge0.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Judge0;

internal sealed class Judge0Client : ICodeExecutionClient
{
    private const string ProviderName = "Judge0";
    private const string CreateBatchOperation = "CreateBatch";
    private const string PollBatchOperation = "PollBatch";
    private const string UnknownRequestTarget = "unknown";
    private const int MaxCorrelationIdLength = 128;
    private const string ResultFields =
        "token,status_id,status,stdout,stderr," +
        "compile_output,message,time,memory";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull
        };

    private readonly HttpClient _httpClient;
    private readonly Judge0Options _options;
    private readonly IJudge0SourcePackageBuilder _packageBuilder;
    private readonly ILogger<Judge0Client> _logger;

    public Judge0Client(
        HttpClient httpClient,
        IOptions<Judge0Options> options,
        IJudge0SourcePackageBuilder packageBuilder,
        ILogger<Judge0Client> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _packageBuilder = packageBuilder;
        _logger = logger;
    }

    public async Task<CodeExecutionBatchReceipt> CreateBatchAsync(
        CodeExecutionBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cases.Count > _options.MaximumBatchSize)
        {
            throw new CodeExecutionClientException(
                safeMessage:
                    $"The batch contains {request.Cases.Count} test cases. " +
                    $"The maximum allowed is {_options.MaximumBatchSize}.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Validation,
                providerName: ProviderName);
        }

        var sourcePackage = _packageBuilder.Build(
            request.LanguageId,
            request.SourceFiles);

        var orderedCases = request.Cases
            .OrderBy(item => item.CaseIndex)
            .ToList();

        var submissions = orderedCases
            .Select(executionCase => new Judge0SubmissionRequest
            {
                LanguageId = sourcePackage.Judge0LanguageId,
                SourceCode = sourcePackage.SourceCodeBase64,
                AdditionalFiles =
                    sourcePackage.AdditionalFilesBase64,
                StandardInput = EncodeBase64(executionCase.StandardInput),
                CpuTimeLimitSeconds =
                    ConvertMillisecondsToSeconds(
                        executionCase.TimeLimitMs),
                WallTimeLimitSeconds =
                    CalculateWallTimeLimitSeconds(
                        executionCase.TimeLimitMs),
                MemoryLimitKb = executionCase.MemoryLimitKb
            })
            .ToList();

        var payload = new Judge0CreateBatchRequest
        {
            Submissions = submissions
        };

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            "submissions/batch?base64_encoded=true")
        {
            Content = JsonContent.Create(
                payload,
                options: JsonOptions)
        };

        using var response = await SendAsync(
            requestMessage,
            cancellationToken);

        var results = await DeserializeAsync<
            List<Judge0CreateSubmissionResponse>>(
            response,
            cancellationToken);

        if (results.Count != submissions.Count)
        {
            throw new CodeExecutionClientException(
                safeMessage:
                    $"Judge0 returned {results.Count} batch items for " +
                    $"{submissions.Count} submissions.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Provider,
                providerName: ProviderName,
                statusCode: response.StatusCode);
        }

        var tokens = new List<CodeExecutionToken>(results.Count);
        var seenProviderTokens = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var executionCase = orderedCases[index];

            if (result.ValidationErrors is { Count: > 0 })
            {
                LogMalformedResponse(
                    CreateBatchOperation,
                    response.StatusCode,
                    response.Content?.Headers.ContentLength,
                    GetSafeCorrelationId(response),
                    "Judge0 rejected a create-batch item.",
                    validationErrorCount: result.ValidationErrors.Count,
                    caseIndex: executionCase.CaseIndex);

                throw new CodeExecutionClientException(
                    safeMessage:
                        $"Judge0 rejected execution case index {executionCase.CaseIndex}.",
                    isTransient: false,
                    errorCategory: CodeExecutionErrorCategory.Validation,
                    providerName: ProviderName,
                    statusCode: response.StatusCode);
            }

            if (string.IsNullOrWhiteSpace(result.Token))
            {
                throw new CodeExecutionClientException(
                    safeMessage:
                        $"Judge0 returned an empty token for case index {executionCase.CaseIndex}.",
                    isTransient: false,
                    errorCategory: CodeExecutionErrorCategory.Provider,
                    providerName: ProviderName,
                    statusCode: response.StatusCode);
            }

            var normalizedToken = result.Token.Trim();

            if (!seenProviderTokens.Add(normalizedToken))
            {
                LogMalformedResponse(
                    CreateBatchOperation,
                    response.StatusCode,
                    response.Content?.Headers.ContentLength,
                    GetSafeCorrelationId(response),
                    "Judge0 returned duplicate create-batch tokens.",
                    duplicateTokenCount: 1);

                throw new CodeExecutionClientException(
                    safeMessage: "Judge0 returned duplicate execution tokens.",
                    isTransient: false,
                    errorCategory: CodeExecutionErrorCategory.Provider,
                    providerName: ProviderName,
                    statusCode: response.StatusCode);
            }

            tokens.Add(new CodeExecutionToken(
                executionCase.CaseIndex,
                normalizedToken));
        }

        return new CodeExecutionBatchReceipt(
            ProviderName,
            tokens);
    }

    public async Task<IReadOnlyList<CodeExecutionResult>>
        GetBatchResultsAsync(
            IReadOnlyCollection<string> tokens,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var normalizedTokens = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToList();

        if (normalizedTokens.Count == 0)
        {
            throw new CodeExecutionClientException(
                safeMessage: "At least one execution token is required.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Validation,
                providerName: ProviderName);
        }

        if (normalizedTokens.Count != tokens.Count)
        {
            throw new CodeExecutionClientException(
                safeMessage: "Execution tokens cannot contain empty values.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Validation,
                providerName: ProviderName);
        }

        if (normalizedTokens
            .Distinct(StringComparer.Ordinal)
            .Count() != normalizedTokens.Count)
        {
            throw new CodeExecutionClientException(
                safeMessage: "Execution tokens must be unique.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Validation,
                providerName: ProviderName);
        }

        var tokenParameter = Uri.EscapeDataString(
            string.Join(',', normalizedTokens));

        var fieldsParameter = Uri.EscapeDataString(
            ResultFields);

        var requestUri =
            $"submissions/batch" +
            $"?tokens={tokenParameter}" +
            $"&base64_encoded=true" +
            $"&fields={fieldsParameter}";

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            requestUri);

        using var response = await SendAsync(
            requestMessage,
            cancellationToken);

        var result = await DeserializeAsync<
            Judge0BatchResultResponse>(
            response,
            cancellationToken);

        if (result.Submissions.Count != normalizedTokens.Count)
        {
            LogMalformedResponse(
                PollBatchOperation,
                response.StatusCode,
                response.Content?.Headers.ContentLength,
                GetSafeCorrelationId(response),
                "Judge0 returned an unexpected polling result count.",
                expectedCount: normalizedTokens.Count,
                actualCount: result.Submissions.Count);

            throw new CodeExecutionClientException(
                safeMessage:
                    $"Judge0 returned {result.Submissions.Count} results " +
                    $"for {normalizedTokens.Count} tokens.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Provider,
                providerName: ProviderName,
                statusCode: response.StatusCode);
        }

        var responseByToken =
            new Dictionary<string, Judge0SubmissionResultResponse>(
                StringComparer.Ordinal);

        foreach (var submission in result.Submissions)
        {
            if (string.IsNullOrWhiteSpace(submission.Token))
            {
                throw new CodeExecutionClientException(
                    safeMessage:
                        "Judge0 returned a polling result without a token.",
                    isTransient: false,
                    errorCategory: CodeExecutionErrorCategory.Provider,
                    providerName: ProviderName,
                    statusCode: response.StatusCode);
            }

            var normalizedToken = submission.Token.Trim();

            if (!responseByToken.TryAdd(normalizedToken, submission))
            {
                LogMalformedResponse(
                    PollBatchOperation,
                    response.StatusCode,
                    response.Content?.Headers.ContentLength,
                    GetSafeCorrelationId(response),
                    "Judge0 returned duplicate polling tokens.",
                    duplicateTokenCount: 1);

                throw new CodeExecutionClientException(
                    safeMessage:
                        "Judge0 returned duplicate polling results.",
                    isTransient: false,
                    errorCategory: CodeExecutionErrorCategory.Provider,
                    providerName: ProviderName,
                    statusCode: response.StatusCode);
            }
        }

        var mappedResults =
            new List<CodeExecutionResult>(
                normalizedTokens.Count);

        var expectedTokens = normalizedTokens.ToHashSet(StringComparer.Ordinal);

        var unexpectedToken = responseByToken.Keys
            .FirstOrDefault(token => !expectedTokens.Contains(token));

        if (unexpectedToken is not null)
        {
            LogMalformedResponse(
                PollBatchOperation,
                response.StatusCode,
                response.Content?.Headers.ContentLength,
                GetSafeCorrelationId(response),
                "Judge0 returned unexpected polling tokens.",
                unexpectedTokenCount: 1,
                expectedCount: normalizedTokens.Count,
                actualCount: responseByToken.Count);

            throw new CodeExecutionClientException(
                safeMessage:
                    "Judge0 returned unexpected polling tokens.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Provider,
                providerName: ProviderName,
                statusCode: response.StatusCode);
        }

        foreach (var token in normalizedTokens)
        {
            if (!responseByToken.TryGetValue(
                    token,
                    out var submission))
            {
                LogMalformedResponse(
                    PollBatchOperation,
                    response.StatusCode,
                    response.Content?.Headers.ContentLength,
                    GetSafeCorrelationId(response),
                    "Judge0 omitted polling tokens from the response.",
                    missingTokenCount: 1,
                    expectedCount: normalizedTokens.Count,
                    actualCount: responseByToken.Count);

                throw new CodeExecutionClientException(
                    safeMessage:
                        "Judge0 did not return all requested polling results.",
                    isTransient: true,
                    errorCategory: CodeExecutionErrorCategory.Provider,
                    providerName: ProviderName,
                    statusCode: response.StatusCode);
            }

            var statusId = submission.StatusId != 0
                ? submission.StatusId
                : submission.Status?.Id ?? 0;

            mappedResults.Add(new CodeExecutionResult(
                CaseIndex: -1,
                ProviderToken: token,
                State: MapJudge0Status(
                    statusId,
                    submission.Status?.Description,
                    DecodeNullableBase64(
                        submission.StandardError),
                    DecodeNullableBase64(
                        submission.Message)),
                ProviderStatusDescription:
                    submission.Status?.Description,
                StandardOutput:
                    DecodeNullableBase64(
                        submission.StandardOutput),
                StandardError:
                    DecodeNullableBase64(
                        submission.StandardError),
                CompileOutput:
                    DecodeNullableBase64(
                        submission.CompileOutput),
                Message:
                    DecodeNullableBase64(
                        submission.Message),
                TimeSeconds: submission.TimeSeconds,
                MemoryKb: submission.MemoryKb,
                ProviderStatusCode: statusId.ToString()));
        }

        return mappedResults;
    }

    public async Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "languages");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // Thành công: trả response về cho method gọi.
            // Method gọi sẽ chịu trách nhiệm Dispose response.
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            // Lưu các thông tin cần dùng trước khi Dispose.
            var statusCode = response.StatusCode;

            var responseBody = await response.Content
                .ReadAsStringAsync(cancellationToken);

            var isTransient =
                IsTransientStatusCode(statusCode);
            var retryAfter = ParseRetryAfter(response);
            var correlationId = GetSafeCorrelationId(response);

            _logger.LogWarning(
                "Judge0 request failed. ProviderName: {ProviderName}. " +
                "Method: {Method}. SafeRequestTarget: {SafeRequestTarget}. StatusCode: {StatusCode}. " +
                "ErrorCategory: {ErrorCategory}. IsTransient: {IsTransient}. " +
                "RetryAfter: {RetryAfter}. ResponseBodyLength: {ResponseBodyLength}. " +
                "ProviderCorrelationId: {ProviderCorrelationId}. ElapsedMilliseconds: {ElapsedMilliseconds}.",
                ProviderName,
                request.Method,
                GetSafeRequestTarget(request),
                (int)statusCode,
                ClassifyStatusCode(statusCode),
                isTransient,
                retryAfter,
                responseBody.Length,
                correlationId,
                (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);

            // Không trả response lỗi ra ngoài nên Dispose tại đây.
            response.Dispose();

            throw new CodeExecutionClientException(
                safeMessage: BuildSafeHttpErrorMessage(statusCode),
                isTransient: isTransient,
                retryAfter: retryAfter,
                errorCategory: ClassifyStatusCode(statusCode),
                providerName: ProviderName,
                statusCode: statusCode);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new CodeExecutionClientException(
                safeMessage: "Judge0 request timed out.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Timeout,
                providerName: ProviderName,
                statusCode: HttpStatusCode.RequestTimeout,
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new CodeExecutionClientException(
                safeMessage: "Unable to connect to Judge0.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Network,
                providerName: ProviderName,
                innerException: exception);
        }
    }


    private async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await response.Content
                .ReadFromJsonAsync<T>(
                    JsonOptions,
                    cancellationToken);

            return value ??
                   throw new CodeExecutionClientException(
                       safeMessage: "Judge0 returned an empty response.",
                       isTransient: true,
                       errorCategory: CodeExecutionErrorCategory.Provider,
                       providerName: ProviderName,
                       statusCode: response.StatusCode);
        }
        catch (JsonException exception)
        {
            LogMalformedResponse(
                typeof(T) == typeof(Judge0BatchResultResponse)
                    ? PollBatchOperation
                    : CreateBatchOperation,
                response.StatusCode,
                response.Content?.Headers.ContentLength,
                GetSafeCorrelationId(response),
                "Judge0 returned malformed JSON.");

            throw new CodeExecutionClientException(
                safeMessage: "Judge0 returned an invalid response.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Provider,
                providerName: ProviderName,
                statusCode: response.StatusCode,
                innerException: exception);
        }
    }

    private void LogMalformedResponse(
        string operation,
        HttpStatusCode statusCode,
        long? responseBodyLength,
        string? providerCorrelationId,
        string message,
        int? expectedCount = null,
        int? actualCount = null,
        int? missingTokenCount = null,
        int? duplicateTokenCount = null,
        int? unexpectedTokenCount = null,
        int? validationErrorCount = null,
        int? caseIndex = null)
    {
        _logger.LogWarning(
            "{Message} ProviderName: {ProviderName}. Operation: {Operation}. " +
            "StatusCode: {StatusCode}. ErrorCategory: {ErrorCategory}. " +
            "ResponseBodyLength: {ResponseBodyLength}. ProviderCorrelationId: {ProviderCorrelationId}. " +
            "ExpectedCount: {ExpectedCount}. ActualCount: {ActualCount}. MissingTokenCount: {MissingTokenCount}. " +
            "DuplicateTokenCount: {DuplicateTokenCount}. UnexpectedTokenCount: {UnexpectedTokenCount}. " +
            "ValidationErrorCount: {ValidationErrorCount}. CaseIndex: {CaseIndex}.",
            message,
            ProviderName,
            operation,
            (int)statusCode,
            CodeExecutionErrorCategory.Provider,
            responseBodyLength,
            providerCorrelationId,
            expectedCount,
            actualCount,
            missingTokenCount,
            duplicateTokenCount,
            unexpectedTokenCount,
            validationErrorCount,
            caseIndex);
    }

    private static CodeExecutionState MapJudge0Status(
        int statusId,
        string? statusDescription,
        string? standardError,
        string? message)
    {
        return statusId switch
        {
            1 => CodeExecutionState.Queued,
            2 => CodeExecutionState.Running,
            3 or 4 => CodeExecutionState.ExecutionSucceeded,
            5 => CodeExecutionState.TimedOut,
            6 => CodeExecutionState.CompilationFailed,
            8 => CodeExecutionState.OutputLimitExceeded,
            13 or 14 => CodeExecutionState.ProviderInternalError,
            7 or 9 or 10 or 11 or 12 =>
                ResolveRuntimeLikeState(
                    statusDescription,
                    standardError,
                    message),
            _ => CodeExecutionState.Unknown
        };
    }

    private static CodeExecutionState ResolveRuntimeLikeState(
        string? statusDescription,
        string? standardError,
        string? message)
    {
        var diagnostic = string.Join(
            ' ',
            statusDescription,
            standardError,
            message);

        if (diagnostic.Contains(
                "memory",
                StringComparison.OrdinalIgnoreCase))
        {
            return CodeExecutionState.MemoryLimitExceeded;
        }

        return CodeExecutionState.RuntimeFailed;
    }

    private static bool IsTransientStatusCode(
        HttpStatusCode statusCode)
    {
        return statusCode is
                   HttpStatusCode.RequestTimeout or
                   HttpStatusCode.TooManyRequests or
                   HttpStatusCode.BadGateway or
                   HttpStatusCode.ServiceUnavailable or
                   HttpStatusCode.GatewayTimeout ||
               (int)statusCode >= 500;
    }

    private static CodeExecutionErrorCategory ClassifyStatusCode(
        HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest =>
                CodeExecutionErrorCategory.Validation,
            HttpStatusCode.Unauthorized =>
                CodeExecutionErrorCategory.Authentication,
            HttpStatusCode.Forbidden =>
                CodeExecutionErrorCategory.Authorization,
            HttpStatusCode.NotFound =>
                CodeExecutionErrorCategory.NotFound,
            (HttpStatusCode)422 =>
                CodeExecutionErrorCategory.Validation,
            HttpStatusCode.RequestTimeout =>
                CodeExecutionErrorCategory.Timeout,
            HttpStatusCode.TooManyRequests =>
                CodeExecutionErrorCategory.RateLimit,
            _ when (int)statusCode >= 500 =>
                CodeExecutionErrorCategory.Provider,
            _ => CodeExecutionErrorCategory.Provider
        };
    }

    private static TimeSpan? ParseRetryAfter(
        HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta >= TimeSpan.Zero
                ? delta
                : null;
        }

        if (retryAfter.Date is { } date)
        {
            var duration = date - DateTimeOffset.UtcNow;
            return duration > TimeSpan.Zero
                ? duration
                : null;
        }

        return null;
    }

    private static string BuildSafeHttpErrorMessage(
        HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest =>
                "Judge0 rejected the execution request.",
            HttpStatusCode.Unauthorized =>
                "Judge0 authentication failed.",
            HttpStatusCode.Forbidden =>
                "Judge0 authorization failed.",
            HttpStatusCode.NotFound =>
                "Judge0 endpoint was not found.",
            (HttpStatusCode)422 =>
                "Judge0 rejected the execution request.",
            HttpStatusCode.RequestTimeout =>
                "Judge0 request timed out.",
            HttpStatusCode.TooManyRequests =>
                "Judge0 rate limit was reached.",
            _ when (int)statusCode >= 500 =>
                "Judge0 temporarily failed to process the request.",
            _ =>
                "Judge0 returned an unexpected HTTP status."
        };
    }

    private static string? GetSafeCorrelationId(
        HttpResponseMessage response)
    {
        foreach (var headerName in new[] { "X-Request-Id", "Request-Id", "Trace-Id" })
        {
            if (response.Headers.TryGetValues(headerName, out var values))
            {
                var value = values.FirstOrDefault();
                var sanitizedValue = SanitizeCorrelationId(value);

                if (!string.IsNullOrWhiteSpace(sanitizedValue))
                {
                    return sanitizedValue;
                }
            }
        }

        return null;
    }

    private string GetSafeRequestTarget(
        HttpRequestMessage request)
    {
        try
        {
            if (request.RequestUri is null)
            {
                return UnknownRequestTarget;
            }

            if (request.RequestUri.IsAbsoluteUri)
            {
                return BuildSafeRequestTarget(
                    request.RequestUri);
            }

            if (_httpClient.BaseAddress is { } baseAddress)
            {
                return BuildSafeRequestTarget(
                    new Uri(
                        baseAddress,
                        request.RequestUri));
            }

            var relativeTarget = request.RequestUri.GetComponents(
                UriComponents.Path,
                UriFormat.UriEscaped);

            return string.IsNullOrWhiteSpace(relativeTarget)
                ? UnknownRequestTarget
                : "/" + relativeTarget.TrimStart('/');
        }
        catch (UriFormatException)
        {
            return UnknownRequestTarget;
        }
        catch (InvalidOperationException)
        {
            return UnknownRequestTarget;
        }
    }

    private static string BuildSafeRequestTarget(
        Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped);
    }

    private static string? SanitizeCorrelationId(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        Span<char> buffer = stackalloc char[Math.Min(
            value.Length,
            MaxCorrelationIdLength)];

        var trimmed = value.Trim();
        var count = 0;

        foreach (var character in trimmed)
        {
            if (count >= buffer.Length)
            {
                break;
            }

            if (char.IsControl(character))
            {
                continue;
            }

            buffer[count++] = character;
        }

        if (count == 0)
        {
            return null;
        }

        return new string(buffer[..count]).Trim();
    }

    private static double ConvertMillisecondsToSeconds(
        int milliseconds)
    {
        return Math.Max(
            milliseconds / 1_000d,
            0.001d);
    }

    private static double CalculateWallTimeLimitSeconds(
        int timeLimitMilliseconds)
    {
        var cpuSeconds =
            ConvertMillisecondsToSeconds(
                timeLimitMilliseconds);

        return Math.Max(
            cpuSeconds * 2,
            cpuSeconds + 1);
    }

    private static string EncodeBase64(string value)
    {
        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static string? DecodeNullableBase64(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return Encoding.UTF8.GetString(
                Convert.FromBase64String(value));
        }
        catch (FormatException exception)
        {
            throw new CodeExecutionClientException(
                safeMessage: "Judge0 returned invalid Base64 content.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Provider,
                providerName: ProviderName,
                innerException: exception);
        }
    }
}
