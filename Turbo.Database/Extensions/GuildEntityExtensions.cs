using System;
using Turbo.Database.Entities.Guilds;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Database.Extensions;

/// <summary>
/// Row to snapshot, shared by the guild directory (summaries for the whole hotel) and the guild
/// grain (the group itself).
///
/// Two things the guild row does not hold are parameters: whether the group has a forum (that is
/// whether a settings row exists) and the badge palette its two colour ids point into. The
/// palette is a snapshot rather than a provider, so these stay pure functions of what they are
/// handed — and taking it whole means the two-slot lookup is written here once instead of at
/// every call site.
/// </summary>
public static class GuildEntityExtensions
{
    public static GuildSummarySnapshot ToSummarySnapshot(
        this GuildEntity entity,
        GuildEditorDataSnapshot palette,
        bool hasForum
    ) =>
        new()
        {
            GuildId = GuildId.Parse(entity.Id),
            Name = entity.Name,
            BadgeCode = entity.BadgeCode,
            RoomId = RoomId.Parse(entity.RoomEntityId),
            OwnerId = PlayerId.Parse(entity.PlayerEntityId),
            PrimaryColorId = entity.PrimaryColorId,
            SecondaryColorId = entity.SecondaryColorId,
            PrimaryColor = palette.GetColor(GuildColorSlotType.Primary, entity.PrimaryColorId),
            SecondaryColor = palette.GetColor(
                GuildColorSlotType.Secondary,
                entity.SecondaryColorId
            ),
            Type = entity.GuildType,
            HasForum = hasForum,
            RightsLevel = entity.RightsLevel,
        };

    public static GuildSnapshot ToSnapshot(
        this GuildEntity entity,
        GuildEditorDataSnapshot palette,
        bool hasForum
    ) =>
        new(entity.ToSummarySnapshot(palette, hasForum))
        {
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
        };

    public static GuildBadgePartDefinitionSnapshot ToSnapshot(this GuildBadgePartEntity entity) =>
        new()
        {
            Type = entity.PartType,
            PartId = entity.PartId,
            FileName = entity.FileName,
            MaskFileName = entity.MaskFileName,
        };

    public static GuildColorSnapshot ToSnapshot(this GuildColorEntity entity) =>
        new()
        {
            Slot = entity.Slot,
            ColorId = entity.ColorId,
            Color = entity.Color,
        };

    /// <summary>Whole seconds from <paramref name="at"/> to <paramref name="now"/>, never below 0.</summary>
    private static int SecondsAgo(DateTime? at, DateTime now) =>
        at is { } value ? (int)Math.Max(0, Math.Min(int.MaxValue, (now - value).TotalSeconds)) : 0;

    public static GuildForumSnapshot ToSnapshot(
        this GuildForumEntity forum,
        GuildEntity guild,
        string lastMessageAuthorName,
        int lastReadMessageId,
        int recentMessages,
        DateTime now
    ) =>
        new()
        {
            GroupId = guild.Id,
            Name = guild.Name,
            Description = guild.Description,
            Icon = guild.BadgeCode,
            TotalThreads = forum.ThreadCount,
            LeaderboardScore = recentMessages,
            TotalMessages = forum.MessageCount,
            UnreadMessages = Math.Max(0, forum.MessageCount - lastReadMessageId),
            LastMessageId = forum.MessageCount,
            LastMessageAuthorId = forum.LastMessagePlayerEntityId ?? 0,
            LastMessageAuthorName = lastMessageAuthorName,
            LastMessageSecondsAgo = SecondsAgo(forum.LastMessageAt, now),
        };

    /// <param name="lastMessage">The thread's last message, null for a thread with none.</param>
    /// <param name="subject">The subject as this viewer may see it.</param>
    public static GuildForumThreadSnapshot ToSnapshot(
        this GuildForumThreadEntity thread,
        string subject,
        string authorName,
        GuildForumMessageEntity? lastMessage,
        string lastMessageAuthorName,
        int unreadMessages,
        string moderatorName,
        DateTime now
    ) =>
        new()
        {
            ThreadId = thread.Id,
            AuthorId = thread.PlayerEntityId,
            AuthorName = authorName,
            Subject = subject,
            IsSticky = thread.IsSticky,
            IsLocked = thread.IsLocked,
            CreatedSecondsAgo = SecondsAgo(thread.CreatedAt, now),
            TotalMessages = thread.MessageCount,
            UnreadMessages = unreadMessages,
            LastMessageId = lastMessage?.ForumMessageId ?? 0,
            LastMessageAuthorId = lastMessage?.PlayerEntityId ?? 0,
            LastMessageAuthorName = lastMessageAuthorName,
            LastMessageSecondsAgo = SecondsAgo(lastMessage?.CreatedAt, now),
            State = thread.State,
            ModeratorId = thread.ModeratorEntityId ?? 0,
            ModeratorName = moderatorName,
            ModeratedSecondsAgo = SecondsAgo(thread.ModeratedAt, now),
        };

    /// <param name="text">The text as this viewer may see it.</param>
    public static GuildForumMessageSnapshot ToSnapshot(
        this GuildForumMessageEntity message,
        string text,
        PlayerEntity? author,
        int authorPostCount,
        string moderatorName,
        DateTime now
    ) =>
        new()
        {
            MessageId = message.ForumMessageId,
            MessageIndex = message.ThreadIndex,
            AuthorId = message.PlayerEntityId,
            AuthorName = author?.Name ?? "",
            AuthorFigure = author?.Figure ?? "",
            CreatedSecondsAgo = SecondsAgo(message.CreatedAt, now),
            Text = text,
            State = message.State,
            ModeratorId = message.ModeratorEntityId ?? 0,
            ModeratorName = moderatorName,
            ModeratedSecondsAgo = SecondsAgo(message.ModeratedAt, now),
            AuthorPostCount = authorPostCount,
        };
}
