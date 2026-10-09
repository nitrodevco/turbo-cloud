using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Database.Entities.Guilds;

/// <summary>
/// A forum message. <see cref="ForumMessageId"/> numbers it within the forum and
/// <see cref="ThreadIndex"/> places it in its thread, from 0.
/// </summary>
[Table("guild_forum_messages")]
[Index(nameof(GuildEntityId), nameof(ForumMessageId), IsUnique = true)]
[Index(nameof(ThreadEntityId), nameof(ThreadIndex))]
public class GuildForumMessageEntity : TurboEntity
{
    /// <summary>The client's compose window stops a message at 4000.</summary>
    public const int TEXT_MAX_LENGTH = 4000;

    [Column("guild_id")]
    public required int GuildEntityId { get; set; }

    [Column("thread_id")]
    public required int ThreadEntityId { get; set; }

    [Column("forum_message_id")]
    public required int ForumMessageId { get; set; }

    [Column("thread_index")]
    public required int ThreadIndex { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("text")]
    [StringLength(TEXT_MAX_LENGTH)]
    public required string Text { get; set; }

    [Column("state")]
    [DefaultValue(GuildForumState.Normal)]
    public GuildForumState State { get; set; }

    [Column("moderator_id")]
    public int? ModeratorEntityId { get; set; }

    [Column("moderated_at")]
    public DateTime? ModeratedAt { get; set; }

    [ForeignKey(nameof(ThreadEntityId))]
    public GuildForumThreadEntity? Thread { get; set; }
}
