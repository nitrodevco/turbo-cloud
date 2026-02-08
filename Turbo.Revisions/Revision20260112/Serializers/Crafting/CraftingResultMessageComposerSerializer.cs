using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Crafting;

internal class CraftingResultMessageComposerSerializer(int header)
    : AbstractSerializer<CraftingResultMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CraftingResultMessageComposer message)
    {
        //
    }
}
