using Orleans;

namespace Turbo.Primitives.Players.Wallet;

/// <summary>
/// Why a balance moves, given with every debit and credit so the ledger says where money came
/// from and went. <paramref name="Detail"/> names the thing, when there is one: the offer, the
/// voucher code, the photo, the chest.
/// </summary>
[GenerateSerializer, Immutable]
public readonly record struct WalletChange(
    [property: Id(0)] WalletChangeReason Reason,
    [property: Id(1)] string? Detail = null
)
{
    /// <summary>The longest detail kept; a longer one is cut.</summary>
    public const int DETAIL_MAX_LENGTH = 100;
}
