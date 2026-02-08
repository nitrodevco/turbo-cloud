using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

internal class GetCatalogPageWithEarliestExpiryMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetCatalogPageWithEarliestExpiryMessage();
}
