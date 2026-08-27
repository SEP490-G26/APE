using System.Runtime.ExceptionServices;
using System.Text.Json;
using Application.Common;
using Application.Exceptions;

namespace API.Middlewares;

public sealed class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request was cancelled by the client. TraceId={TraceId}",
                context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(
        HttpContext context,
        Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                exception,
                "Cannot write error response because the response has already started. TraceId={TraceId}",
                context.TraceIdentifier);

            ExceptionDispatchInfo.Capture(exception).Throw();
            return;
        }

        var traceId = context.TraceIdentifier;
        var error = MapException(exception);

        if (error.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception. StatusCode={StatusCode}, TraceId={TraceId}",
                error.StatusCode,
                traceId);
        }
        else
        {
            _logger.LogWarning(
                "Handled exception {ExceptionType}. StatusCode={StatusCode}, TraceId={TraceId}, Message={Message}",
                exception.GetType().Name,
                error.StatusCode,
                traceId,
                exception.Message);
        }

        context.Response.Clear();
        context.Response.StatusCode = error.StatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = ApiResponse.Fail(
            error.PublicMessage,
            traceId);

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            JsonOptions,
            context.RequestAborted);
    }

    private static ErrorMapping MapException(Exception exception)
    {
        return exception switch
        {
            UnauthorizedException => new ErrorMapping(
                StatusCodes.Status401Unauthorized,
                exception.Message),

            UnauthorizedAccessException => new ErrorMapping(
                StatusCodes.Status401Unauthorized,
                exception.Message),

            ForbiddenException => new ErrorMapping(
                StatusCodes.Status403Forbidden,
                exception.Message),

            NotFoundException => new ErrorMapping(
                StatusCodes.Status404NotFound,
                exception.Message),

            KeyNotFoundException => new ErrorMapping(
                StatusCodes.Status404NotFound,
                exception.Message),

            ValidationException => new ErrorMapping(
                StatusCodes.Status400BadRequest,
                exception.Message),

            ArgumentException => new ErrorMapping(
                StatusCodes.Status400BadRequest,
                exception.Message),

            ConflictException => new ErrorMapping(
                StatusCodes.Status409Conflict,
                exception.Message),

            AIProviderException providerException =>
                ResolveAiProviderFailure(providerException),

            InvalidOperationException => new ErrorMapping(
                StatusCodes.Status400BadRequest,
                exception.Message),

            _ => new ErrorMapping(
                StatusCodes.Status500InternalServerError,
                "A system error occurred. Please try again.")
        };
    }

    private static ErrorMapping ResolveAiProviderFailure(
        AIProviderException exception)
    {
        var statusCode = exception.StatusCode switch
        {
            400 => StatusCodes.Status400BadRequest,
            408 => StatusCodes.Status504GatewayTimeout,
            429 => StatusCodes.Status429TooManyRequests,
            401 or 403 or 404 => StatusCodes.Status502BadGateway,
            >= 500 => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status502BadGateway
        };

        var publicMessage = exception.StatusCode switch
        {
            401 or 403 =>
                "The AI service rejected authentication. Please check the provider API key and access settings.",

            404 =>
                "The AI service could not find the configured model or endpoint.",

            408 =>
                "The AI service took too long to respond. Please try again.",

            429 =>
                "The AI service is currently rate-limited. Please try again later.",

            >= 500 =>
                "The AI provider is currently unavailable. Please try again later.",

            _ when !string.IsNullOrWhiteSpace(exception.Message) =>
                exception.Message,

            _ =>
                "Unable to process the AI request right now."
        };

        return new ErrorMapping(
            statusCode,
            publicMessage);
    }

    private sealed record ErrorMapping(
        int StatusCode,
        string PublicMessage);
}
