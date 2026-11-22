using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Marketplace;

internal class MakeOfferMessageParser : IParser
{
    private const int MAX_ITEMS = 1000;

    public IMessageEvent Parse(IClientPacket packet)
    {
        var price = packet.PopInt();
        var furniType = packet.PopInt();
        var refs = packet.PopList(bytesPerItem: 4, p => p.PopInt(), MAX_ITEMS);

        return new MakeOfferMessage
        {
            Price = price,
            FurniType = furniType,
            ItemRefs = [.. refs],
        };
    }
}
