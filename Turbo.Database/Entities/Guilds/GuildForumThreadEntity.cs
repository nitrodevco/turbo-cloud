using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Database.Entities.Guilds;

/// <summary>A forum thread. Its first message is the opening post.</summary>
[Table("guild_forum_threads")]
[Index(nameof(GuildEntityId), nameof(IsSticky), nameof(LastMessageAt))]
public class GuildForumThreadEntity : TurboEntity
{
    /// <summary>The client's compose window stops the subject at 120.</summary>
    public const int SUBJECT_MAX_LENGTH = 120;

    [Column("guild_id")]
    public required int GuildEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("subject")]
    [StringLength(SUBJECT_MAX_LENGTH)]
    public required string Subject { get; set; }

    [Column("is_sticky")]
    [DefaultValue(false)]
    public bool IsSticky { get; set; }

    [Column("is_locked")]
    [DefaultValue(false)]
    public bool IsLocked { get; set; }

    [Column("state")]
    [DefaultValue(GuildForumState.Normal)]
    public GuildForumState State { get; set; }

    [Column("moderator_id")]
    public int? ModeratorEntityId { get; set; }

    [Column("moderated_at")]
    public DateTime? ModeratedAt { get; set; }

    [Column("message_count")]
    [DefaultValue(0)]
    public int MessageCount { get; set; }

    [Column("last_message_at")]
    public DateTime LastMessageAt { get; set; }
}
