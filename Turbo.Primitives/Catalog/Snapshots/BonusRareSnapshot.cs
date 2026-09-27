using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>BonusRareInfoMessageParser's four fields; a class id of -1 hides the campaign.</summary>
[GenerateSerializer, Immutable]
public sealed record BonusRareSnapshot
{
    [Id(0)]
    public required string ProductCode { get; init; }

    [Id(1)]
    public required int ProductClassId { get; init; }

    [Id(2)]
    public required int TotalCoinsForBonus { get; init; }

    [Id(3)]
    public required int CoinsStillRequiredToBuy { get; init; }
}
