using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.FriendList;

public record FollowFriendMessage : IMessageEvent
{
    public required int PlayerId { get; init; }
}
