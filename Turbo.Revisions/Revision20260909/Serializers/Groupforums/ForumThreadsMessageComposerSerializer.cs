using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>Group, start index, a count of ThreadData (AS3 ForumThreadsMessageParser).</summary>
internal class ForumThreadsMessageComposerSerializer(int header)
    : AbstractSerializer<ForumThreadsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ForumThreadsMessageComposer message)
    {
        packet.WriteInteger(message.GroupId);
        packet.WriteInteger(message.StartIndex);
        packet.WriteInteger(message.Threads.Length);

        foreach (var thread in message.Threads)
            GuildForumWriter.WriteThread(packet, thread);
    }
}
