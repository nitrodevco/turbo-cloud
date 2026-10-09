using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>ForumData, then the permissions, their errors, settings and staff flags (AS3 ExtendedForumData).</summary>
internal class ForumDataMessageComposerSerializer(int header)
    : AbstractSerializer<ForumDataMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ForumDataMessageComposer message)
    {
        var forum = message.Forum;

        GuildForumWriter.WriteForum(packet, forum);
        packet.WriteInteger((int)forum.ReadPermission);
        packet.WriteInteger((int)forum.PostMessagePermission);
        packet.WriteInteger((int)forum.PostThreadPermission);
        packet.WriteInteger((int)forum.ModeratePermission);
        packet.WriteString(forum.ReadError);
        packet.WriteString(forum.PostMessageError);
        packet.WriteString(forum.PostThreadError);
        packet.WriteString(forum.ModerateError);
        packet.WriteString(forum.ReportError);
        packet.WriteBoolean(forum.CanChangeSettings);
        packet.WriteBoolean(forum.IsStaff);
    }
}
