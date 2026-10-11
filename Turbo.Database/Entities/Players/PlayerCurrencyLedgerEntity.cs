using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// Every change to a player's balance, with why and what it left, written in the same
/// transaction as the change. A balance is the sum of its ledger, so a player who says their
/// credits vanished, a refund in dispute or a duplicated reward is answered from here.
/// </summary>
[Comment("Every change to a player's balance: how much, why, and the balance it left.")]
[Table("player_currency_ledger")]
// A player's history, newest first.
[Index(nameof(PlayerEntityId), nameof(Id))]
public class PlayerCurrencyLedgerEntity : TurboLogEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("currency_type_id")]
    public required int CurrencyTypeId { get; set; }

    /// <summary>Signed: a debit is negative.</summary>
    [Column("change")]
    public required int Change { get; set; }

    [Column("balance_after")]
    public required int BalanceAfter { get; set; }

    [Column("reason")]
    public required WalletChangeReason Reason { get; set; }

    /// <summary>What it was for, when there is a thing to name: an offer, a voucher, a photo.</summary>
    [Column("detail")]
    [MaxLength(WalletChange.DETAIL_MAX_LENGTH)]
    public string? Detail { get; set; }
}
