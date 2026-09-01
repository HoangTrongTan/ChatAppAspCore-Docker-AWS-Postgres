using ChatApp.Application.DTOs;
using ChatApp.Application.Features.Messages.Queries.GetMessages;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.API.Controllers;

/// <summary>
/// [REST API Controller] HTTP endpoints cho messages.
/// Thin controller: không có business logic, chỉ delegate vào MediatR.
///
/// [SOLID - Single Responsibility] Controller chỉ:
///   1. Parse HTTP request
///   2. Map sang Command/Query
///   3. Dispatch qua MediatR
///   4. Format HTTP response
///
/// Real-time chat → dùng SignalR Hub.
/// REST endpoints → fallback, history, server-sent data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MessagesController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// GET /api/messages?roomName=general&limit=50&offset=0
    /// Lấy lịch sử messages với phân trang.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<MessageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string roomName = "general",
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        // [CQRS - Query] Dispatch query → handler dùng Dapper + Cache
        var messages = await mediator.Send(new GetMessagesQuery(roomName, limit, offset), ct);
        return Ok(ApiResponse<IEnumerable<MessageDto>>.Ok(messages));
    }
}
