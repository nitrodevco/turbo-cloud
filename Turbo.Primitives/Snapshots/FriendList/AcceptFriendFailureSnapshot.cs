using Orleans;
using Turbo.Contracts.Enums.FriendList;

namespace Turbo.Primitives.Snapshots.FriendList;

[GenerateSerializer, Immutable]
public record AcceptFriendFailureSnapshot
{
    [Id(0)]
    public required long SenderId { get; init; }

    [Id(1)]
    public required FriendListErrorCodeEnum ErrorCode { get; init; }
}
