using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Chat;

internal static class ChatMessagePayloadSerializer
{
    /// <summary>What the client's parser holds as the receiver when none was sent.</summary>
    private const int NO_RECEIVER = -1;

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

        // Two optional trailing fields, read by position: the client takes the receiver's room
        // index if bytes remain, then the bubble width if bytes still remain
        // (ChatMessageParser.parse). A width without a receiver (a bot's talk or shout) must
        // still fill the receiver's slot, with the client's own "none"; this used to stop at a
        // missing receiver and so dropped every such width.
        if (message.ReceiverRoomIndex is null && message.ChatBubbleWidthOverride is null)
            return;

        packet.WriteInteger(message.ReceiverRoomIndex ?? NO_RECEIVER);

        if (message.ChatBubbleWidthOverride is { } bubbleWidth)
            packet.WriteInteger(bubbleWidth);
    }
}
