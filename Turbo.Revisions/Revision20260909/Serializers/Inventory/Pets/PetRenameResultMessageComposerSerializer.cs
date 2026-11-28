using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Pets;

internal class PetRenameResultMessageComposerSerializer(int header)
    : AbstractSerializer<PetRenameResultMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetRenameResultMessageComposer message)
    {
        packet.WriteInteger(message.PetId).WriteBoolean(message.Success);
    }
}
