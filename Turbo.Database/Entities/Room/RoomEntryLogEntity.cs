using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Room;

/// <summary>
/// A player entering a room. Plain ids, not foreign keys, so a visit stays in the log when the room
/// or the player is deleted.
/// </summary>
[Table("room_entry_logs")]
// Covers the navigator history: a player's visits grouped by room with the latest per room.
[Index(nameof(PlayerEntityId), nameof(RoomEntityId), nameof(CreatedAt))]
// A room's visitors, newest first, in the admin panel.
[Index(nameof(RoomEntityId), nameof(CreatedAt))]
public class RoomEntryLogEntity : TurboLogEntity
{
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }
}
