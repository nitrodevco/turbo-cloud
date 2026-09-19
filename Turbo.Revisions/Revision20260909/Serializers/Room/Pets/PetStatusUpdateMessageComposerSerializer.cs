using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Pets;

internal class PetStatusUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<PetStatusUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetStatusUpdateMessageComposer message)
    {
        packet
            .WriteInteger(message.ObjectId)
            .WriteInteger(message.PetId)
            .WriteBoolean(message.CanBreed)
            .WriteBoolean(message.CanHarvest)
            .WriteBoolean(message.CanRevive)
            .WriteBoolean(message.HasBreedingPermission);
    }
}
