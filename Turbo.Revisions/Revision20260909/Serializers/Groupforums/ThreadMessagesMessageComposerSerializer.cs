using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>Group, thread, start index, a count of MessageData (AS3 ThreadMessagesMessageParser).</summary>
internal class ThreadMessagesMessageComposerSerializer(int header)
    : AbstractSerializer<ThreadMessagesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ThreadMessagesMessageComposer message)
    {
        packet.WriteInteger(message.GroupId);
        packet.WriteInteger(message.ThreadId);
        packet.WriteInteger(message.StartIndex);
        packet.WriteInteger(message.Messages.Length);

        foreach (var forumMessage in message.Messages)
            GuildForumWriter.WriteMessage(packet, forumMessage);
    }
}
