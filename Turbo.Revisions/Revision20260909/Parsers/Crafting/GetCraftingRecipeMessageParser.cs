using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Crafting;

/// <summary>outgoing/crafting/GetCraftingRecipeComposer.as:16: the recipe code.</summary>
internal class GetCraftingRecipeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetCraftingRecipeMessage { RecipeCode = packet.PopString() };
}
