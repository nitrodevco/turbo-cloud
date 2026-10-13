using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Every published leaderboard in the room, at most four (Flash
/// <c>VariableLeaderboardSnapshotMessageParser</c>). The client drops the ones it had that are not
/// in it, so an empty one clears them; Habbo sends one, empty when there are none, on every room
/// entry (official client capture, WIN63-202610121709: <c>00000000</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VariableLeaderboardSnapshotMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<VariableLeaderboardDescriptorSnapshot> Descriptors { get; init; }
}
