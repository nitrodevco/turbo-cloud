using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Pets;

internal class PetBreedingResultEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetBreedingResultEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetBreedingResultEventMessageComposer message
    )
    {
        SerializeResult(packet, message.Result);
        SerializeResult(packet, message.OtherResult);
    }

    private static void SerializeResult(IServerPacket packet, PetBreedingResultSnapshot result)
    {
        packet
            .WriteInteger(result.StuffId)
            .WriteInteger(result.ClassId)
            .WriteString(result.ProductCode)
            .WriteInteger(result.OwnerId)
            .WriteString(result.OwnerName)
            .WriteInteger(result.RarityLevel)
            .WriteBoolean(result.HasMutation);
    }
}
