using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Groupforums;

/// <summary>List code, total, start index, a count of ForumData (AS3 GetForumsListMessageParser).</summary>
internal class ForumsListMessageComposerSerializer(int header)
    : AbstractSerializer<ForumsListMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ForumsListMessageComposer message)
    {
        packet.WriteInteger((int)message.ListCode);
        packet.WriteInteger(message.TotalAmount);
        packet.WriteInteger(message.StartIndex);
        packet.WriteInteger(message.Forums.Length);

        foreach (var forum in message.Forums)
            GuildForumWriter.WriteForum(packet, forum);
    }
}
