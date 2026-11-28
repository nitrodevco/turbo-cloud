using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

/// <summary>
/// "You bought a pet". <paramref name="withLocations"/> is the layout a client with
/// <c>pet_inventory_locations</c> reads, whose pet ends with where it is: in the inventory.
/// </summary>
internal class PetReceivedMessageComposerSerializer(int header, bool withLocations = false)
    : AbstractSerializer<PetReceivedMessageComposer>(header)
{
    private readonly bool _withLocations = withLocations;

    protected override void Serialize(IServerPacket packet, PetReceivedMessageComposer message)
    {
        packet.WriteBoolean(message.BoughtAsGift);

        if (_withLocations)
            PetDataSerializer.SerializeWithLocation(packet, message.Pet, null);
        else
            PetDataSerializer.Serialize(packet, message.Pet);
    }
}
