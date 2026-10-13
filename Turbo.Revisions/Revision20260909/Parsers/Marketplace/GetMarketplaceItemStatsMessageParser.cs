using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Marketplace;

internal class GetMarketplaceItemStatsMessageParser : IParser
{
    // Flash sends the data string only for a poster.
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetMarketplaceItemStatsMessage
        {
            Category = packet.PopInt(),
            FurniTypeId = packet.PopInt(),
            ExtraData = packet.Remaining > 0 ? packet.PopString() : null,
        };
}
