using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Chat;

internal class ShoutMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ShoutMessage { Text = packet.PopString(), StyleId = packet.PopInt() };
}
