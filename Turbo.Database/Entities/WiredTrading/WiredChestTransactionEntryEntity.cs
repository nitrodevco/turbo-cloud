using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.WiredTrading;

/// <summary>
/// How much of one kind went into or out of one chest in a transaction: credits, or furni of
/// one type. The chest is not a foreign key, so the log keeps its entries after the chest is gone.
/// </summary>
[Table("wired_chest_transaction_entries")]
[Index(nameof(ChestItemId), nameof(TransactionEntityId))]
public class WiredChestTransactionEntryEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("transaction_id")]
    public long TransactionEntityId { get; set; }

    [Column("chest_item_id")]
    public int ChestItemId { get; set; }

    /// <summary>True when the entry put something into the chest, false when it took it out.</summary>
    [Column("is_deposit")]
    public bool IsDeposit { get; set; }

    /// <summary>True for credits; the item type columns are then unused.</summary>
    [Column("is_coins")]
    public bool IsCoins { get; set; }

    [Column("is_wall_item")]
    public bool IsWallItem { get; set; }

    [Column("type_id")]
    public int TypeId { get; set; }

    [Column("poster_id")]
    [MaxLength(64)]
    public required string PosterId { get; set; }

    [Column("count")]
    public int Count { get; set; }

    [ForeignKey(nameof(TransactionEntityId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public WiredChestTransactionEntity? TransactionEntity { get; set; }
}
