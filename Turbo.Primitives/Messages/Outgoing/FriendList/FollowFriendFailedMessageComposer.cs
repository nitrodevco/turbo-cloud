using Orleans;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

[GenerateSerializer, Immutable]
public sealed record FollowFriendFailedMessageComposer : IComposer
{
    [Id(0)]
    public required FollowFriendErrorCodeType ErrorCode { get; init; }
}
