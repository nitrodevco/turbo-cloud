using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Messages.Outgoing.Friendlist;

public record FollowFriendFailedMessage : IComposer
{
    public required FollowFriendErrorCodeEnum ErrorCode { get; init; }
}
