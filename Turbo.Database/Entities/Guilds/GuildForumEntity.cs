using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Database.Entities.Guilds;

/// <summary>
/// A group's forum, opened when the group's owner buys a forum terminal for it. Its messages
/// are numbered within the forum (<see cref="MessageCount"/> is the last number given), which is
/// what a read marker counts: the client works a forum's unread count out as total minus the
/// last read message (AS3 ForumData).
/// </summary>
[Table("guild_forums")]
[Index(nameof(GuildEntityId), IsUnique = true)]
public class GuildForumEntity : TurboEntity
{
    [Column("guild_id")]
    public required int GuildEntityId { get; set; }

    [Column("read_permission")]
    [DefaultValue(GuildForumPermission.Everybody)]
    public GuildForumPermission ReadPermission { get; set; } = GuildForumPermission.Everybody;

    [Column("post_message_permission")]
    [DefaultValue(GuildForumPermission.Members)]
    public GuildForumPermission PostMessagePermission { get; set; } = GuildForumPermission.Members;

    [Column("post_thread_permission")]
    [DefaultValue(GuildForumPermission.Members)]
    public GuildForumPermission PostThreadPermission { get; set; } = GuildForumPermission.Members;

    [Column("moderate_permission")]
    [DefaultValue(GuildForumPermission.Admins)]
    public GuildForumPermission ModeratePermission { get; set; } = GuildForumPermission.Admins;

    [Column("thread_count")]
    [DefaultValue(0)]
    public int ThreadCount { get; set; }

    [Column("message_count")]
    [DefaultValue(0)]
    public int MessageCount { get; set; }

    [Column("last_message_player_id")]
    public int? LastMessagePlayerEntityId { get; set; }

    [Column("last_message_at")]
    public DateTime? LastMessageAt { get; set; }

    [ForeignKey(nameof(GuildEntityId))]
    public GuildEntity? GuildEntity { get; set; }
}
