using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Crafting;

/// <summary>outgoing/crafting/GetCraftableProductsComposer.as:16: the table's id.</summary>
internal class GetCraftableProductsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetCraftableProductsMessage { ObjectId = packet.PopInt() };
}
