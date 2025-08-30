using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Packets.Parsers;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class SSOTicketMessageParser : AbstractParser<SSOTicketMessage>
{
    public override IMessageEvent Parse(IClientPacket packet) =>
        new SSOTicketMessage { SSO = packet.PopString(), ElapsedMilliseconds = packet.PopInt() };
}
