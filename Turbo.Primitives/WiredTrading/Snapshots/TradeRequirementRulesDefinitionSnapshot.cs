using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>What the user may give (any one of several rules) and what they get for it.</summary>
[GenerateSerializer, Immutable]
public sealed record TradeRequirementRulesDefinitionSnapshot
{
    /// <summary>The alternatives the user may give, or null for none.</summary>
    [Id(0)]
    public required ImmutableArray<TradeRequirementRuleSnapshot>? YouGive { get; init; }

    /// <summary>What the user receives, or null for nothing.</summary>
    [Id(1)]
    public required TradeRequirementRuleSnapshot? YouGet { get; init; }
}
