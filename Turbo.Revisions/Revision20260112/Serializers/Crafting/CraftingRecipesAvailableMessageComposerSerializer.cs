using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Crafting;

internal class CraftingRecipesAvailableMessageComposerSerializer(int header)
    : AbstractSerializer<CraftingRecipesAvailableMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CraftingRecipesAvailableMessageComposer message
    )
    {
        //
    }
}
