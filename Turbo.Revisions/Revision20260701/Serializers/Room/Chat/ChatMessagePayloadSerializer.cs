using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Chat;

internal static class ChatMessagePayloadSerializer
{
    public static void Serialize(IServerPacket packet, ChatMessageComposer message)
    {
        packet
            .WriteInteger(message.ObjectId)
            .WriteString(message.Text)
            .WriteInteger((int)message.Gesture)
            .WriteInteger(message.StyleId)
            .WriteInteger(message.Links.Count);

        foreach (var (url, title, isInternal) in message.Links)
            packet.WriteString(url).WriteString(title).WriteBoolean(isInternal);

        packet.WriteInteger(message.TrackingId);
    }
}
