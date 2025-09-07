using Turbo.Contracts.Abstractions;
using Turbo.Packets.Abstractions;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class InitDiffieHandshakeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new InitDiffieHandshakeMessage();
}
