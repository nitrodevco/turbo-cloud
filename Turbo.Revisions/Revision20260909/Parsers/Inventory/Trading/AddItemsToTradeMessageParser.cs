using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Revisions.Revision20260909.Parsers.Inventory.Trading;

internal class AddItemsToTradeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var itemIds = packet.PopList<RoomObjectId>(bytesPerItem: 4, p => p.PopInt());

        return new AddItemsToTradeMessage { ItemIds = [.. itemIds] };
    }
}
