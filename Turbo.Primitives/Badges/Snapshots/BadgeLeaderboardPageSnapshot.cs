using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Primitives.Badges.Snapshots;

/// <summary>One chunk of a board, as the client pages it, with the asking player's own row.</summary>
[GenerateSerializer, Immutable]
public sealed record BadgeLeaderboardPageSnapshot
{
    [Id(0)]
    public required BadgeLeaderboardType Type { get; init; }

    /// <summary>The tier of a rarity board; -1 on the other boards, as the client sends it.</summary>
    [Id(1)]
    public required int Rarity { get; init; }

    [Id(2)]
    public required int ChunkIndex { get; init; }

    [Id(3)]
    public required int ChunkSize { get; init; }

    [Id(4)]
    public required int TotalEntries { get; init; }

    [Id(5)]
    public required ImmutableArray<BadgeLeaderboardEntrySnapshot> Entries { get; init; }

    /// <summary>Null when the asking player has no score on this board.</summary>
    [Id(6)]
    public required BadgeLeaderboardEntrySnapshot? OwnEntry { get; init; }
}
