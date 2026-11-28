using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

/// <summary>
/// A pet that joined the pets tab. <paramref name="withLocations"/> is the layout a client with
/// <c>pet_inventory_locations</c> reads, whose pet ends with where it is: in the inventory.
/// </summary>
internal class PetAddedToInventoryEventMessageComposerSerializer(
    int header,
    bool withLocations = false
) : AbstractSerializer<PetAddedToInventoryEventMessageComposer>(header)
{
    private readonly bool _withLocations = withLocations;

    protected override void Serialize(
        IServerPacket packet,
        PetAddedToInventoryEventMessageComposer message
    )
    {
        if (_withLocations)
            PetDataSerializer.SerializeWithLocation(packet, message.Pet, null);
        else
            PetDataSerializer.Serialize(packet, message.Pet);

        packet.WriteBoolean(message.OpenInventory);
    }
}
