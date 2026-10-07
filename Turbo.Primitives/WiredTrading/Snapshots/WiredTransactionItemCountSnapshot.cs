using Orleans;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>How many of one furni type a transaction moved.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionItemCountSnapshot
{
    [Id(0)]
    public required ChestItemTypeSnapshot Type { get; init; }

    [Id(1)]
    public required int Count { get; init; }
}
