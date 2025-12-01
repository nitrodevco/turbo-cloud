using Orleans;
using Turbo.Primitives.FriendList.Enums;

namespace Turbo.Primitives.Snapshots.FriendList;

[GenerateSerializer, Immutable]
public record AcceptFriendFailureSnapshot
{
    [Id(0)]
    public required long SenderId { get; init; }

    [Id(1)]
    public required FriendListErrorCodeType ErrorCode { get; init; }
}
