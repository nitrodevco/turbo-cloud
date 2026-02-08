using Turbo.Primitives.Messages.Incoming.Inventory.Furni;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Inventory.Furni;

internal class RequestFurniInventoryMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new RequestFurniInventoryMessage();
}
