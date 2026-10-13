using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// Wired context section 8 (<c>wiredcontext/params/VariableLeaderboardContext.as</c>), sent to the
/// Leaderboard add-on's editor: its overlay id (0 while it is unpublished), the tile it stands on,
/// how many user variables its stack has, then up to ten of them from the bottom.
/// </summary>
[GenerateSerializer, Immutable]
public record WiredVariableLeaderboardContextSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required int OverlayInstanceId { get; init; }

    [Id(2)]
    public required int TileX { get; init; }

    [Id(3)]
    public required int TileY { get; init; }

    [Id(4)]
    public required int TotalCandidateCount { get; init; }

    [Id(5)]
    public required ImmutableArray<WiredVariableLeaderboardChoiceSnapshot> Choices { get; init; }
}

/// <summary>
/// A stacked variable as the Leaderboard editor sees it (<c>wiredcontext/params/VariableLeaderboardChoice.as</c>):
/// id, technical name, whether it is persisted, whether it has a value, and how many Level Up
/// add-ons apply to it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredVariableLeaderboardChoiceSnapshot
{
    [Id(0)]
    public required string VariableId { get; init; }

    [Id(1)]
    public required string TechnicalName { get; init; }

    [Id(2)]
    public required bool Persistent { get; init; }

    [Id(3)]
    public required bool HasValue { get; init; }

    [Id(4)]
    public required int ApplicableLevelUpCount { get; init; }
}
