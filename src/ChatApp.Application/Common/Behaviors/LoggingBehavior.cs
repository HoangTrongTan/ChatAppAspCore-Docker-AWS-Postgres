using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ChatApp.Application.Common.Behaviors;

/// <summary>
/// [Design Pattern - Decorator / Pipeline]
/// MediatR Pipeline Behavior: tự động log mọi request/response.
/// Không cần thêm log code vào từng handler → Open/Closed Principle.
///
/// Pipeline: Request → LoggingBehavior → ValidationBehavior → Handler → Response
///
/// [SOLID - Open/Closed] Thêm behavior mà không sửa handler.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation("[START] Handling {RequestName}: {@Request}", requestName, request);

        try
        {
            var response = await next();
            sw.Stop();

            logger.LogInformation(
                "[END] {RequestName} handled in {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);

            // [Performance Monitoring] Warning nếu chậm > 500ms
            if (sw.ElapsedMilliseconds > 500)
                logger.LogWarning(
                    "[SLOW] {RequestName} took {ElapsedMs}ms — consider caching or optimization!",
                    requestName, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[ERROR] {RequestName} failed after {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
