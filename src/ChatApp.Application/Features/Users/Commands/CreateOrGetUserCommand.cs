using AutoMapper;
using ChatApp.Application.Common.Interfaces;
using ChatApp.Application.DTOs;
using ChatApp.Domain.Entities;
using ChatApp.Domain.Interfaces;
using ChatApp.Domain.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace ChatApp.Application.Features.Users.Commands;

/// <summary>
/// [CQRS - Command] Tạo user mới hoặc lấy user cũ nếu đã tồn tại.
/// Idempotent: gọi nhiều lần với cùng username → kết quả như nhau.
/// </summary>
public record CreateOrGetUserCommand(
    string Username,
    int AvatarIndex
) : IRequest<UserDto>;

public class CreateOrGetUserValidator : AbstractValidator<CreateOrGetUserCommand>
{
    public CreateOrGetUserValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(2).WithMessage("Username must be at least 2 characters.")
            .MaximumLength(30);

        RuleFor(x => x.AvatarIndex)
            .InclusiveBetween(0, 5).WithMessage("Avatar index must be 0-5.");
    }
}

public class CreateOrGetUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    IMapper mapper
) : IRequestHandler<CreateOrGetUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateOrGetUserCommand request, CancellationToken ct)
    {
        // [Idempotent] Tìm user đã tồn tại trước
        var existing = await userRepository.GetByUsernameAsync(request.Username, ct);
        if (existing is not null)
        {
            // Update avatar nếu khác → không tạo mới
            existing.UpdateAvatar(request.AvatarIndex);
            await unitOfWork.SaveChangesAsync(ct);

            // Refresh cache
            await cacheService.SetAsync(
                CacheKeys.UserByUsername(request.Username),
                existing,
                TimeSpan.FromMinutes(10), ct);

            return mapper.Map<UserDto>(existing);
        }

        // [Domain Factory] Tạo user mới qua factory method
        var user = User.Create(request.Username, request.AvatarIndex);
        await userRepository.AddAsync(user, ct);

        // [Unit of Work + Domain Events] UserCreatedEvent sẽ được dispatch
        await unitOfWork.SaveChangesAndDispatchEventsAsync(ct);

        // Cache user mới
        await cacheService.SetAsync(
            CacheKeys.UserByUsername(user.Username),
            user,
            TimeSpan.FromMinutes(10), ct);

        return mapper.Map<UserDto>(user);
    }
}
