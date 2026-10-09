using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>The forum, thread and message records in the order the AS3 parsers read them.</summary>
internal static class GuildForumWriter
{
    /// <summary>ForumData.fillFromMessage.</summary>
    public static void WriteForum(IServerPacket packet, GuildForumSnapshot forum)
    {
        packet.WriteInteger(forum.GroupId);
        packet.WriteString(forum.Name);
        packet.WriteString(forum.Description);
        packet.WriteString(forum.Icon);
        packet.WriteInteger(forum.TotalThreads);
        packet.WriteInteger(forum.LeaderboardScore);
        packet.WriteInteger(forum.TotalMessages);
        packet.WriteInteger(forum.UnreadMessages);
        packet.WriteInteger(forum.LastMessageId);
        packet.WriteInteger(forum.LastMessageAuthorId);
        packet.WriteString(forum.LastMessageAuthorName);
        packet.WriteInteger(forum.LastMessageSecondsAgo);
    }

    /// <summary>ThreadData.readFromMessage.</summary>
    public static void WriteThread(IServerPacket packet, GuildForumThreadSnapshot thread)
    {
        packet.WriteInteger(thread.ThreadId);
        packet.WriteInteger(thread.AuthorId);
        packet.WriteString(thread.AuthorName);
        packet.WriteString(thread.Subject);
        packet.WriteBoolean(thread.IsSticky);
        packet.WriteBoolean(thread.IsLocked);
        packet.WriteInteger(thread.CreatedSecondsAgo);
        packet.WriteInteger(thread.TotalMessages);
        packet.WriteInteger(thread.UnreadMessages);
        packet.WriteInteger(thread.LastMessageId);
        packet.WriteInteger(thread.LastMessageAuthorId);
        packet.WriteString(thread.LastMessageAuthorName);
        packet.WriteInteger(thread.LastMessageSecondsAgo);
        packet.WriteByte((byte)thread.State);
        packet.WriteInteger(thread.ModeratorId);
        packet.WriteString(thread.ModeratorName);
        packet.WriteInteger(thread.ModeratedSecondsAgo);
    }

    /// <summary>MessageData.readFromMessage.</summary>
    public static void WriteMessage(IServerPacket packet, GuildForumMessageSnapshot message)
    {
        packet.WriteInteger(message.MessageId);
        packet.WriteInteger(message.MessageIndex);
        packet.WriteInteger(message.AuthorId);
        packet.WriteString(message.AuthorName);
        packet.WriteString(message.AuthorFigure);
        packet.WriteInteger(message.CreatedSecondsAgo);
        packet.WriteString(message.Text);
        packet.WriteByte((byte)message.State);
        packet.WriteInteger(message.ModeratorId);
        packet.WriteString(message.ModeratorName);
        packet.WriteInteger(message.ModeratedSecondsAgo);
        packet.WriteInteger(message.AuthorPostCount);
    }
}
