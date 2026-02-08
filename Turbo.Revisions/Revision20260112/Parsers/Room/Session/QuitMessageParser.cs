using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Session;

internal class QuitMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new QuitMessage();
}
