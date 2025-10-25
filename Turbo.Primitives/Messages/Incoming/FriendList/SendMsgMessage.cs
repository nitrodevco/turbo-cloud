using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record SendMsgMessage : IMessageEvent
{
    public int ChatId { get; init; }
    public required string Message { get; init; }
    public int ConfirmationId { get; init; }
}
