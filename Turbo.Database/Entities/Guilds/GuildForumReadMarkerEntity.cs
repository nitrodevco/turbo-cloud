using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Guilds;

/// <summary>
/// How far a player has read a forum: the last message read, numbered within the forum.
/// <see cref="ReadAt"/> is when they last read it, which the Most Viewed list counts.
/// </summary>
[Table("guild_forum_read_markers")]
[Index(nameof(PlayerEntityId), nameof(GuildEntityId), IsUnique = true)]
[Index(nameof(GuildEntityId), nameof(ReadAt))]
public class GuildForumReadMarkerEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("guild_id")]
    public required int GuildEntityId { get; set; }

    [Column("last_read_message_id")]
    public int LastReadMessageId { get; set; }

    [Column("read_at")]
    public DateTime ReadAt { get; set; }
}
