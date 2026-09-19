using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

internal class PetInventoryEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetInventoryEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetInventoryEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.TotalFragments)
            .WriteInteger(message.CurrentFragment)
            .WriteInteger(message.Pets.Length);

        foreach (var pet in message.Pets)
            PetDataSerializer.Serialize(packet, pet);
    }
}
