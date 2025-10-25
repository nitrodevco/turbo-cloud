using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record GetMessengerHistoryMessage : IMessageEvent
{
    public int ChatId { get; init; }
    public required string Message { get; init; }
}
