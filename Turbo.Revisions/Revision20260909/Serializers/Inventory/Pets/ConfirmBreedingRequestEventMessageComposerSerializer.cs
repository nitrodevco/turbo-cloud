using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

internal class ConfirmBreedingRequestEventMessageComposerSerializer(int header)
    : AbstractSerializer<ConfirmBreedingRequestEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ConfirmBreedingRequestEventMessageComposer message
    )
    {
        packet.WriteInteger(message.NestId);

        SerializePet(packet, message.Pet1);
        SerializePet(packet, message.Pet2);

        packet.WriteInteger(message.RarityCategories.Length);

        foreach (var category in message.RarityCategories)
        {
            packet.WriteInteger(category.Chance).WriteInteger(category.PaletteIds.Length);

            foreach (var paletteId in category.PaletteIds)
                packet.WriteInteger(paletteId);
        }

        packet.WriteInteger(message.ResultPetTypeId);
    }

    private static void SerializePet(IServerPacket packet, PetBreedingPetSnapshot pet)
    {
        packet
            .WriteInteger(pet.PetId)
            .WriteString(pet.Name)
            .WriteInteger(pet.Level)
            .WriteString(pet.Figure)
            .WriteString(pet.OwnerName);
    }
}
