using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record FollowFriendFailedMessageComposer : IComposer
{
    [Id(0)]
    public required FollowFriendErrorCodeType ErrorCode { get; init; }
}
