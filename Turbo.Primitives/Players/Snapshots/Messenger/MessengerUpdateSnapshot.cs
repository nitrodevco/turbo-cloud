using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.Primitives.Players.Snapshots.Messenger;

[GenerateSerializer, Immutable]
public record MessengerUpdateSnapshot
{
    [Id(0)]
    public required FriendListUpdateActionType ActionType { get; init; }

    [Id(1)]
    public PlayerId FriendId { get; init; }

    [Id(2)]
    public MessengerFriendDto? Friend { get; init; }
}
