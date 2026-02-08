using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Crafting;

internal class CraftingRecipeMessageComposerSerializer(int header)
    : AbstractSerializer<CraftingRecipeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CraftingRecipeMessageComposer message)
    {
        //
    }
}
