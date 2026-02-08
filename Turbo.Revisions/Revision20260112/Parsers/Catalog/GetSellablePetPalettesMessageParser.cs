using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

internal class GetSellablePetPalettesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetSellablePetPalettesMessage { LocalizationId = packet.PopInt() };
}
