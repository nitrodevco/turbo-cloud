using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Chat;

internal class WhisperMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var parts = packet.PopString().Split(' ', 2);
        var recipientName = parts[0];
        var text = parts[1];

        return new WhisperMessage
        {
            RecipientName = recipientName,
            Text = text,
            StyleId = packet.PopInt(),
        };
    }
}
