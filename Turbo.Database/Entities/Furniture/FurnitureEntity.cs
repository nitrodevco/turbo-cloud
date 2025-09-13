using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;

namespace Turbo.Database.Entities.Furniture;

[Table("furniture")]
public class FurnitureEntity : TurboEntity
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

    [Column("stuff_data")]
    public string? StuffData { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public required FurnitureDefinitionEntity FurnitureDefinitionEntity { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }
}
