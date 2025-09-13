using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Database.Entities.Room;

[Table("room_models")]
[Index(nameof(Name), IsUnique = true)]
public class RoomModelEntity : TurboEntity
{
    [Column("name")]
    public required string Name { get; set; }

    [Column("model")]
    public required string Model { get; set; }

    [Column("door_x")]
    [DefaultValue(0)]
    public required int DoorX { get; set; }

    [Column("door_y")]
    [DefaultValue(0)]
    public required int DoorY { get; set; }

    [Column("door_rotation")]
    [DefaultValue(Rotation.North)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required Rotation DoorRotation { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public required bool Enabled { get; set; }

    [Column("custom")]
    [DefaultValue(false)]
    public required bool Custom { get; set; }
}
