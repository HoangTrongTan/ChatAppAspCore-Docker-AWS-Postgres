using AutoMapper;
using ChatApp.Application.Common.Interfaces;
using ChatApp.Application.DTOs;
using ChatApp.Domain.Entities;
using ChatApp.Domain.Exceptions;
using ChatApp.Domain.Interfaces;
using ChatApp.Domain.Interfaces.Repositories;
using MediatR;

namespace ChatApp.Application.Features.Messages.Commands.SendMessage;

/// <summary>
/// [CQRS - Command Handler] Xử lý SendMessageCommand.
/// Handler biết cách orchestrate domain objects và infrastructure services.
///
/// [SOLID - Single Responsibility] Handler chỉ làm 1 việc: xử lý SendMessageCommand.
/// [SOLID - Dependency Inversion] Inject interfaces, không concrete classes.
///
/// Flow:
///   1. Validate (done by ValidationBehavior before this runs)
///   2. Load Room và User từ DB (cache first via ICacheService)
///   3. Tạo Message entity (domain logic trong entity)
///   4. Lưu vào DB qua Repository + UnitOfWork
///   5. SaveChangesAndDispatchEventsAsync → publish domain events:
///      - KafkaProducer gửi lên topic "chat-messages"
///      - RabbitMQ notification đến subscribers
///      - SignalR broadcast qua IChatNotifier
/// </summary>
public class SendMessageCommandHandler(
    IMessageRepository messageRepository,
    IUserRepository userRepository,
    IRoomRepository roomRepository,
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    IChatNotifier chatNotifier,
    IMapper mapper
) : IRequestHandler<SendMessageCommand, MessageDto>
{
    public async Task<MessageDto> Handle(SendMessageCommand request, CancellationToken ct)
    {
        // [Cache-Aside] Lấy user từ cache trước, fallback về DB
        var user = await cacheService.GetOrSetAsync(
            CacheKeys.UserByUsername(request.SenderUsername),
            async () => await userRepository.GetByUsernameAsync(request.SenderUsername, ct)
                        ?? throw new NotFoundException(nameof(User), request.SenderUsername),
            expiry: TimeSpan.FromMinutes(10),
            ct: ct);

        // Lấy room (general room sẽ được tạo sẵn khi seeding)
        var room = await cacheService.GetOrSetAsync(
            $"room:name:{request.RoomName}",
            async () => await roomRepository.GetByNameAsync(request.RoomName, ct)
                        ?? throw new NotFoundException(nameof(Room), request.RoomName),
            expiry: TimeSpan.FromMinutes(5),
            ct: ct);

        // [Domain Factory] Tạo Message qua factory method, domain validate logic bên trong
        var message = Message.Create(request.Content, user.Id, room.Id);
        await messageRepository.AddAsync(message, ct);

        // [Unit of Work] Save và dispatch domain events trong 1 transaction
        // Domain events sẽ trigger: Kafka publish + RabbitMQ publish + SignalR broadcast
        await unitOfWork.SaveChangesAndDispatchEventsAsync(ct);

        // Invalidate cache messages của room → next query sẽ lấy fresh data
        await cacheService.RemoveAsync(CacheKeys.RoomMessages(room.Id), ct);

        // Load lại message với navigation properties (Sender) để map sang DTO
        var savedMessage = await messageRepository.GetByIdAsync(message.Id, ct)
                           ?? throw new NotFoundException(nameof(Message), message.Id);

        var dto = mapper.Map<MessageDto>(savedMessage);

        // Ngay lập tức notify qua SignalR (real-time)
        await chatNotifier.NotifyMessageSent(room.Id, dto, ct);

        return dto;
    }
}
