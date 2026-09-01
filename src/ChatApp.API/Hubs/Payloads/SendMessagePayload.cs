namespace ChatApp.API.Hubs.Payloads;

/// <summary>
/// [SignalR Payload] Input contract khi client gọi Hub method SendMessage.
///
/// FE Angular gọi:
/// this.connection.invoke("SendMessage", { content, roomName })
///
/// SignalR tự động deserialize object này từ JSON payload của client.
/// </summary>
public record SendMessagePayload(string Content, string RoomName);
