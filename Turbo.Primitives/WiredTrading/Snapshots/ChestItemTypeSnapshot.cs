using Orleans;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>A furni type as wired chests and trades count it; value-equal, so it can key a dictionary of counts.</summary>
[GenerateSerializer, Immutable]
public sealed record ChestItemTypeSnapshot
{
    [Id(0)]
    public required bool IsWallItem { get; init; }

    /// <summary>The furni definition's sprite id.</summary>
    [Id(1)]
    public required int TypeId { get; init; }

    /// <summary>The poster id of a legacy poster, or an empty string for none.</summary>
    [Id(2)]
    public required string LegacyPosterId { get; init; }
}
