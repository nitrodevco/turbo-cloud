using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Database.Entities.Players;

/// <summary>Records that a referenced credit was applied, so repeating it changes nothing.</summary>
[Table("wallet_credit_receipts")]
[PrimaryKey(nameof(PlayerId), nameof(Reference))]
public sealed class WalletCreditReceiptEntity
{
    public int PlayerId { get; set; }

    [MaxLength(WalletCreditReference.MaxLength)]
    public required string Reference { get; set; }
}
