using System.Net;
using Application.Models;

namespace Application.Exceptions;

public sealed class CodeExecutionClientException : Exception
{
    [Obsolete(
        "Use the provider-neutral constructor with RetryAfter, " +
        "ErrorCategory, and ProviderName.")]
    public CodeExecutionClientException(
        string safeMessage,
        bool isTransient,
        HttpStatusCode? statusCode,
        Exception? innerException = null)
        : this(
            safeMessage,
            isTransient,
            retryAfter: null,
            errorCategory: statusCode switch
            {
                HttpStatusCode.RequestTimeout => CodeExecutionErrorCategory.Timeout,
                HttpStatusCode.TooManyRequests => CodeExecutionErrorCategory.RateLimit,
                HttpStatusCode.Unauthorized => CodeExecutionErrorCategory.Authentication,
                HttpStatusCode.Forbidden => CodeExecutionErrorCategory.Authorization,
                HttpStatusCode.NotFound => CodeExecutionErrorCategory.NotFound,
                HttpStatusCode.BadRequest => CodeExecutionErrorCategory.Validation,
                _ => CodeExecutionErrorCategory.Provider
            },
            providerName: null,
            statusCode: statusCode,
            innerException: innerException)
    {
    }

    public CodeExecutionClientException(
        string safeMessage,
        bool isTransient,
        TimeSpan? retryAfter = null,
        CodeExecutionErrorCategory errorCategory =
            CodeExecutionErrorCategory.Unknown,
        string? providerName = null,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        IsTransient = isTransient;
        RetryAfter = retryAfter;
        ErrorCategory = errorCategory;
        ProviderName = string.IsNullOrWhiteSpace(providerName)
            ? null
            : providerName.Trim();
        SafeMessage = safeMessage;
        StatusCode = statusCode;
    }

    public bool IsTransient { get; }

    public TimeSpan? RetryAfter { get; }

    public CodeExecutionErrorCategory ErrorCategory { get; }

    public string? ProviderName { get; }

    public string SafeMessage { get; }

    public HttpStatusCode? StatusCode { get; }
}
