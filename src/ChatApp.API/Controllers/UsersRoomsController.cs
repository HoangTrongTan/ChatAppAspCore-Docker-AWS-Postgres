using AutoMapper;
using ChatApp.Application.Common.Interfaces;
using ChatApp.Application.DTOs;
using ChatApp.Application.Features.Users.Commands;
using ChatApp.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController(
    ISender mediator,
    IUserRepository userRepository,
    ICacheService cacheService,
    IMapper mapper
) : ControllerBase
{
    /// <summary>
    /// POST /api/users - Tạo hoặc lấy user (idempotent).
    /// FE gọi khi login → tạo user nếu chưa có.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrGetUser(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var command = new CreateOrGetUserCommand(request.Username, request.AvatarIndex);
        var userDto = await mediator.Send(command, ct);
        return Ok(ApiResponse<UserDto>.Ok(userDto));
    }

    /// <summary>
    /// GET /api/users/online - Danh sách user online.
    /// Dùng Redis cache → fast response.
    /// </summary>
    [HttpGet("online")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOnlineUsers(CancellationToken ct)
    {
        var users = await cacheService.GetOrSetAsync(
            CacheKeys.OnlineUsers(),
            async () =>
            {
                var onlineUsers = await userRepository.GetOnlineUsersAsync(ct);
                return mapper.Map<IEnumerable<UserDto>>(onlineUsers);
            },
            expiry: TimeSpan.FromSeconds(10), // Short TTL cho presence
            ct: ct);

        return Ok(ApiResponse<IEnumerable<UserDto>>.Ok(users));
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RoomsController(IRoomRepository roomRepository, IMapper mapper) : ControllerBase
{
    /// <summary>
    /// GET /api/rooms - Danh sách public rooms.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<RoomDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicRooms(CancellationToken ct)
    {
        var rooms = await roomRepository.GetPublicRoomsAsync(ct);
        var dtos = mapper.Map<IEnumerable<RoomDto>>(rooms);
        return Ok(ApiResponse<IEnumerable<RoomDto>>.Ok(dtos));
    }
}
