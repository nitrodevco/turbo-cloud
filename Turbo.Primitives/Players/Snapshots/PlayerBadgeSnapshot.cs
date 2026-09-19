using Orleans;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Primitives.Players.Snapshots;

/// <summary>A badge a player owns, with the hotel-wide figures the client shows beside it.</summary>
[GenerateSerializer, Immutable]
public sealed record PlayerBadgeSnapshot
{
    /// <summary>The id of the player's badge row; the client tracks "unseen" badges by it.</summary>
    [Id(0)]
    public required int BadgeId { get; init; }

    [Id(1)]
    public required string BadgeCode { get; init; }

    /// <summary>1 to the number of wearable slots, or 0 when the badge is not worn.</summary>
    [Id(2)]
    public required int SlotId { get; init; }

    [Id(3)]
    public required int OwnerCount { get; init; }

    [Id(4)]
    public required BadgeRarityType Rarity { get; init; }

    public bool IsWorn => SlotId > 0;
}
