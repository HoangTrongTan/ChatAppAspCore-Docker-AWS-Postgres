using System.Net;
using System.Text.Json;
using ChatApp.Application.DTOs;
using ChatApp.Domain.Exceptions;
using FluentValidation;

namespace ChatApp.API.Middleware;

/// <summary>
/// [Middleware - Exception Handler]
/// Global exception handler: bắt tất cả unhandled exceptions → format thành HTTP response nhất quán.
///
/// [Design Pattern - Chain of Responsibility] Middleware pipeline:
/// Request → [ExceptionMiddleware] → [RequestLogging] → [Auth] → Controller
///
/// [SOLID - Single Responsibility] Mỗi middleware chỉ làm 1 việc.
/// Không bao giờ xử lý exception trong từng controller.
///
/// Mapping:
/// DomainException          → 400 Bad Request
/// ValidationException      → 422 Unprocessable Entity
/// NotFoundException        → 404 Not Found
/// UnauthorizedAccessException → 401 Unauthorized
/// Exception (unexpected)   → 500 Internal Server Error
/// </summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.UnprocessableEntity,
                "Validation failed",
                ve.Errors.Select(e => e.ErrorMessage)
            ),
            DomainException de => (
                HttpStatusCode.BadRequest,
                de.Message,
                Enumerable.Empty<string>()
            ),
            NotFoundException nfe => (
                HttpStatusCode.NotFound,
                nfe.Message,
                Enumerable.Empty<string>()
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized access",
                Enumerable.Empty<string>()
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred",
                Enumerable.Empty<string>()
            )
        };

        // [Security] Không expose stack trace trong production
        if (statusCode == HttpStatusCode.InternalServerError)
            logger.LogError(exception, "Unhandled exception for request {Path}", context.Request.Path);
        else
            logger.LogWarning("Handled exception: {Message}", exception.Message);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Fail(message, errors);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}

/// <summary>
/// [Middleware - Request Logging] Structured logging mọi request/response.
/// Kết hợp với Serilog → dễ search trên Kibana/Grafana.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].ToString()
                           ?? Guid.NewGuid().ToString();

        // [Correlation ID] Trace request qua nhiều services
        context.Response.Headers.Append("X-Correlation-Id", correlationId);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        logger.LogInformation(
            "[{CorrelationId}] {Method} {Path} started",
            correlationId, context.Request.Method, context.Request.Path);

        await next(context);

        sw.Stop();

        logger.LogInformation(
            "[{CorrelationId}] {Method} {Path} → {StatusCode} in {ElapsedMs}ms",
            correlationId, context.Request.Method, context.Request.Path,
            context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}
