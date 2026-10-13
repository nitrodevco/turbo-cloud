using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Crafting;

internal class CraftingRecipeMessageComposerSerializer(int header)
    : AbstractSerializer<CraftingRecipeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CraftingRecipeMessageComposer message)
    {
        packet.WriteInteger(message.Ingredients.Length);

        foreach (var ingredient in message.Ingredients)
            packet.WriteInteger(ingredient.Count).WriteString(ingredient.FurnitureClassName);
    }
}
