using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>A trade rule set and how it scales.</summary>
[GenerateSerializer, Immutable]
public sealed record TradeRequirementRulesSnapshot
{
    [Id(0)]
    public required TradeRequirementRulesDefinitionSnapshot Definition { get; init; }

    [Id(1)]
    public required TradeRequirementRulesType Type { get; init; }

    /// <summary>Written only when <see cref="Type"/> is Multiplier.</summary>
    [Id(2)]
    public required int Multiplier { get; init; }

    /// <summary>Written only when <see cref="Type"/> is AutoMultiplier.</summary>
    [Id(3)]
    public required int AutoMultiplierMax { get; init; }
}
