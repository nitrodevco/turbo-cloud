using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Players.Snapshots.Messenger;

[GenerateSerializer, Immutable]
public record AcceptFriendFailureSnapshot
{
    [Id(0)]
    public required PlayerId SenderId { get; init; }

    [Id(1)]
    public required FriendListErrorCodeType ErrorCode { get; init; }
}
