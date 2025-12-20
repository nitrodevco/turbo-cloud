using Orleans;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Snapshots.FriendList;

[GenerateSerializer, Immutable]
public record AcceptFriendFailureSnapshot
{
    [Id(0)]
    public required PlayerId SenderId { get; init; }

    [Id(1)]
    public required FriendListErrorCodeType ErrorCode { get; init; }
}
