using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record FollowFriendFailedMessage : IComposer
{
    public required FollowFriendErrorCodeEnum ErrorCode { get; init; }
}
