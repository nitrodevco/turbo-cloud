using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Players.Snapshots.Messenger;

[GenerateSerializer, Immutable]
public record RelationshipStatusEntrySnapshot
{
    [Id(0)]
    public required MessengerFriendRelationType RelationshipStatusType { get; init; }

    [Id(1)]
    public required int FriendCount { get; init; }

    [Id(2)]
    public required PlayerId RandomFriendId { get; init; }

    [Id(3)]
    public required string RandomFriendName { get; init; }

    [Id(4)]
    public required string RandomFriendFigure { get; init; }
}
