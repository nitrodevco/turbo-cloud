using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record FollowFriendFailedMessage : IComposer
{
    public required FollowFriendErrorCodeType ErrorCode { get; init; }
}
