using AutoMapper;
using ChatApp.Application.Common.Interfaces;
using ChatApp.Application.DTOs;
using ChatApp.Domain.Exceptions;
using ChatApp.Domain.Interfaces.Repositories;
using MediatR;

namespace ChatApp.Application.Features.Messages.Queries.GetMessages;

/// <summary>
/// [CQRS - Query] Query chỉ đọc dữ liệu, không thay đổi state.
/// Query handler nên tối ưu cho read: dùng Dapper + Cache.
///
/// [C# - Record with default values] Pagination params có default.
/// </summary>
public record GetMessagesQuery(
    string RoomName,
    int Limit = 50,
    int Offset = 0
) : IRequest<IEnumerable<MessageDto>>;

/// <summary>
/// [CQRS - Query Handler] Chỉ đọc, không có side effects.
/// Dùng Dapper raw SQL (trong repository) cho hiệu năng cao hơn EF Core LINQ.
///
/// [Design Pattern - Cache-Aside]
///   1. Check cache (Redis) → nếu hit → return ngay
///   2. Cache miss → query DB (Dapper raw SQL)
///   3. Set cache → return kết quả
/// </summary>
public class GetMessagesQueryHandler(
    IMessageRepository messageRepository,
    IRoomRepository roomRepository,
    ICacheService cacheService,
    IMapper mapper
) : IRequestHandler<GetMessagesQuery, IEnumerable<MessageDto>>
{
    public async Task<IEnumerable<MessageDto>> Handle(
        GetMessagesQuery request, CancellationToken ct)
    {
        var room = await roomRepository.GetByNameAsync(request.RoomName, ct)
                   ?? throw new NotFoundException("Room", request.RoomName);

        // [Cache-Aside Pattern] key bao gồm pagination params
        var cacheKey = $"{CacheKeys.RoomMessages(room.Id)}:{request.Limit}:{request.Offset}";

        return await cacheService.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                // [Raw SQL via Dapper] Gọi repository dùng raw SQL bên trong
                var messages = await messageRepository.GetByRoomIdAsync(
                    room.Id, request.Limit, request.Offset, ct);

                return mapper.Map<IEnumerable<MessageDto>>(messages);
            },
            expiry: TimeSpan.FromSeconds(30), // Short TTL cho chat messages
            ct: ct);
    }
}
