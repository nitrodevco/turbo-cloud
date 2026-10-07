using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>
/// A wired contract as the client edits it: the rules definition, then the fields of its type.
/// Every field is present; the payment fields go on the wire only for a payment contract and the
/// reward fields only for a reward contract.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredContractSnapshot
{
    [Id(0)]
    public required int ContractId { get; init; }

    [Id(1)]
    public required WiredContractType Type { get; init; }

    /// <summary>A payment contract fills the give rules, a reward contract the get rule, a trade both.</summary>
    [Id(2)]
    public required TradeRequirementRulesDefinitionSnapshot Definition { get; init; }

    /// <summary>Payment only.</summary>
    [Id(3)]
    public WiredContractPaymentMode PaymentMode { get; init; } = WiredContractPaymentMode.Donation;

    /// <summary>Payment only.</summary>
    [Id(4)]
    public string ReceiveText { get; init; } = string.Empty;

    /// <summary>Payment only: <c>generic</c> or <c>games</c> in the client's editor.</summary>
    [Id(5)]
    public string LayoutType { get; init; } = string.Empty;

    /// <summary>Reward only: the earnings category id the client's dropdown offers (see <see cref="WiredEarningsCategory"/>).</summary>
    [Id(6)]
    public int RewardCategory { get; init; }

    /// <summary>Reward only: show the reward dialog without the user asking.</summary>
    [Id(7)]
    public bool ShowDialog { get; init; }

    /// <summary>Reward only.</summary>
    [Id(8)]
    public string RewardText { get; init; } = string.Empty;
}
