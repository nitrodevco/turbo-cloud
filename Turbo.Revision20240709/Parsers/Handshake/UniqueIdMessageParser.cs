using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Packets.Parsers;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class UniqueIdMessageParser : AbstractParser<UniqueIdMessage>
{
    public override IMessageEvent Parse(IClientPacket packet) =>
        new UniqueIdMessage
        {
            MachineID = packet.PopString(),
            Fingerprint = packet.PopString(),
            FlashVersion = packet.PopString(),
        };
}
