using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Marketplace;

internal class CancelMarketplaceOfferMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new CancelMarketplaceOfferMessage();
}
