using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Marketplace;

internal class GetMarketplaceOffersMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetMarketplaceOffersMessage
        {
            MinPrice = packet.PopInt(),
            MaxPrice = packet.PopInt(),
            SearchQuery = packet.PopString(),
            SortType = packet.PopInt(),
            CombineUniques = packet.Remaining > 0 && packet.PopBoolean(),
        };
}
