using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Crafting;

internal class GetCraftableProductsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetCraftableProductsMessage();
}
