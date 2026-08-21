using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Pets;

internal class PetBreedingResultEventMessageComposerSerializer(int header)
    : AbstractSerializer<PetBreedingResultEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetBreedingResultEventMessageComposer message
    )
    {
        //
    }
}
