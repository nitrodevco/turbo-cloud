using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

internal class PetReceivedMessageComposerSerializer(int header)
    : AbstractSerializer<PetReceivedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetReceivedMessageComposer message)
    {
        packet.WriteBoolean(message.BoughtAsGift);

        PetDataSerializer.Serialize(packet, message.Pet);
    }
}
