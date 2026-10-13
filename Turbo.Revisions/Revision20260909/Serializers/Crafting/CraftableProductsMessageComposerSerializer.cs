using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Crafting;

internal class CraftableProductsMessageComposerSerializer(int header)
    : AbstractSerializer<CraftableProductsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CraftableProductsMessageComposer message
    )
    {
        packet.WriteInteger(message.Products.Length);

        foreach (var product in message.Products)
            packet
                .WriteString(product.RecipeCode)
                .WriteString(product.ProductCode)
                .WriteString(product.FurnitureClassName);

        packet.WriteInteger(message.UsableFurniClasses.Length);

        foreach (var furniClass in message.UsableFurniClasses)
            packet.WriteString(furniClass);
    }
}
