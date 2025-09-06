using Turbo.Packets.Abstractions;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Primitives;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class CompleteDiffieHandshakeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CompleteDiffieHandshakeMessage { SharedKey = packet.PopString() };
}
