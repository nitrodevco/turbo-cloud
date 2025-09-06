using Turbo.Packets.Abstractions;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Primitives;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class SSOTicketMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SSOTicketMessage { SSO = packet.PopString(), ElapsedMilliseconds = packet.PopInt() };
}
