using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

/// <summary>The pet row shared by the inventory list, respect notifications and pet arrivals.</summary>
internal static class PetDataSerializer
{
    public static void Serialize(IServerPacket packet, PetSnapshot pet)
    {
        packet.WriteInteger(pet.Id).WriteString(pet.Name);

        PetFigureSerializer.Serialize(packet, pet.Figure);

        packet
            .WriteInteger(pet.Level)
            .WriteInteger(PetTypes.InventoryRarityLevel(pet.Figure.TypeId, pet.RarityLevel));
    }

    /// <summary>
    /// The row a client naming <c>pet_inventory_locations</c> reads (build 116's
    /// <c>PetData</c>): the same, then the id of the room the pet stands in and, only then, that
    /// room's name and description. A pet in the inventory ends with -1, the client's "no room".
    /// A room nothing is known of is written with empty texts, which the client still links to.
    /// </summary>
    public static void SerializeWithLocation(
        IServerPacket packet,
        PetSnapshot pet,
        PetRoomSnapshot? room
    )
    {
        Serialize(packet, pet);

        if (pet.RoomId is not { } roomId)
        {
            packet.WriteInteger(-1);

            return;
        }

        packet
            .WriteInteger(roomId.Value)
            .WriteString(room?.Name ?? string.Empty)
            .WriteString(room?.Description ?? string.Empty);
    }
}
