using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;

/// <summary>
/// A variable a viewer can pick on a leaderboard (Flash <c>variableleaderboard/_-a7.as</c>): its id
/// and the name the leaderboard shows for it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VariableLeaderboardChoiceSnapshot
{
    [Id(0)]
    public required string VariableId { get; init; }

    [Id(1)]
    public required string DisplayName { get; init; }
}
