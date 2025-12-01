using Orleans;
using Turbo.Primitives.FriendList.Enums;

namespace Turbo.Primitives.Snapshots.FriendList;

[GenerateSerializer, Immutable]
public record FriendListUpdateSnapshot
{
    [Id(0)]
    public required FriendListUpdateActionType ActionType { get; init; }

    [Id(1)]
    public int? FriendId { get; init; }

    [Id(2)]
    public MessengerFriendSnapshot? Friend { get; init; }
}
