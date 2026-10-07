using Orleans;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>
/// Take something out of a wired chest. Null <see cref="Amount"/> takes everything; a named
/// <see cref="ItemType"/> takes only furni of that type.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WithdrawFromChestInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int? Amount { get; init; }

    [Id(1)]
    public required ChestItemTypeSnapshot? ItemType { get; init; }
}
