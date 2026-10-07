using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

internal class WiredTradeAddDeleteItemsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var isDelete = packet.PopBoolean();
        var itemIds = packet.PopList(bytesPerItem: 4, p => p.PopInt());

        return new WiredTradeAddDeleteItemsMessage { IsDelete = isDelete, ItemIds = [.. itemIds] };
    }
}
