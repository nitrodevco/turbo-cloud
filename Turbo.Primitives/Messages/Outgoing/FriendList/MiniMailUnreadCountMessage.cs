using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Friendlist;

public record MiniMailUnreadCountMessage : IComposer
{
    public required int UnreadCount { get; init; }
}
