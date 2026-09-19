using Orleans;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Primitives.Badges.Snapshots;

/// <summary>What the hotel knows about a badge code: how many players own it and how rare that makes it.</summary>
[GenerateSerializer, Immutable]
public sealed record BadgeInfoSnapshot
{
    [Id(0)]
    public required string BadgeCode { get; init; }

    [Id(1)]
    public required int OwnerCount { get; init; }

    [Id(2)]
    public required BadgeRarityType Rarity { get; init; }
}
