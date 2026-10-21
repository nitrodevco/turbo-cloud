using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Chat;

internal static class ChatMessagePayloadSerializer
{
    /// <summary>What the client's parser holds as the receiver when none was sent.</summary>
    private const int NO_RECEIVER = -1;

    /// <summary>What the client's parser holds as the bubble width when none was sent.</summary>
    private const int NO_BUBBLE_WIDTH = -1;

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
        // WIN63-202610121624 reads a third, hideUsername, the same way, so hiding the name
        // fills both slots before it.
        // WIN63-202610201032 reads a fourth, the bubble's tile (two ints), after all three.
        if (
            message.ReceiverRoomIndex is null
            && message.ChatBubbleWidthOverride is null
            && !message.HideUsername
            && message.Location is null
        )
            return;

        // A receiver always comes with a width: Habbo's wired whispers (the triggerer's hidden
        // message echoed back, Show Message's whisper) end with receiver 0 and width -1 even when
        // the box sets no width (official client capture, WIN63-202610121709).
        packet
            .WriteInteger(message.ReceiverRoomIndex ?? NO_RECEIVER)
            .WriteInteger(message.ChatBubbleWidthOverride ?? NO_BUBBLE_WIDTH);

        if (!message.HideUsername && message.Location is null)
            return;

        packet.WriteBoolean(message.HideUsername);

        if (message.Location is { } location)
            packet.WriteInteger(location.X).WriteInteger(location.Y);
    }
}
