using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Inventory.Pets;

internal class RetirePetMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RetirePetMessage { PetId = packet.PopInt() };
}
