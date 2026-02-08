using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

internal class PurchaseRoomAdMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new PurchaseRoomAdMessageMessage
        {
            PageId = packet.PopInt(),
            OfferId = packet.PopInt(),
            FlatId = packet.PopInt(),
            Name = packet.PopString(),
            Extended = packet.PopBoolean(),
            Description = packet.PopString(),
            CategoryId = packet.PopInt(),
        };
}
