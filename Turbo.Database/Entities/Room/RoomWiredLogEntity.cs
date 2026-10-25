using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Room;

/// <summary>
/// One line of a room's wired log (the wired menu's "View full logs"), kept while the room is
/// unloaded as Habbo keeps it: the lines of a room left empty were all listed again after it
/// loaded, eleven days old ones too (official client capture 2026-10-25, wired matrix
/// action.49.R6). Only the room's newest <c>WiredConfig.MaxRoomLogEntries</c> are kept. The room
/// id is a plain column, as on <see cref="CommandLogEntity"/>; indexed by room, newest first.
/// <see cref="TurboEntity.CreatedAt"/> is when the line was written.
/// </summary>
[Table("room_wired_logs")]
[Index(nameof(RoomEntityId), nameof(Id))]
public class RoomWiredLogEntity : TurboEntity
{
    public const int MESSAGE_MAX_LENGTH = 1000;

    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    /// <summary><c>wiredmenu.logs_overview.log_level.&lt;n&gt;</c>.</summary>
    [Column("level")]
    public required int Level { get; set; }

    /// <summary><c>wiredmenu.logs_overview.log_source.&lt;n&gt;</c>.</summary>
    [Column("source")]
    public required int Source { get; set; }

    [Column("message")]
    [StringLength(MESSAGE_MAX_LENGTH)]
    public required string Message { get; set; }
}
