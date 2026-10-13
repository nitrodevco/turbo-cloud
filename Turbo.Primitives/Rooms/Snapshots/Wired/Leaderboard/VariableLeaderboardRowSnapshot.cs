using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;

/// <summary>
/// One leaderboard entry (Flash <c>variableleaderboard/_-02R.as</c>): rank, the holder's user id,
/// name and figure, the value as text, when the value was created and last changed (epoch ms),
/// and the level and text connection when the stack has a Level Up or Text Connector add-on.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VariableLeaderboardRowSnapshot
{
    [Id(0)]
    public required int Rank { get; init; }

    [Id(1)]
    public required int UserId { get; init; }

    [Id(2)]
    public required string Username { get; init; }

    [Id(3)]
    public required string Figure { get; init; }

    [Id(4)]
    public required string RawValue { get; init; }

    [Id(5)]
    public required long CreationTime { get; init; }

    [Id(6)]
    public required long LastUpdateTime { get; init; }

    [Id(7)]
    public string? Level { get; init; }

    [Id(8)]
    public string? TextConnection { get; init; }
}
