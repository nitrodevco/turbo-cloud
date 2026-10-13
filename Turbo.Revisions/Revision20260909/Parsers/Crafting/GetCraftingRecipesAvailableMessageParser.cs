using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Crafting;

/// <summary>outgoing/crafting/GetCraftingRecipesAvailableComposer.as:10: the table's id, then the mixer's item ids.</summary>
internal class GetCraftingRecipesAvailableMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetCraftingRecipesAvailableMessage
        {
            ObjectId = packet.PopInt(),
            ItemIds = CraftingItemIds.Pop(packet),
        };
}
