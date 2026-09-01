using AutoMapper;
using ChatApp.Application.Common.Interfaces;
using ChatApp.Application.DTOs;
using ChatApp.Domain.Entities;
using ChatApp.Domain.Exceptions;
using ChatApp.Domain.Interfaces;
using ChatApp.Domain.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace ChatApp.Application.Features.Rooms.Commands;

/// <summary>
/// [CQRS - Command] Join room hoặc tạo room nếu chưa tồn tại.
/// Sau khi join → add member vào room → dispatch UserJoinedRoomEvent.
/// Event handler sẽ notify các user khác trong room qua SignalR.
/// </summary>
public record JoinRoomCommand(
    string Username,
    int AvatarIndex,
    string RoomName
) : IRequest<RoomDto>;

public class JoinRoomValidator : AbstractValidator<JoinRoomCommand>
{
    public JoinRoomValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(2);
        RuleFor(x => x.RoomName).NotEmpty().MinimumLength(1);
        RuleFor(x => x.AvatarIndex).InclusiveBetween(0, 5);
    }
}

public class JoinRoomCommandHandler(
    IRoomRepository roomRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    IChatNotifier chatNotifier,
    IMapper mapper
) : IRequestHandler<JoinRoomCommand, RoomDto>
{
    public async Task<RoomDto> Handle(JoinRoomCommand request, CancellationToken ct)
    {
        // Tìm hoặc tạo user
        var user = await userRepository.GetByUsernameAsync(request.Username, ct);
        if (user is null)
        {
            user = User.Create(request.Username, request.AvatarIndex);
            await userRepository.AddAsync(user, ct);
        }
        else
        {
            user.UpdateAvatar(request.AvatarIndex);
        }

        // Đánh dấu user online
        user.GoOnline();

        // Tìm hoặc tạo room
        var room = await roomRepository.GetByNameAsync(request.RoomName, ct);
        if (room is null)
        {
            room = Room.Create(request.RoomName, user.Id, $"#{request.RoomName} channel");
            await roomRepository.AddAsync(room, ct);
        }

        // [Domain Method] AddMember idempotent - không throw nếu đã là member
        room.AddMember(user.Id);

        await unitOfWork.SaveChangesAndDispatchEventsAsync(ct);

        // Refresh online users cache
        await cacheService.RemoveAsync(CacheKeys.OnlineUsers(), ct);

        // Notify room members về user mới vào (qua SignalR)
        var userDto = mapper.Map<UserDto>(user);
        await chatNotifier.NotifyUserJoined(room.Id, userDto, ct);

        // Gửi danh sách online users hiện tại về room
        var onlineUsers = await userRepository.GetOnlineUsersAsync(ct);
        await chatNotifier.NotifyOnlineUsers(room.Id, mapper.Map<IEnumerable<UserDto>>(onlineUsers), ct);

        return mapper.Map<RoomDto>(room);
    }
}
