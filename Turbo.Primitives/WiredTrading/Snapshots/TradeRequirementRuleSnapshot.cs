using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>A set of coins and furni that together satisfy one side of a wired trade.</summary>
[GenerateSerializer, Immutable]
public sealed record TradeRequirementRuleSnapshot
{
    [Id(0)]
    public required ImmutableArray<TradeRequirementNodeSnapshot> Nodes { get; init; }
}
