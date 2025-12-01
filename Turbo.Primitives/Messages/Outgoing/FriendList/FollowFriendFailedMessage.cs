using Turbo.Contracts.Abstractions;
using Turbo.Primitives.FriendList.Enums;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record FollowFriendFailedMessage : IComposer
{
    public required FollowFriendErrorCodeType ErrorCode { get; init; }
}
