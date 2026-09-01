using FluentValidation;
using MediatR;

namespace ChatApp.Application.Common.Behaviors;

/// <summary>
/// [Design Pattern - Decorator via MediatR Pipeline]
/// ValidationBehavior tự động validate mọi request có validator tương ứng.
/// Không cần gọi validator thủ công trong từng handler.
///
/// FluentValidation: xác thực dữ liệu đầu vào trước khi vào business logic.
/// Nếu validation fail → throw ValidationException → ExceptionMiddleware bắt → HTTP 400.
///
/// [SOLID - Single Responsibility] Handler chỉ lo business logic,
/// không phải lo validation.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next(); // Không có validator → skip

        var context = new ValidationContext<TRequest>(request);

        // Chạy tất cả validators song song → tổng hợp lỗi
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            // [FluentValidation] Throw exception với danh sách lỗi chi tiết
            // ExceptionMiddleware sẽ map thành HTTP 400 với errors array
            throw new ValidationException(failures);
        }

        return await next();
    }
}
