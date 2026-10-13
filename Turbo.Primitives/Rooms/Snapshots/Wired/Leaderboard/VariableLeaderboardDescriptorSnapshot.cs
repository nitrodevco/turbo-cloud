using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;

/// <summary>
/// A published leaderboard as <c>VariableLeaderboardSnapshot</c> carries it (Flash
/// <c>VariableLeaderboardDescriptorData.fromMessage</c>): the overlay id, the query revision the
/// client subscribes with, title and description, the location (tile, x offset, altitude), the
/// sort mode, the columns, the variables a viewer can pick, the one picked by default and the
/// page size option (0..2 = 10, 15, 20 rows).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VariableLeaderboardDescriptorSnapshot
{
    [Id(0)]
    public required int OverlayInstanceId { get; init; }

    [Id(1)]
    public required int QueryRevision { get; init; }

    [Id(2)]
    public required string Title { get; init; }

    [Id(3)]
    public required string Description { get; init; }

    [Id(4)]
    public required int TileX { get; init; }

    [Id(5)]
    public required int TileY { get; init; }

    [Id(6)]
    public required int OffsetX { get; init; }

    [Id(7)]
    public required int Altitude { get; init; }

    [Id(8)]
    public required int SortMode { get; init; }

    [Id(9)]
    public required ImmutableArray<VariableLeaderboardColumnSnapshot> Columns { get; init; }

    [Id(10)]
    public required ImmutableArray<VariableLeaderboardChoiceSnapshot> Choices { get; init; }

    [Id(11)]
    public required string DefaultVariableId { get; init; }

    [Id(12)]
    public required int PageSizeOption { get; init; }
}
