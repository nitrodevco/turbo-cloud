using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// One chunk of up to 50 leaderboard rows (Flash <c>AchievementResolutionProgressMessageParser</c>,
/// misnamed by the obfuscator; handled by <c>VariableLeaderboardController.onChunkUpdate</c>): the
/// overlay, query revision and variable it answers, the chunk index and the total number of
/// entries. The client drops a chunk it did not subscribe to or whose revision or variable is
/// not the one it shows.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VariableLeaderboardChunkUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required int OverlayInstanceId { get; init; }

    [Id(1)]
    public required int QueryRevision { get; init; }

    [Id(2)]
    public required string SelectedVariableId { get; init; }

    [Id(3)]
    public required int ChunkIndex { get; init; }

    [Id(4)]
    public required int TotalCount { get; init; }

    [Id(5)]
    public required ImmutableArray<VariableLeaderboardRowSnapshot> Rows { get; init; }
}
