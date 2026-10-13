using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

internal class UserChatlogEventMessageComposerSerializer(int header)
    : AbstractSerializer<UserChatlogEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserChatlogEventMessageComposer message)
    {
        packet.WriteInteger(message.UserId).WriteString(message.UserName);
        packet.WriteInteger(message.Records.Length);

        foreach (var record in message.Records)
            ModChatlogWriter.Write(packet, record);
    }
}
