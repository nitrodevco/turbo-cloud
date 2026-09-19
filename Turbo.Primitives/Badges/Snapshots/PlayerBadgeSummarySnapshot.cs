using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Badges.Snapshots;

/// <summary>The badge figures a profile shows: how many, how many of each rarity, and the rank that gives.</summary>
[GenerateSerializer, Immutable]
public sealed record PlayerBadgeSummarySnapshot
{
    [Id(0)]
    public required int TotalBadges { get; init; }

    [Id(1)]
    public required ImmutableArray<BadgeRarityCountSnapshot> RarityCounts { get; init; }

    /// <summary>The place on the total badges board, or <see cref="BadgeRanks.NONE"/> with no badges.</summary>
    [Id(2)]
    public required int TotalBadgesRank { get; init; }
}
