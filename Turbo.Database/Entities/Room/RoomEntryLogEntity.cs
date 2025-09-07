using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Attributes;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Room;

[TurboEntity]
[Table("room_entry_logs")]
public class RoomEntryLogEntity : Entity
{
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public required RoomEntity RoomEntity { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
