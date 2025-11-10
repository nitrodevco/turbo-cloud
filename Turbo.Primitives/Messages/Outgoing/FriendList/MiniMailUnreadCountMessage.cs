using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record MiniMailUnreadCountMessage : IComposer
{
    public required int UnreadCount { get; init; }
}
