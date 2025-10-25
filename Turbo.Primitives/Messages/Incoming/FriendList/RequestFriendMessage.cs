using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record RequestFriendMessage : IMessageEvent
{
    public required string PlayerName { get; init; }
}
