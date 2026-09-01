using ChatApp.Application.DTOs;
using FluentValidation;
using MediatR;

namespace ChatApp.Application.Features.Messages.Commands.SendMessage;

/// <summary>
/// [CQRS - Command] Command chỉ để thay đổi state (write side).
/// Trả về MessageDto để FE hiển thị message vừa gửi ngay lập tức.
///
/// [Design Pattern - Command Pattern] Đóng gói request thành object,
/// có thể queue, log, undo dễ dàng.
///
/// [C# - Record] Immutable sau khi tạo → thread-safe, không bị mutate.
/// </summary>
public record SendMessageCommand(
    string Content,
    string RoomName,
    string SenderUsername
) : IRequest<MessageDto>;

/// <summary>
/// [FluentValidation] Validate input tại Application layer.
/// Validator được tự động chạy bởi ValidationBehavior trước khi vào Handler.
/// </summary>
public class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(2000).WithMessage("Message cannot exceed 2000 characters.");

        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Room name is required.");

        RuleFor(x => x.SenderUsername)
            .NotEmpty().WithMessage("Sender username is required.");
    }
}
