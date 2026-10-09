using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>Group, thread, MessageData.</summary>
internal class UpdateMessageMessageComposerSerializer(int header)
    : AbstractSerializer<UpdateMessageMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UpdateMessageMessageComposer message)
    {
        packet.WriteInteger(message.GroupId);
        packet.WriteInteger(message.ThreadId);
        GuildForumWriter.WriteMessage(packet, message.Message);
    }
}
