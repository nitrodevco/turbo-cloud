using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Pets;

internal class ConfirmBreedingRequestEventMessageComposerSerializer(int header)
    : AbstractSerializer<ConfirmBreedingRequestEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ConfirmBreedingRequestEventMessageComposer message
    )
    {
        //
    }
}
