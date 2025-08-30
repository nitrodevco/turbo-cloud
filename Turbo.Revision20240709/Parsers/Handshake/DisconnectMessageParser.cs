using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Packets.Parsers;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class DisconnectMessageParser : AbstractParser<DisconnectMessage>
{
    public override IMessageEvent Parse(IClientPacket packet) => new DisconnectMessage();
}
