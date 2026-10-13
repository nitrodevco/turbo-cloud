using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Marketplace;

internal class GetMarketplaceOwnOffersMessageParser : IParser
{
    // Flash's composer defaults the category to 1 (open offers).
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetMarketplaceOwnOffersMessage
        {
            Category = packet.Remaining >= 4 ? packet.PopInt() : 1,
        };
}
