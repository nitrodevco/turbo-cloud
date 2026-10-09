using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>Group, ThreadData.</summary>
internal class PostThreadMessageComposerSerializer(int header)
    : AbstractSerializer<PostThreadMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PostThreadMessageComposer message)
    {
        packet.WriteInteger(message.GroupId);
        GuildForumWriter.WriteThread(packet, message.Thread);
    }
}
