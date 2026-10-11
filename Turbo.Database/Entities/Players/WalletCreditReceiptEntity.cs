using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Database.Entities.Players;

/// <summary>Records that a referenced credit was applied, so repeating it changes nothing.</summary>
[Table("player_wallet_credit_receipts")]
[PrimaryKey(nameof(PlayerId), nameof(Reference))]
public sealed class WalletCreditReceiptEntity
{
    public int PlayerId { get; set; }

    [MaxLength(WalletCreditReference.MaxLength)]
    public required string Reference { get; set; }

    /// <summary>When the row was written, so old receipts can be purged.</summary>
    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
