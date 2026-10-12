using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Room;

/// <summary>
/// One line said in a room. The room and players are plain ids, not foreign keys: the line is
/// moderation evidence, and stays when the room or a player is deleted.
/// </summary>
[Comment("One line said in a room.")]
[Table("room_chat_logs")]
// The moderation tools page a room's or a player's lines newest first, by id.
[Index(nameof(RoomEntityId), nameof(Id))]
[Index(nameof(PlayerEntityId), nameof(Id))]
[Index(nameof(TargetPlayerEntityId), nameof(Id))]
public class RoomChatlogEntity : TurboLogEntity
{
    public const int MESSAGE_MAX_LENGTH = 100;

    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("target_player_id")]
    public int? TargetPlayerEntityId { get; set; }

    /// <summary>The line as the room heard it, after the word filter.</summary>
    [Column("message")]
    [StringLength(MESSAGE_MAX_LENGTH)]
    public required string Message { get; set; }

    /// <summary>
    /// What the player typed, when the word filter changed it; null when the line went out as
    /// typed, which is nearly always, so it costs nothing then. A moderator judges what was
    /// typed, not what the filter let through.
    /// </summary>
    [Column("typed_message")]
    [StringLength(MESSAGE_MAX_LENGTH)]
    public string? TypedMessage { get; set; }
}
