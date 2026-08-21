using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Session;

internal class QuitMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new QuitMessage();
}
