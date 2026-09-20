using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Furniture;

/// <summary>
/// A furni the Builders Club lends. It stands in a room like any other and outlives a reload,
/// but nobody owns it: it is not in <c>furniture</c>, it never reaches an inventory, and picking
/// it up deletes the row.
/// <para>
/// The key is the room and the id the room-facing object id, because a furni object id only has
/// to be unique within a room session and Builders Club ids have to fall inside the band the
/// client reads their kind out of (<c>FurniIdBands</c>) — which an auto-increment shared with
/// <c>furniture</c> could never do without dragging every ordinary furni id up with it. Storing
/// the id rather than handing it out again on each load is what keeps wired values keyed by
/// furni id pointing at the same furni.
/// </para>
/// </summary>
[Table("builders_club_furniture")]
[PrimaryKey(nameof(RoomEntityId), nameof(RoomObjectId))]
// The borrow count is per player across every room, which the key above cannot serve.
[Index(nameof(PlacedByPlayerEntityId))]
public class BuildersClubFurnitureEntity
{
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    /// <summary>The id the room and the client know this furni by, inside the Builders Club band.</summary>
    [Column("room_object_id")]
    public required int RoomObjectId { get; set; }

    [Column("definition_id")]
    public required int FurnitureDefinitionEntityId { get; set; }

    /// <summary>Who borrowed it. It counts against their limit; it is not ownership.</summary>
    [Column("placed_by_player_id")]
    public required int PlacedByPlayerEntityId { get; set; }

    /// <summary>The Builders Club offer it came from, so a pickup can warn when it is gone.</summary>
    [Column("offer_id")]
    public required int CatalogOfferEntityId { get; set; }

    [Column("x")]
    [DefaultValue(0)]
    public int X { get; set; }

    [Column("y")]
    [DefaultValue(0)]
    public int Y { get; set; }

    [Column("z", TypeName = "double(10,3)")]
    [DefaultValue(0.0d)]
    public double Z { get; set; }

    [Column("direction")]
    [DefaultValue(Rotation.North)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Rotation Rotation { get; set; }

    [Column("wall_offset")]
    [DefaultValue(0)]
    public int WallOffset { get; set; }

    [Column("extra_data")]
    public string? ExtraData { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public FurnitureDefinitionEntity? FurnitureDefinitionEntity { get; set; }

    [ForeignKey(nameof(PlacedByPlayerEntityId))]
    public PlayerEntity? PlacedByPlayerEntity { get; set; }
}
