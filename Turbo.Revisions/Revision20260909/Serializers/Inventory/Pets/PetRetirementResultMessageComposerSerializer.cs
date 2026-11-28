using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

internal class PetRetirementResultMessageComposerSerializer(int header)
    : AbstractSerializer<PetRetirementResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PetRetirementResultMessageComposer message
    )
    {
        packet.WriteInteger(message.PetId).WriteBoolean(message.Success);
    }
}
