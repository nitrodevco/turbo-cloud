using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;

/// <summary>
/// One leaderboard column (Flash <c>variableleaderboard/_-53.as</c>): the column type (0 username,
/// 1 value, 2 last update, 3 creation time, 4 level, 5 text connection), the name shown in its
/// header and its width option (0 small 70 px, 1 medium 90, 2 large 120, 3 XL 170).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VariableLeaderboardColumnSnapshot
{
    [Id(0)]
    public required int Type { get; init; }

    [Id(1)]
    public required string Label { get; init; }

    [Id(2)]
    public required int Width { get; init; }
}
