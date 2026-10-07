using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>A completed wired transaction; a reward also carries what was given and how to show it.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionSuccessContentsSnapshot
{
    [Id(0)]
    public required WiredTransactionSuccessType Type { get; init; }

    /// <summary>Written only for Rewarded.</summary>
    [Id(1)]
    public required TradeRequirementRuleSnapshot? RewardContents { get; init; }

    /// <summary>Written only for Rewarded.</summary>
    [Id(2)]
    public required string? RewardText { get; init; }

    /// <summary>Written only for Rewarded.</summary>
    [Id(3)]
    public required bool OpenByDefault { get; init; }
}
