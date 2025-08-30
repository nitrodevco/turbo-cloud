using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Packets.Parsers;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class InfoRetrieveMessageParser : AbstractParser<InfoRetrieveMessage>
{
    public override IMessageEvent Parse(IClientPacket packet) => new InfoRetrieveMessage();
}
