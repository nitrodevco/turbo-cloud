using Turbo.Primitives.Messages.Incoming.Landingview;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Landingview;

internal class GetPromoArticlesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetPromoArticlesMessage();
}
