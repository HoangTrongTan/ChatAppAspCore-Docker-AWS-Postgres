namespace ChatApp.API.Hubs.Payloads;

/// <summary>
/// [SignalR Payload] Input contract khi client gọi Hub method JoinRoom.
///
/// FE Angular gọi:
/// this.connection.invoke("JoinRoom", { username, avatarIndex, roomId })
///
/// SignalR tự động deserialize object này từ JSON payload của client.
/// </summary>
public record JoinRoomPayload(string Username, int AvatarIndex, string RoomId);
