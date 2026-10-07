using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Furniture;

[Table("furniture")]
// An inventory is the rows a player owns that are in no room (player_id = ? AND room_id IS NULL).
[Index(nameof(PlayerEntityId), nameof(RoomEntityId))]
[Index(nameof(ChestItemEntityId))]
public class FurnitureEntity : TurboEntity, IPlacedFurnitureEntity
{
    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    [Column("definition_id")]
    public int FurnitureDefinitionEntityId { get; set; }

    [Column("room_id")]
    public int? RoomEntityId { get; set; }

    [Column("x")]
    [DefaultValue(0)]
    public int X { get; set; } = 0;

    [Column("y")]
    [DefaultValue(0)]
    public int Y { get; set; } = 0;

    [Column("z", TypeName = "double(10,3)")]
    [DefaultValue(0.0d)]
    public double Z { get; set; }

    [Column("direction")]
    [DefaultValue(Rotation.North)] // Rotation.North
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Rotation Rotation { get; set; }

    [Column("wall_offset")]
    [DefaultValue(0)]
    public int WallOffset { get; set; } = 0;

    [Column("extra_data")]
    public string? ExtraData { get; set; }

    /// <summary>
    /// The wired chest this row is stored in, or null. A stored row is in no room and in no
    /// inventory: every query that lists an inventory excludes it. When the chest row goes, the
    /// database clears this and the row falls back to its owner's inventory.
    /// </summary>
    [Column("chest_item_id")]
    public int? ChestItemEntityId { get; set; }

    /// <summary>The chest transaction that put the row in its chest; null outside a chest.</summary>
    [Column("chest_transaction_id")]
    public long? ChestTransactionId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public FurnitureDefinitionEntity? FurnitureDefinitionEntity { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }

    [ForeignKey(nameof(ChestItemEntityId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public FurnitureEntity? ChestItemEntity { get; set; }
}
