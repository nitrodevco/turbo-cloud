using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

internal class PurchaseFromCatalogAsGiftMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new PurchaseFromCatalogAsGiftMessage
        {
            PageId = packet.PopInt(),
            OfferCode = packet.PopInt(),
            ExtraParam = packet.PopString(),
            RecieverName = packet.PopString(),
            Message = packet.PopString(),
            BoxStuffTypeId = packet.PopInt(),
            BoxTypeId = packet.PopInt(),
            RibbonTypeId = packet.PopInt(),
            ShowPurchaserName = packet.PopBoolean(),
        };
}
