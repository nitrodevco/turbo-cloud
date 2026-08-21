using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Inventory.Pets;

internal class PetReceivedMessageComposerSerializer(int header)
    : AbstractSerializer<PetReceivedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PetReceivedMessageComposer message)
    {
        //
    }
}
