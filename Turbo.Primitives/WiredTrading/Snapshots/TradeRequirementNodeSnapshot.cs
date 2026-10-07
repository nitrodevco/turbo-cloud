using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>One entry of a trade rule: an amount of coins, or an amount of one furni type.</summary>
[GenerateSerializer, Immutable]
public sealed record TradeRequirementNodeSnapshot
{
    [Id(0)]
    public required TradeRequirementNodeType Type { get; init; }

    [Id(1)]
    public required int Amount { get; init; }

    /// <summary>The furni type; set when <see cref="Type"/> is Furni, ignored otherwise.</summary>
    [Id(2)]
    public required ChestItemTypeSnapshot? ItemType { get; init; }
}
