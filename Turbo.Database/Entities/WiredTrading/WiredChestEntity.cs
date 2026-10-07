using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;

namespace Turbo.Database.Entities.WiredTrading;

/// <summary>
/// What a wired chest holds besides its furni rows: the credits in a credit chest and how many
/// times its capacity was bought up. One row per chest furni, created on first use and removed
/// with the furni. The furni a furni chest holds are <c>furniture</c> rows pointing at it.
/// </summary>
[Table("wired_chests")]
public class WiredChestEntity
{
    [Key]
    [Column("item_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int ItemEntityId { get; set; }

    [Column("coins")]
    public int Coins { get; set; }

    [Column("capacity_level")]
    public int CapacityLevel { get; set; }

    [ForeignKey(nameof(ItemEntityId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public FurnitureEntity? ItemEntity { get; set; }
}
