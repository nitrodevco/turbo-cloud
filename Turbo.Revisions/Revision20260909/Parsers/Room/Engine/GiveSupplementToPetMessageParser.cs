using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Engine;

internal class GiveSupplementToPetMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GiveSupplementToPetMessage
        {
            PetId = packet.PopInt(),
            Supplement = (PetSupplementType)packet.PopInt(),
        };
}
