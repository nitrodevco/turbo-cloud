using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Inventory.Pets;

internal class PetRemovedFromInventoryEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetRemovedFromInventoryEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetRemovedFromInventoryEventMessageComposer message
    )
    {
        //
    }
}
