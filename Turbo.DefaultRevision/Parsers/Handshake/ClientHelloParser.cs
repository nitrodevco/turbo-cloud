using Turbo.Contracts.Abstractions;
using Turbo.Packets.Abstractions;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.DefaultRevision.Parsers.Handshake;

public class ClientHelloParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ClientHelloMessage { Production = packet.PopString() };
}
