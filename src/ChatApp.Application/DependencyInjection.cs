using AutoMapper;
using ChatApp.Application.Common.Behaviors;
using ChatApp.Application.Common.Mappings;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ChatApp.Application;

/// <summary>
/// [Design Pattern - Extension Method] Clean DI registration.
/// Program.cs chỉ cần gọi builder.Services.AddApplication() → gọn gàng.
///
/// [SOLID - Open/Closed] Thêm service mới → chỉ sửa file này, không sửa Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // [MediatR] Scan assembly → tự tìm tất cả IRequestHandler, INotificationHandler
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // [Pipeline Behaviors] Thứ tự quan trọng: Logging → Validation → Handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // [FluentValidation] Scan assembly → tự tìm tất cả AbstractValidator<T>
        services.AddValidatorsFromAssembly(assembly);

        // [AutoMapper] Scan assembly → tìm tất cả Profile
        services.AddAutoMapper(assembly);

        return services;
    }
}
