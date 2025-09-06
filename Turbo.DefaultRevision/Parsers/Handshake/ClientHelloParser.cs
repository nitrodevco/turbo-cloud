using Turbo.Packets.Abstractions;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Primitives;

namespace Turbo.DefaultRevision.Parsers.Handshake;

public class ClientHelloParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ClientHelloMessage { Production = packet.PopString() };
}
