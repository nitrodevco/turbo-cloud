using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Pets;

internal class PetAddedToInventoryEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetAddedToInventoryEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetAddedToInventoryEventMessageComposer message
    )
    {
        //
    }
}
