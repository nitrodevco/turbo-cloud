using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>What a wired trade asks of the user, as the trade window shows it.</summary>
[GenerateSerializer, Immutable]
public sealed record TradeRequirementSnapshot
{
    [Id(0)]
    public required TradeRequirementType Type { get; init; }

    [Id(1)]
    public required string YouGetText { get; init; }

    [Id(2)]
    public required string LayoutType { get; init; }

    /// <summary>Set when <see cref="Type"/> is Rules; written only then.</summary>
    [Id(3)]
    public required TradeRequirementRulesSnapshot? Rules { get; init; }
}
