using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Crafting;

/// <summary>The success flag, then the product only when it worked (CraftingResultMessageParser.as:13).</summary>
internal class CraftingResultMessageComposerSerializer(int header)
    : AbstractSerializer<CraftingResultMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CraftingResultMessageComposer message)
    {
        var product = message.Success ? message.Product : null;

        packet.WriteBoolean(product is not null);

        if (product is not null)
            packet
                .WriteString(product.RecipeCode)
                .WriteString(product.ProductCode)
                .WriteString(product.FurnitureClassName);
    }
}
