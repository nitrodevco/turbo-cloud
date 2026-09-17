using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Chat;

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

        // optional trailing fields; the client reads each only if bytes remain
        if (message.ReceiverRoomIndex is null)
            return;

        packet.WriteInteger(message.ReceiverRoomIndex.Value);

        if (message.ChatBubbleWidthOverride is not null)
            packet.WriteInteger(message.ChatBubbleWidthOverride.Value);
    }
}
