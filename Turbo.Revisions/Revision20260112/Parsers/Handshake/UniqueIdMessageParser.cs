using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Handshake;

internal class UniqueIdMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UniqueIdMessage
        {
            MachineID = packet.PopString(),
            Fingerprint = packet.PopString(),
            FlashVersion = packet.PopString(),
        };
}
