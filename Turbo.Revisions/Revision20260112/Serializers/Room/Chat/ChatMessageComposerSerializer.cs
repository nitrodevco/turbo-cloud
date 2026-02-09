using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Chat;

internal class ChatMessageComposerSerializer(int header)
    : AbstractSerializer<ChatMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ChatMessageComposer message)
    {
        packet
            .WriteInteger(message.UserId)
            .WriteString(message.Text)
            .WriteInteger((int)message.Gesture)
            .WriteInteger(message.StyleId)
            .WriteInteger(message.Links.Count);

        foreach (var (one, two, three) in message.Links)
            packet.WriteString(one).WriteString(two).WriteBoolean(three);

        packet.WriteInteger(message.TrackingId);
    }
}
