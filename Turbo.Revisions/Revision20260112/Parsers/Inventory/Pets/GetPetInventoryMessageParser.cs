using Turbo.Primitives.Messages.Incoming.Inventory.Pets;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Inventory.Pets;

internal class GetPetInventoryMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetPetInventoryMessage();
}
