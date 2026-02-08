using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

internal class PurchaseFromCatalogMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new PurchaseFromCatalogMessage
        {
            PageId = packet.PopInt(),
            OfferId = packet.PopInt(),
            ExtraParam = packet.PopString(),
            Quantity = packet.PopInt(),
        };
}
