using System.Linq;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

/// <summary>
/// A fragment of the pets tab. <paramref name="withLocations"/> is the layout a client with
/// <c>pet_inventory_locations</c> reads: every pet of the fragment, each ending with where it is
/// (build 116's <c>PetData</c>). An older client reads no room and takes every pet listed as
/// one in the inventory, so a pet standing in a room is left out of its list.
/// </summary>
internal class PetInventoryEventMessageComposerSerializer(int header, bool withLocations = false)
    : AbstractSerializer<PetInventoryEventMessageComposer>(header)
{
    private readonly bool _withLocations = withLocations;

    protected override void Serialize(
        IServerPacket packet,
        PetInventoryEventMessageComposer message
    )
    {
        packet.WriteInteger(message.TotalFragments).WriteInteger(message.CurrentFragment);

        if (_withLocations)
        {
            packet.WriteInteger(message.Pets.Length);

            foreach (var pet in message.Pets)
                PetDataSerializer.SerializeWithLocation(
                    packet,
                    pet,
                    message.Rooms.FirstOrDefault(x => x.RoomId == pet.RoomId)
                );

            return;
        }

        packet.WriteInteger(message.Pets.Count(x => x.RoomId is null));

        foreach (var pet in message.Pets)
        {
            if (pet.RoomId is null)
                PetDataSerializer.Serialize(packet, pet);
        }
    }
}
