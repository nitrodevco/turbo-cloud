using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

/// <summary>The pet row shared by the inventory list, respect notifications and pet arrivals.</summary>
internal static class PetDataSerializer
{
    public static void Serialize(IServerPacket packet, PetSnapshot pet)
    {
        packet.WriteInteger(pet.Id).WriteString(pet.Name);

        PetFigureSerializer.Serialize(packet, pet.Figure);

        packet.WriteInteger(pet.Level).WriteInteger(pet.RarityLevel);
    }
}
