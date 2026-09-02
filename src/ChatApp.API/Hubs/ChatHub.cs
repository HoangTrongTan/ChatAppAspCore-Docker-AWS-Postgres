using AutoMapper;
using ChatApp.API.Hubs.Payloads;
using ChatApp.Application.Common.Interfaces;
using ChatApp.Application.DTOs;
using ChatApp.Application.Features.Messages.Commands.SendMessage;
using ChatApp.Application.Features.Rooms.Commands;
using ChatApp.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.API.Hubs;

/// <summary>
/// [SignalR Hub] Trung tâm real-time communication.
/// Hub tương đương Socket.IO namespace/room.
///
/// [System Design - SignalR Architecture]
/// Client ←→ SignalR Hub ←→ Application Layer (MediatR)
///                ↕
///           SignalR Backplane (Redis pub/sub)
/// → Multiple server instances đều có thể broadcast tới cùng client
///
/// [Client Methods - FE cần implement @microsoft/signalr]
/// - "ReceiveMessage" → nhận message mới
/// - "UserJoined"     → user mới join room
/// - "UserLeft"       → user rời room
/// - "OnlineUsers"    → danh sách user online
/// - "MessageHistory" → lịch sử message khi mới join
///
/// [Hub Methods - FE gọi lên server]
/// - JoinRoom(JoinRoomPayload)      → invoke("JoinRoom", { username, avatarIndex, roomId })
/// - SendMessage(SendMessagePayload) → invoke("SendMessage", { content, roomName })
/// - LeaveRoom(roomName)             → invoke("LeaveRoom", roomName)
///
/// Tương đương FE Angular code:
/// this.connection.invoke("JoinRoom", { username, avatarIndex, roomId })
/// this.connection.invoke("SendMessage", { content, roomName })
/// this.connection.on("ReceiveMessage", (msg) => this.messages.update(...))
/// </summary>
public class ChatHub(
    ISender mediator,
    IMessageRepository messageRepository,
    IUserRepository userRepository,
    IMapper mapper
) : Hub<IChatClient>
{
    /// <summary>
    /// [SignalR Hub Method] Client gọi để join room.
    /// Dùng MediatR để dispatch vào Application layer → clean, testable.
    ///
    /// SignalR Groups = chat rooms (group tự động managed bởi SignalR)
    /// </summary>
    public async Task JoinRoom(JoinRoomPayload payload)
    {
        var command = new JoinRoomCommand(payload.Username, payload.AvatarIndex, payload.RoomId);
        var roomDto = await mediator.Send(command);

        // [SignalR Group] Thêm connection này vào group tên = roomName
        // Mỗi connection = 1 browser tab/window
        await Groups.AddToGroupAsync(Context.ConnectionId, roomDto.Name);

        // Lưu metadata vào connection context (dùng khi disconnect)
        Context.Items["username"] = payload.Username;
        Context.Items["roomName"] = roomDto.Name;
        Context.Items["avatarIndex"] = payload.AvatarIndex;

        // Gửi lịch sử message về cho user mới join (50 messages gần nhất)
        var history = await messageRepository.GetByRoomIdAsync(roomDto.Id, 50, 0);
        var historyDtos = mapper.Map<IEnumerable<MessageDto>>(history);
        await Clients.Caller.ReceiveMessageHistory(historyDtos);
    }

    /// <summary>
    /// [SignalR Hub Method] Client gửi message.
    /// Hub nhận → dispatch CQRS Command → handler save DB + broadcast.
    ///
    /// [Security] Trong production: thêm JWT authentication
    /// [hub] → [attribute] [Authorize] ở class level
    /// </summary>
    public async Task SendMessage(SendMessagePayload payload)
    {
        var username = Context.Items["username"]?.ToString()
            ?? throw new HubException("Not authenticated. Please join a room first.");

        var roomName = !string.IsNullOrWhiteSpace(payload.RoomName)
            ? payload.RoomName
            : Context.Items["roomName"]?.ToString();

        if (string.IsNullOrWhiteSpace(roomName))
        {
            throw new HubException("Room name is required. Please join a room first.");
        }

        var command = new SendMessageCommand(payload.Content, roomName, username);

        // [MediatR] Handler sẽ save DB, Kafka publish, và gọi IChatNotifier
        // IChatNotifier implement là SignalRChatNotifier → gọi lại Hub
        await mediator.Send(command);
    }

    /// <summary>
    /// [SignalR Event] Called automatically khi connection bị đứt.
    /// → Đánh dấu user offline + notify room.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.Items["username"]?.ToString();
        var roomName = Context.Items["roomName"]?.ToString();

        if (username is not null && roomName is not null)
        {
            // Đánh dấu user offline trong DB
            var user = await userRepository.GetByUsernameAsync(username);
            if (user is not null)
            {
                user.GoOffline();
                // Note: UnitOfWork không có ở đây → inject nếu cần save
            }

            // Notify tất cả members trong room
            await Clients.Group(roomName).UserLeft(username);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// [SignalR Typed Hub] Định nghĩa các methods mà SERVER gọi xuống CLIENT.
/// Compile-time type safety → tránh typo method name.
///
/// FE Angular cần implement:
/// connection.on("ReceiveMessage", callback)
/// connection.on("UserJoined", callback)
/// etc.
/// </summary>
public interface IChatClient
{
    /// <summary>Gửi 1 message mới đến tất cả clients trong room</summary>
    Task ReceiveMessage(MessageDto message);

    /// <summary>Gửi lịch sử 50 messages gần nhất khi mới join</summary>
    Task ReceiveMessageHistory(IEnumerable<MessageDto> messages);

    /// <summary>Notify khi có user mới join</summary>
    Task UserJoined(UserDto user);

    /// <summary>Notify khi user rời room</summary>
    Task UserLeft(string username);

    /// <summary>Gửi toàn bộ danh sách online users</summary>
    Task OnlineUsers(IEnumerable<UserDto> users);
}

/// <summary>
/// [Adapter Pattern] SignalRChatNotifier implements IChatNotifier interface.
/// Application layer gọi IChatNotifier (không biết SignalR).
/// Infrastructure layer dùng IHubContext để thực sự gửi qua SignalR.
///
/// Đây là ví dụ điển hình của Dependency Inversion Principle:
/// Application defines interface (IChatNotifier)
/// Infrastructure provides implementation (SignalRChatNotifier)
/// </summary>
public class SignalRChatNotifier(
    IHubContext<ChatHub, IChatClient> hubContext,
    IRoomRepository roomRepository
) : IChatNotifier
{
    public async Task NotifyMessageSent(Guid roomId, MessageDto message, CancellationToken ct)
    {
        // [SignalR Group Broadcast] Gửi đến tất cả clients trong room group
        var room = await roomRepository.GetByIdAsync(roomId, ct);
        if (room is null) return;

        await hubContext.Clients.Group(room.Name).ReceiveMessage(message);
    }

    public async Task NotifyUserJoined(Guid roomId, UserDto user, CancellationToken ct)
    {
        var room = await roomRepository.GetByIdAsync(roomId, ct);
        if (room is null) return;

        // OthersInGroup: tất cả trừ chính user vừa join
        await hubContext.Clients.Group(room.Name).UserJoined(user);
    }

    public async Task NotifyUserLeft(Guid roomId, string username, CancellationToken ct)
    {
        var room = await roomRepository.GetByIdAsync(roomId, ct);
        if (room is null) return;

        await hubContext.Clients.Group(room.Name).UserLeft(username);
    }

    public async Task NotifyOnlineUsers(Guid roomId, IEnumerable<UserDto> users, CancellationToken ct)
    {
        var room = await roomRepository.GetByIdAsync(roomId, ct);
        if (room is null) return;

        await hubContext.Clients.Group(room.Name).OnlineUsers(users);
    }
}
