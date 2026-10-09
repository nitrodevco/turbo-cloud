using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>Group, ThreadData.</summary>
internal class UpdateThreadMessageComposerSerializer(int header)
    : AbstractSerializer<UpdateThreadMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UpdateThreadMessageComposer message)
    {
        packet.WriteInteger(message.GroupId);
        GuildForumWriter.WriteThread(packet, message.Thread);
    }
}
