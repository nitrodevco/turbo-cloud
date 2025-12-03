using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record MiniMailUnreadCountMessageComposer : IComposer
{
    [Id(0)]
    public required int UnreadCount { get; init; }
}
